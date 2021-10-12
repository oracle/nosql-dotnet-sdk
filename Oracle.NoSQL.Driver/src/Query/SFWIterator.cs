/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.Query {
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SFWIterator : PlanAsyncIterator
    {
        private readonly SFWStep step;
        private readonly PlanAsyncIterator fromIterator;
        private readonly PlanSyncIterator[] columnIterators;
        private long offset;
        private readonly long limit;
        private long resultCount;
        private FieldValue[] groupTuple;
        private bool lastGroupDone;

        internal SFWIterator(QueryRuntime runtime, SFWStep step) :
            base(runtime)
        {
            this.step = step;
            fromIterator = step.FromStep.CreateAsyncIterator(runtime);
            columnIterators = new PlanSyncIterator[step.ColumnSteps.Length];
            for (var i = 0; i < columnIterators.Length; i++)
            {
                columnIterators[i] = step.ColumnSteps[i].CreateSyncIterator(
                    runtime);
            }

            offset = GetOffset(step.OffsetStep?.CreateSyncIterator(runtime));
            limit = GetLimit(step.LimitStep?.CreateSyncIterator(runtime));
        }

        private long GetOffset(PlanSyncIterator iterator)
        {
            if (iterator == null)
            {
                return 0;
            }

            if (!iterator.Next())
            {
                throw new InvalidOperationException(
                    "Query: offset iterator in SFW has no results");
            }

            var result = iterator.Result.ToInt64();
            if (result < 0)
            {
                throw new ArgumentOutOfRangeException(GetMessageWithLocation(
                    "Offset cannot be negative"));
            }

            return result;
        }

        private long GetLimit(PlanSyncIterator iterator)
        {
            if (iterator == null)
            {
                return -1;
            }

            if (!iterator.Next())
            {
                throw new InvalidOperationException(
                    "Query: limit iterator in SFW has no results");
            }

            var result = iterator.Result.ToInt64();
            if (result < 0)
            {
                throw new ArgumentOutOfRangeException(GetMessageWithLocation(
                    "Limit cannot be negative"));
            }

            return result;
        }

        // Non-grouping next
        private async Task<bool> SimpleNextAsync(
            CancellationToken cancellationToken)
        {
            if (!await fromIterator.NextAsync(cancellationToken))
            {
                return false;
            }

            // Skip if offset has not been reached yet
            if (offset > 0)
            {
                return true;
            }

            // In case of selectStar this iterator shares result registry with
            // the 1st column iterator which will contain the result.
            if (step.IsSelectStar)
            {
                if (!columnIterators[0].Next())
                {
                    throw new InvalidOperationException();
                }

                columnIterators[0].Reset();
                return true;
            }

            var result = new RecordValue(columnIterators.Length);
            for (var i = 0; i < columnIterators.Length; i++)
            {
                var iterator = columnIterators[i];
                var hasResult = iterator.Next();
                // iterator.Next() may return false if this is for JSON field
                // and that field doesn't exist in the current record
                result[step.ColumnNames[i]] = hasResult ? iterator.Result :
                    FieldValue.Null;
                iterator.Reset();
            }

            Result = result;
            return true;
        }

        private static void AggregateColumn(PlanSyncIterator iterator)
        {
            if (!iterator.Next())
            {
                throw new InvalidOperationException(
                    iterator.GetMessageWithLocation(
                    "Aggregate column iterator reached end"));
            }

            iterator.Reset();
        }

        private void AggregateRow()
        {
            for (var i = step.GroupColumnCount;
                i < columnIterators.Length;
                i++)
            {
                AggregateColumn(columnIterators[i]);
            }
        }

        /*
         * This method checks whether the current input tuple (a) starts the
         * first group, i.e. it is the very 1st tuple in the input stream, or
         * (b) belongs to the current group, or (c) starts a new group
         * otherwise. The method returns true in case (c), indicating that an
         * output tuple is ready to be returned to the consumer of this SFW.
         * Otherwise, false is returned.
         */
        bool GroupInputRow()
        {
            int i;
            /*
             * If this is the very first input row, start the first group and
             * go back to compute next input row.
             */
            if (groupTuple == null)
            {
                groupTuple = new FieldValue[step.GroupColumnCount];
                for (i = 0; i < groupTuple.Length; i++)
                {
                    groupTuple[i] = columnIterators[i].Result;
                }
                AggregateRow();
                return false;
            }

            // Compare grouping columns for the current input row
            // with the current group row.
            for (i = 0; i < step.GroupColumnCount; i++)
            {
                if (!columnIterators[i].Result.QueryEquals(groupTuple[i]))
                {
                    break;
                }
            }

            // If the input row is in current group, update the aggregate
            // functions and go back to compute the next input row.
            if (i == step.GroupColumnCount)
            {
                AggregateRow();
                return false;
            }

            // Input row starts new group. We must finish up the current
            // group, produce result, and init the new group.
            var result = new RecordValue(columnIterators.Length);

            for (i = 0; i < groupTuple.Length; i++)
            {
                result[step.ColumnNames[i]] = groupTuple[i];
                // Init new grouping column values in groupTuple
                groupTuple[i] = columnIterators[i].Result;
            }

            for (; i < columnIterators.Length; i++)
            {
                var iterator = columnIterators[i];
                result[step.ColumnNames[i]] = iterator.Result;
                iterator.Reset(true);
                // Aggregate column values for the new row
                AggregateColumn(iterator);
            }

            Result = result;
            return true;
        }

        private bool ProduceLastGroup()
        {
            // Ignore last group if we haven't started grouping yet, execution
            // needs user continuation (group not ready yet) or if we haven't
            // skipped the offset yet

            if (groupTuple == null || runtime.NeedContinuation || offset > 0)
            {
                return false;
            }

            var result = new RecordValue(columnIterators.Length);
            int i;
            for (i = 0; i < groupTuple.Length; i++)
            {
                result[step.ColumnNames[i]] = groupTuple[i];
            }

            for (; i < columnIterators.Length; i++)
            {
                result[step.ColumnNames[i]] = columnIterators[i].Result;
            }

            Result = result;
            lastGroupDone = true;
            return true;
        }

        private async Task<bool> GroupingNextAsync(
            CancellationToken cancellationToken)
        {
            if (lastGroupDone)
            {
                return false;
            }

            for (;;)
            {
                if (!await fromIterator.NextAsync(cancellationToken))
                {
                    return ProduceLastGroup();
                }
                // Compute the expressions of grouping columns
                int i;
                for(i = 0; i < step.GroupColumnCount; i++)
                {
                    var iterator = columnIterators[i];
                    // iterator.Next() may return false if this is for JSON
                    // field and that field doesn't exist in the current
                    // record
                    if (!iterator.Next())
                    {
                        iterator.Reset();
                        break;
                    }

                    iterator.Reset();
                }

                // Skip records with non-existing JSON fields in the group by
                // columns
                if (i < step.GroupColumnCount)
                {
                    continue;
                }

                if (GroupInputRow())
                {
                    return true;
                }
            }
        }

        internal override PlanStep Step => step;

        internal override async Task<bool> NextAsync(
            CancellationToken cancellationToken)
        {
            if (limit >= 0 && resultCount == limit)
            {
                return false;
            }

            // Loop to skip offset results
            for (;;)
            {
                if (!await (step.GroupColumnCount < 0
                    ? SimpleNextAsync(cancellationToken)
                    : GroupingNextAsync(cancellationToken)))
                {
                    return false;
                }

                if (offset == 0)
                {
                    resultCount++;
                    return true;
                }

                offset--;
            }
        }

    }
}
