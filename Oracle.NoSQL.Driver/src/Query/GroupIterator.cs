/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.Query {
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
        private static readonly IntegerValue One = new IntegerValue(1);

        private readonly GroupStep step;
        private readonly PlanAsyncIterator inputIterator;
        private readonly Dictionary<FieldValue[], FieldValue[]> groupMap;
        private readonly FieldValue[] groupTuple;
        private IEnumerator<KeyValuePair<FieldValue[], FieldValue[]>>
            resultEnumerator;

        internal GroupIterator(QueryRuntime runtime, GroupStep step) :
            base(runtime)
        {
            this.step = step;
            inputIterator = step.InputStep.CreateAsyncIterator(runtime);
            groupMap = new Dictionary<FieldValue[], FieldValue[]>(this);
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

        private FieldValue[] CreateAggregateTuple()
        {
            var aggregateTuple = new FieldValue[step.ColumnNames.Length -
                                                step.GroupingColumnCount];
            for (var i = 0; i < aggregateTuple.Length; i++)
            {
                aggregateTuple[i] = IsCountFunc(step.AggregateFuncCodes[i]) ?
                    new IntegerValue(0) : (FieldValue)FieldValue.Null;
            }

            return aggregateTuple;
        }

        private static bool IsMinMax(SQLFuncCode code, int result)
        {
            return (code == SQLFuncCode.Min && result < 0) || result > 0;
        }

        private void AggregateColumn(ref FieldValue aggregate,
            SQLFuncCode funcCode, FieldValue newValue)
        {
            var oldValue = aggregate;
            switch (funcCode)
            {
                case SQLFuncCode.Min: case SQLFuncCode.Max:
                    if (!newValue.SupportsComparison || newValue.IsSpecial)
                    {
                        break;
                    }

                    if (aggregate == FieldValue.Null || IsMinMax(funcCode,
                        newValue.QueryCompare(aggregate)))
                    {
                        aggregate = newValue;
                    }
                    break;
                case SQLFuncCode.Sum:
                    if (!newValue.IsNumeric)
                    {
                        break;
                    }

                    aggregate = aggregate == FieldValue.Null ?
                        newValue : aggregate.QueryAdd(newValue);

                    break;
                case SQLFuncCode.CountStar:
                    aggregate = aggregate.QueryAdd(One);
                    break;
                case SQLFuncCode.Count:
                    if (!newValue.IsSpecial)
                    {
                        aggregate = aggregate.QueryAdd(One);
                    }

                    break;
                case SQLFuncCode.CountNumbers:
                    if (newValue.IsNumeric)
                    {
                        aggregate = aggregate.QueryAdd(One);
                    }

                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            // Add() will return the same FieldValue if it's type has not
            // changed, in which case we don't need to update the memory.
            if (aggregate != oldValue)
            {
                runtime.TotalMemory += aggregate.GetMemorySize() -
                                       oldValue.GetMemorySize();
            }
        }

        private void Aggregate(FieldValue[] aggregateTuple, RecordValue row)
        {
            for (var i = 0; i < aggregateTuple.Length; i++)
            {
                var value =
                    row[step.ColumnNames[step.GroupingColumnCount + i]];
                AggregateColumn(ref aggregateTuple[i],
                    step.AggregateFuncCodes[i], value);
            }
        }

        private RecordValue MakeResult(FieldValue[] groupingTuple,
            FieldValue[] aggregateTuple)
        {
            var result = new RecordValue();
            int i;
            for (i = 0; i < step.GroupingColumnCount; i++)
            {
                result[step.ColumnNames[i]] = groupingTuple[i];
            }

            for (; i < step.ColumnNames.Length; i++)
            {
                result[step.ColumnNames[i]] =
                    aggregateTuple[i - step.GroupingColumnCount];
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
                                GetMemorySize(groupTuple),
                                aggregateTuple != null ? GetMemorySize(
                                    aggregateTuple) : 0);
                        }

                        // If there are no aggregates, we are ready to return the row
                        // and don't need to iterate over groupMap.
                        if (!HasAggregateColumns)
                        {
                            Result = row;
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
