/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Diagnostics;

    internal abstract class AggregateFuncIterator : PlanSyncIterator
    {
        protected internal readonly PlanSyncIterator inputIterator;

        internal AggregateFuncIterator(QueryRuntime runtime,
            AggregateFuncStep step) : base(runtime)
        {
            inputIterator = step.InputStep.CreateSyncIterator(runtime);
        }

        // We don't use result register since aggregate function result is
        // never shared between iterators
        internal override FieldValue Result { get; set; }

        internal override void Reset(bool resetResult = false)
        {
            inputIterator.Reset(resetResult);
            if (resetResult)
            {
                Result = null;
            }
        }
    }

    internal class FuncMinMaxIterator : AggregateFuncIterator
    {
        private readonly FuncMinMaxStep step;

        internal FuncMinMaxIterator(QueryRuntime runtime,
            FuncMinMaxStep step) : base(runtime, step)
        {
            this.step = step;
        }

        internal override PlanStep Step => step;

        internal override bool Next()
        {
            for (;;)
            {
                if (!inputIterator.Next())
                {
                    return true;
                }

                var result = inputIterator.Result;
                Debug.Assert(result != null);

                if (result == FieldValue.Null || result == FieldValue.Empty)
                {
                    continue;
                }

                if (Result == null)
                {
                    Result = result;
                }
                else
                {
                    var compareResult = result.QueryCompare(Result);
                    if (step.IsMin)
                    {
                        if (compareResult < 0)
                        {
                            Result = result;
                        }
                    }
                    else if (compareResult > 0)
                    {
                        Result = result;
                    }
                }
            }

        }
    }

    internal class FuncSumIterator : AggregateFuncIterator
    {
        private readonly FuncSumStep step;

        internal FuncSumIterator(QueryRuntime runtime, FuncSumStep step) :
            base(runtime, step)
        {
            this.step = step;
        }

        internal override PlanStep Step => step;

        internal override bool Next()
        {
            for (;;)
            {
                if (!inputIterator.Next())
                {
                    return true;
                }

                var result = inputIterator.Result;
                Debug.Assert(result != null);

                if (result == FieldValue.Null || result == FieldValue.Empty)
                {
                    continue;
                }

                Result = Result == null ? result : Result.QueryAdd(result);
            }
        }
    }
}
