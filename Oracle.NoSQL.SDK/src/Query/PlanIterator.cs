/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class PlanIterator
    {
        private protected QueryRuntime runtime;

        private protected PlanIterator(QueryRuntime runtime)
        {
            this.runtime = runtime;
        }

        internal ExpressionLocation ExpressionLocation { get; set; }

        internal abstract PlanStep Step { get; }

        internal virtual FieldValue Result
        {
            get => runtime.ResultRegistry[Step.ResultPosition];

            set => runtime.ResultRegistry[Step.ResultPosition] = value;
        }

        internal string GetMessageWithLocation(string message) =>
            runtime.GetMessageWithLocation(message, Step.ExpressionLocation);

        internal virtual void Reset(bool resetResult = false)
        {
        }

        [Conditional("DEBUG")]
        internal void Trace(string message, int level = 0)
        {
            runtime.Trace($"[{Step.Name}] {message}", level);
        }
    }

    internal abstract class PlanSyncIterator : PlanIterator
    {
        private protected PlanSyncIterator(QueryRuntime runtime) :
            base(runtime)
        {
        }

        internal abstract bool Next();
    }

    internal abstract class PlanAsyncIterator : PlanIterator
    {
        private protected PlanAsyncIterator(QueryRuntime runtime) :
            base(runtime)
        {
        }

        internal abstract Task<bool> NextAsync(
            CancellationToken cancellationToken);
    }

}
