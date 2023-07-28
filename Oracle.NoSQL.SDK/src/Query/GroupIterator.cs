/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;
    using static SizeOf;

    internal class GroupIterator : PlanAsyncIterator,
        IEqualityComparer<FieldValue[]>
    {
        private readonly GroupStep step;
        private readonly PlanAsyncIterator inputIterator;
        private readonly Dictionary<FieldValue[], ValueAggregator[]> groupMap;
        private readonly FieldValue[] groupTuple;
        private IEnumerator<KeyValuePair<FieldValue[], ValueAggregator[]>>
            resultEnumerator;

        private static long GetMemorySize(ValueAggregator[] aggregatorArray)
        {
            long result = ArrayOverhead +
                          IntPtr.Size * aggregatorArray.Length;
            foreach (var value in aggregatorArray)
            {
                result += value.GetMemorySize();
            }

            return result;
        }

        internal GroupIterator(QueryRuntime runtime, GroupStep step) :
            base(runtime)
        {
            this.step = step;
            inputIterator = step.InputStep.CreateAsyncIterator(runtime);
            groupMap = new Dictionary<FieldValue[], ValueAggregator[]>(this);
            groupTuple = new FieldValue[step.GroupingColumnCount];
        }

        public bool Equals(FieldValue[] tuple1, FieldValue[] tuple2)
        {
            Debug.Assert(tuple1 != null && tuple2 != null);
            Debug.Assert(tuple1.Length == tuple2.Length);
            for (var i = 0; i < tuple1.Length; i++)
            {
                if (!tuple1[i].QueryEquals(tuple2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(FieldValue[] tuple)
        {
            var hashCode = 1;
            foreach (var value in tuple)
            {
                hashCode = 31 * hashCode + value.QueryHashCode();
            }

            return hashCode;
        }

        private bool HasAggregateColumns =>
            step.ColumnNames.Length != step.GroupingColumnCount;

        private bool InitGroupTuple(RecordValue row)
        {
            for (var i = 0; i < step.GroupingColumnCount; i++)
            {
                var value = row[step.ColumnNames[i]];
                if (value == FieldValue.Empty)
                {
                    if (!step.IsDistinct)
                    {
                        return false;
                    }
                    value = FieldValue.Null;
                }

                groupTuple[i] = value;
            }

            return true;
        }

        private ValueAggregator[] CreateAggregateTuple()
        {
            var aggregateTuple = new ValueAggregator[step.ColumnNames.Length -
                step.GroupingColumnCount];

            for (var i = 0; i < aggregateTuple.Length; i++)
            {
                switch (step.AggregateFuncCodes[i])
                {
                    case SQLFuncCode.CountStar:
                        aggregateTuple[i] = new CountStarAggregator();
                        break;
                    case SQLFuncCode.Count:
                        aggregateTuple[i] = new CountAggregator();
                        break;
                    case SQLFuncCode.CountNumbers:
                        aggregateTuple[i] = new CountNumbersAggregator();
                        break;
                    case SQLFuncCode.Min:
                        aggregateTuple[i] = new MinValueAggregator();
                        break;
                    case SQLFuncCode.Max:
                        aggregateTuple[i] = new MaxValueAggregator();
                        break;
                    case SQLFuncCode.Sum:
                        aggregateTuple[i] = new SumAggregator();
                        break;
                    case SQLFuncCode.ArrayCollect:
                        aggregateTuple[i] =
                            new CollectAggregator(runtime.IsForTest);
                        break;
                    case SQLFuncCode.ArrayCollectDistinct:
                        aggregateTuple[i] =
                            new CollectDistinctAggregator(runtime.IsForTest);
                        break;
                    default:
                        // this is already checked in DeserializeSQLFuncCode
                        Debug.Fail("Unexpected SQLFuncCode: " +
                                   step.AggregateFuncCodes[i]);
                        break;
                }
            }

            return aggregateTuple;
        }

        private void Aggregate(ValueAggregator[] aggregateTuple,
            RecordValue row)
        {
            for (var i = 0; i < aggregateTuple.Length; i++)
            {
                var value =
                    row[step.ColumnNames[step.GroupingColumnCount + i]];
                if (step.CountMemory)
                {
                    runtime.TotalMemory +=
                        aggregateTuple[i].Aggregate(value, true);
                }
                else
                {
                    aggregateTuple[i].Aggregate(value);
                }
            }
        }

        private RecordValue MakeResult(FieldValue[] groupingTuple,
            ValueAggregator[] aggregateTuple = null)
        {
            var result = new RecordValue();
            int i;
            for (i = 0; i < step.GroupingColumnCount; i++)
            {
                result[step.ColumnNames[i]] = groupingTuple[i];
            }

            if (aggregateTuple == null)
            {
                Debug.Assert(!HasAggregateColumns);
                return result;
            }

            for (; i < step.ColumnNames.Length; i++)
            {
                result[step.ColumnNames[i]] =
                    aggregateTuple[i - step.GroupingColumnCount].Result;
            }

            return result;
        }

        internal override async Task<bool> NextAsync(
            CancellationToken cancellationToken)
        {
            if (resultEnumerator == null)
            {
                while (await inputIterator.NextAsync(cancellationToken))
                {
                    if (!(inputIterator.Result is RecordValue row))
                    {
                        throw new InvalidOperationException(
                            "Input to group step is not a record value: " +
                            inputIterator.Result);
                    }

                    if (!InitGroupTuple(row))
                    {
                        continue;
                    }

                    if (!groupMap.TryGetValue(groupTuple, out var aggregateTuple))
                    {
                        aggregateTuple = HasAggregateColumns ?
                            CreateAggregateTuple() : null;
                        groupMap[(FieldValue[])groupTuple.Clone()] =
                            aggregateTuple;
                        if (step.CountMemory)
                        {
                            runtime.TotalMemory += GetDictionaryEntrySize(
                                SizeOf.GetMemorySize(groupTuple),
                                aggregateTuple != null ? GetMemorySize(
                                    aggregateTuple) : 0);
                        }

                        // If there are no aggregates, we are ready to return the row
                        // and don't need to iterate over groupMap.
                        if (!HasAggregateColumns)
                        {
                            Result = MakeResult(groupTuple);
                            return true;
                        }
                    }

                    if (HasAggregateColumns)
                    {
                        Aggregate(aggregateTuple, row);
                    }
                }

                if (runtime.NeedContinuation || !HasAggregateColumns)
                {
                    return false;
                }

                resultEnumerator = groupMap.GetEnumerator();
            }

            if (!resultEnumerator.MoveNext())
            {
                return false;
            }

            var current = resultEnumerator.Current;
            Result = MakeResult(current.Key, current.Value);
            if (step.RemoveResult)
            {
                groupMap.Remove(current.Key);
                // It is not allowed to modify/delete entries while
                // enumerating, enumerator will be invalidated in this case.
                // This is the only work around I see.  Is this expensive?
                resultEnumerator = groupMap.GetEnumerator();
            }
            return true;
        }

        internal override PlanStep Step => step;

    }

}
