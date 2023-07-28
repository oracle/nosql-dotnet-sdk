/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;
    using static SizeOf;

    internal class SortIterator : PlanAsyncIterator, IComparer<RecordValue>
    {
        private readonly SortStep step;
        private readonly PlanAsyncIterator inputIterator;
        private List<RecordValue> rows;
        private int rowIndex = -1;

        internal SortIterator(QueryRuntime runtime, SortStep step) :
            base(runtime)
        {
            this.step = step;
            inputIterator = step.InputStep.CreateAsyncIterator(runtime);
            rows = new List<RecordValue>();
        }

        public int Compare(RecordValue value1, RecordValue value2)
        {
            return CompareRows(value1, value2, step.SortSpecs);
        }

        internal override async Task<bool> NextAsync(
            CancellationToken cancellationToken)
        {
            if (rowIndex == -1)
            {
                while (await inputIterator.NextAsync(cancellationToken))
                {
                    if (!(inputIterator.Result is RecordValue row))
                    {
                        throw new InvalidOperationException(
                            "Input to sort step is not a record value: " +
                            inputIterator.Result);
                    }
                    rows.Add(row);
                    if (step.CountMemory)
                    {
                        runtime.TotalMemory +=
                            GetListEntrySize(row.GetMemorySize());
                    }
                }

                if (runtime.NeedContinuation)
                {
                    return false;
                }

                // OrderBy performs a stable sort.
                // Note that we could avoid creating additional list by
                // iterating via IEnumerator but then we would lose the
                // ability to release row memory as we iterate (see below).
                rows = rows.OrderBy(row => row, this).ToList();
                rowIndex = 0;
            }

            if (rowIndex < rows.Count)
            {
                var result = rows[rowIndex];
                ConvertEmptyToNull(result);
                rows[rowIndex++] = null; // Release memory for the row
                Result = result;
                return true;
            }

            return false;
        }

        internal override PlanStep Step => step;

    }

}
