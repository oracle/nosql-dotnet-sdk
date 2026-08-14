/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;

    internal class UnionIterator : PlanAsyncIterator
    {
        private sealed class Branch
        {
            internal PlanAsyncIterator Iterator { get; }
            internal RecordValue Current { get; set; }
            internal bool IsDone { get; set; }
            internal bool IsStarted { get; set; }

            internal Branch(PlanAsyncIterator iterator)
            {
                Iterator = iterator;
            }
        }

        private readonly UnionStep step;
        private readonly Branch[] branches;
        private int currentBranch;

        internal UnionIterator(QueryRuntime runtime, UnionStep step) :
            base(runtime)
        {
            this.step = step;
            if (runtime.PreparedStatement.QueryBranches.Count !=
                step.BranchSteps.Length)
            {
                throw new BadProtocolException(
                    "Query: number of UNION plan branches does not match " +
                    "the number of proxy prepared statements");
            }

            branches = new Branch[step.BranchSteps.Length];
            for (var i = 0; i < branches.Length; i++)
            {
                branches[i] = new Branch(step.BranchSteps[i]
                    .CreateAsyncIterator(runtime));
            }
        }

        private async Task<bool> FetchBranchAsync(int branch,
            CancellationToken cancellationToken)
        {
            var state = branches[branch];
            if (state.IsDone || state.Current != null)
            {
                return state.Current != null;
            }

            var wasStarted = state.IsStarted;
            state.IsStarted = true;
            runtime.UnionBranch = branch;
            if (!await state.Iterator.NextAsync(cancellationToken))
            {
                // ReceiveIterator intentionally returns false without setting
                // NeedContinuation when a second, not-yet-started iterator
                // is blocked by this batch's one-server-fetch limit. For a
                // UNION branch that means defer the branch, not completion.
                if (!wasStarted && runtime.FetchDone &&
                    !runtime.NeedContinuation)
                {
                    runtime.NeedContinuation = true;
                    return false;
                }

                if (!runtime.NeedContinuation)
                {
                    state.IsDone = true;
                }
                return false;
            }

            if (!(state.Iterator.Result is RecordValue row))
            {
                throw new InvalidOperationException(
                    "Query: UNION branch result is not a record value: " +
                    state.Iterator.Result);
            }

            state.Current = row;
            return true;
        }

        private async Task<bool> NextSequentialAsync(
            CancellationToken cancellationToken)
        {
            while (currentBranch < branches.Length)
            {
                if (await FetchBranchAsync(currentBranch, cancellationToken))
                {
                    Result = branches[currentBranch].Current;
                    branches[currentBranch].Current = null;
                    return true;
                }

                if (runtime.NeedContinuation)
                {
                    return false;
                }

                ++currentBranch;
                if (currentBranch < branches.Length)
                {
                    runtime.UnionBranch = currentBranch;
                    runtime.NeedContinuation = true;
                    return false;
                }
            }

            return false;
        }

        private async Task<bool> NextSortedAsync(
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < branches.Length; i++)
            {
                if (!branches[i].IsDone && branches[i].Current == null)
                {
                    await FetchBranchAsync(i, cancellationToken);
                    if (runtime.NeedContinuation)
                    {
                        return false;
                    }
                }
            }

            var selected = -1;
            for (var i = 0; i < branches.Length; i++)
            {
                var row = branches[i].Current;
                if (row == null)
                {
                    continue;
                }

                if (selected < 0 || CompareRows(row,
                        branches[selected].Current, step.SortSpecs) < 0)
                {
                    selected = i;
                }
            }

            if (selected < 0)
            {
                return false;
            }

            runtime.UnionBranch = selected;
            Result = branches[selected].Current;
            branches[selected].Current = null;
            return true;
        }

        internal override async Task<bool> NextAsync(
            CancellationToken cancellationToken)
        {
            return step.SortSpecs == null
                ? await NextSequentialAsync(cancellationToken)
                : await NextSortedAsync(cancellationToken);
        }

        internal override PlanStep Step => step;
    }
}
