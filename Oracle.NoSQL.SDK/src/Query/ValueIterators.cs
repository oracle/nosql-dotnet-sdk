/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Diagnostics;

    internal abstract class OneResultIterator : PlanSyncIterator
    {
        protected internal bool done;

        internal OneResultIterator(QueryRuntime runtime) : base(runtime)
        {
        }

        internal override bool Next()
        {
            return !done && (done = true);
        }

        internal override void Reset(bool resetResult = false)
        {
            done = false;
        }
    }

    /**
     * ConstIterator represents a reference to a constant value in the query.
     * Such a reference will need to be "executed" at the driver side when
     * the constant appears in the OFFSET or LIMIT clause.
     */
    internal class ConstIterator : OneResultIterator
    {
        private readonly ConstStep step;

        internal ConstIterator(QueryRuntime runtime, ConstStep step) :
            base(runtime)
        {
            this.step = step;
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            Result = step.Value;
            done = true;
            return true;
        }

        internal override PlanStep Step => step;
    }

    /**
     * VarRefIterator represents a reference to a non-external variable in
     * the query. It simply returns the value that the variable is currently
     * bound to. This value is computed by the variable's "domain iterator"
     * (the iterator that evaluates the domain expression of the variable).
     * The domain iterator stores the value in theResultReg of this
     * VarRefIterator.
     * In the context of the driver, an implicit internal variable is used
     * to represent the results arriving from the proxy. All other expressions
     * that are computed at the driver operate on these results, so all such
     * expressions reference this variable. This is analogous to the internal
     * variable used in kvstore to represent the table alias in the FROM
     * clause.
     */
    class VarRefIterator : OneResultIterator
    {
        private readonly VarRefStep step;

        internal VarRefIterator(QueryRuntime runtime, VarRefStep step) :
            base(runtime)
        {
            this.step = step;
        }

        internal override PlanStep Step => step;

        // The value is already stored in domain iterator registry
    }

    /**
     * In general, ExternalVarRefIterator represents a reference to an
     * external variable in the query. Such a reference will need to be
     * "executed" at the driver side when the variable appears in the OFFSET
     * or LIMIT clause. ExternalVarRefIterator simply returns the value that
     * the variable is currently bound to. This value is set by the app via
     * the methods of PreparedStatement.
     */
    internal class ExtVarRefIterator : OneResultIterator
    {
        private readonly ExtVarRefStep step;

        internal ExtVarRefIterator(QueryRuntime runtime, ExtVarRefStep step) :
            base(runtime)
        {
            this.step = step;
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            var value = runtime.GetExtVariable(step.VarPosition);
            // This is checked in QueryRuntime.InitExternalVariables()
            Debug.Assert(value != null);
            Result = value;
            done = true;
            return true;
        }

        internal override PlanStep Step => step;
    }


    /**
     * FieldStepIterator returns the value of a field in an input MapValue.
     * It is used by the driver to implement column references in the SELECT
     * list (see SFWIterator).
     */
    internal class FieldStepIterator : PlanSyncIterator
    {
        private readonly FieldStep step;
        private readonly PlanSyncIterator inputIterator;

        internal FieldStepIterator(QueryRuntime runtime, FieldStep step) :
            base(runtime)
        {
            this.step = step;
            inputIterator = step.InputStep.CreateSyncIterator(runtime);
        }

        internal override bool Next()
        {
            if (!inputIterator.Next())
            {
                return false;
            }

            var result = inputIterator.Result;
            if (!(result is MapValue mapValue))
            {
                throw new InvalidOperationException(
                    "Query: input value in field step is not " +
                    "RecordValue or MapValue");
            }

            if (!mapValue.TryGetValue(step.FieldName, out result) ||
                result == FieldValue.Empty)
            {
                return false;
            }

            Result = result;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            inputIterator.Reset();
        }

        internal override PlanStep Step => step;
    }

}
