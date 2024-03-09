/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class PlanIterator
    {
        protected internal QueryRuntime runtime;

        protected internal PlanIterator(QueryRuntime runtime)
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

        protected internal string GetMessageWithLocation(string message) =>
            runtime.GetMessageWithLocation(message, Step.ExpressionLocation);

        internal virtual void Reset(bool resetResult = false)
        {
        }
    }

    internal abstract class PlanSyncIterator : PlanIterator
    {
        protected internal PlanSyncIterator(QueryRuntime runtime) :
            base(runtime)
        {
        }

        internal abstract bool Next();
    }

    internal abstract class PlanAsyncIterator : PlanIterator
    {
        protected internal PlanAsyncIterator(QueryRuntime runtime) :
            base(runtime)
        {
        }

        internal abstract Task<bool> NextAsync(
            CancellationToken cancellationToken);
    }

}
