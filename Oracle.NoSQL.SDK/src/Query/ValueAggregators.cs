/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using static SizeOf;

    internal abstract class ValueAggregator
    {
        protected internal FieldValue aggregateValue;

        protected internal virtual FieldValue GetInitialValue() =>
            FieldValue.Null;

        internal virtual FieldValue Result =>
            aggregateValue ?? GetInitialValue();

        // If countMemory = true, returns increase in memory size,
        // otherwise 0.
        internal abstract long Aggregate(FieldValue value, bool countMemory);

        // No memory counting, this can be overriden for efficiency.
        internal virtual void Aggregate(FieldValue value) =>
            Aggregate(value, false);

        internal virtual long GetMemorySize() =>
            GetObjectSize(aggregateValue?.GetMemorySize() ?? 0);

        internal virtual void Clear()
        {
            aggregateValue = null;
        }
    }

    internal abstract class SingleValueAggregator : ValueAggregator
    {
        internal abstract void AggregateInternal(FieldValue value);

        internal override void Aggregate(FieldValue value) =>
            AggregateInternal(value);

        internal override long Aggregate(FieldValue value, bool countMemory)
        {
            var oldValue = aggregateValue;
            Aggregate(value);
            
            if (!countMemory || aggregateValue == oldValue)
            {
                return 0;
            }

            // Aggregate() should never set aggregateValue back to null.
            Debug.Assert(aggregateValue != null);
            return aggregateValue.GetMemorySize() -
                   (oldValue?.GetMemorySize() ?? 0);
        }
    }

    internal abstract class MinMaxAggregatorBase : SingleValueAggregator
    {
        protected internal abstract bool IsMinMax(int compareResult);

        internal override void AggregateInternal(FieldValue value)
        {
            Debug.Assert(value != null);
            if (value == FieldValue.Null || value == FieldValue.Empty)
            {
                return;
            }

            if (aggregateValue == null)
            {
                aggregateValue = value;
            }
            else
            {
                var compareResult = value.QueryCompare(aggregateValue);
                if (IsMinMax(compareResult))
                {
                    aggregateValue = value;
                }
            }
        }
    }

    internal class MinValueAggregator : MinMaxAggregatorBase
    {
        protected internal override bool IsMinMax(int compareResult) =>
            compareResult < 0;
    }

    internal class MaxValueAggregator : MinMaxAggregatorBase
    {
        protected internal override bool IsMinMax(int compareResult) =>
            compareResult > 0;
    }

    internal class SumAggregator : SingleValueAggregator
    {
        internal override void AggregateInternal(FieldValue value)
        {
            if (value == FieldValue.Null || value == FieldValue.Empty)
            {
                return;
            }

            aggregateValue = aggregateValue == null
                ? value
                : aggregateValue.QueryAdd(value);
        }
    }

    internal abstract class CountAggregatorBase : SingleValueAggregator
    {
        protected internal static readonly IntegerValue One =
            new IntegerValue(1);

        protected internal override FieldValue GetInitialValue() =>
            new IntegerValue(0);

        protected internal abstract bool ToCount(FieldValue value);

        internal override void AggregateInternal(FieldValue value)
        {
            aggregateValue ??= GetInitialValue();
            Debug.Assert(value != null);

            if (ToCount(value))
            {
                aggregateValue = aggregateValue.QueryAdd(One);
            }
        }
    }

    internal class CountStarAggregator : CountAggregatorBase
    {
        protected internal override bool ToCount(FieldValue value) => true;
    }

    internal class CountAggregator : CountAggregatorBase
    {
        protected internal override bool ToCount(FieldValue value) =>
            !value.IsSpecial;
    }

    internal class CountNumbersAggregator : CountAggregatorBase
    {
        protected internal override bool ToCount(FieldValue value) =>
            value.IsNumeric;
    }

    internal abstract class CollectAggregatorBase : ValueAggregator
    {
        protected internal readonly bool toSortResults;

        protected internal abstract void AddValue(FieldValue value);
        
        protected internal abstract long GetMemorySize(long valueSize);

        protected internal static FieldValue SortResults(FieldValue result)
        {
            Debug.Assert(result is ArrayValue);
            result.AsArrayValue.Sort((value1, value2) =>
                value1.QueryCompareTotalOrder(value2));
            return result;
        }

        protected internal override FieldValue GetInitialValue() =>
            new ArrayValue();

        protected internal CollectAggregatorBase(bool toSortResults)
        {
            this.toSortResults = toSortResults;
        }

        internal override long Aggregate(FieldValue value, bool countMemory)
        {
            if (value == FieldValue.Null || value == FieldValue.Empty)
            {
                return 0;
            }

            if (!(value is ArrayValue))
            {
                throw new InvalidOperationException(
                    "Query: input value for array_collect is not an " +
                    "ArrayValue");
            }

            long mem = 0;
            foreach (var elem in value.AsArrayValue)
            {
                Debug.Assert(elem != null);
                AddValue(elem);
                if (countMemory)
                {
                    mem += GetMemorySize(value.GetMemorySize());
                }
            }

            return mem;
        }

        internal override FieldValue Result =>
            toSortResults ? SortResults(base.Result) : base.Result;
    }

    internal class CollectAggregator : CollectAggregatorBase
    {
        protected internal override void AddValue(FieldValue value)
        {
            aggregateValue ??= GetInitialValue();
            aggregateValue.AsArrayValue.Add(value);
        }

        protected internal override long GetMemorySize(long valueSize) =>
            GetListEntrySize(valueSize);

        internal CollectAggregator(bool toSortResults) : base(toSortResults)
        {
        }
    }

    internal class CollectDistinctAggregator : CollectAggregatorBase,
        IEqualityComparer<FieldValue>
    {
        private readonly HashSet<FieldValue> valueSet =
            new HashSet<FieldValue>();

        protected internal override void AddValue(FieldValue value)
        {
            valueSet.Add(value);
        }

        protected internal override long GetMemorySize(long valueSize) =>
            GetHashSetEntrySize(valueSize);

        internal CollectDistinctAggregator(bool toSortResults) :
            base(toSortResults)
        {
        }

        internal override FieldValue Result
        {
            get
            {
                if (aggregateValue == null)
                {
                    aggregateValue = new ArrayValue(valueSet);
                    valueSet.Clear();
                }

                return base.Result;
            }
        }

        internal override long GetMemorySize() =>
            valueSet.Count * HashSetEntryOverhead + base.GetMemorySize();

        internal override void Clear()
        {
            base.Clear();
            valueSet.Clear();
        }

        public bool Equals(FieldValue value1, FieldValue value2)
        {
            Debug.Assert(value1 != null);
            return value1.QueryEquals(value2);
        }

        public int GetHashCode(FieldValue value)
        {
            Debug.Assert(value != null);
            return value.QueryHashCode();
        }
    }
}
