/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using static SizeOf;

    internal abstract class AggregateFuncIterator : PlanSyncIterator
    {
        protected internal readonly PlanSyncIterator inputIterator;

        protected internal abstract ValueAggregator Aggregator { get; }

        internal AggregateFuncIterator(QueryRuntime runtime,
            AggregateFuncStep step) : base(runtime)
        {
            inputIterator = step.InputStep.CreateSyncIterator(runtime);
        }

        // We don't use result register since aggregate function result is
        // never shared between iterators
        internal override FieldValue Result
        {
            get => Aggregator.Result;
            set => throw new NotSupportedException();
        }

        internal override bool Next()
        {
            for (;;)
            {
                if (!inputIterator.Next())
                {
                    return true;
                }

                Aggregator.Aggregate(inputIterator.Result);
            }
        }

        internal override void Reset(bool resetResult = false)
        {
            inputIterator.Reset(resetResult);
            if (resetResult)
            {
                Aggregator.Clear();
            }
        }
    }

    internal class FuncMinMaxIterator : AggregateFuncIterator
    {
        private readonly FuncMinMaxStep step;

        protected internal override ValueAggregator Aggregator { get; }

        internal FuncMinMaxIterator(QueryRuntime runtime,
            FuncMinMaxStep step) : base(runtime, step)
        {
            this.step = step;
            Aggregator = step.IsMin
                ? (ValueAggregator)new MinValueAggregator()
                : new MaxValueAggregator();
        }

        internal override PlanStep Step => step;
    }

    internal class FuncSumIterator : AggregateFuncIterator
    {
        private readonly FuncSumStep step;

        protected internal override ValueAggregator Aggregator { get; }

        internal FuncSumIterator(QueryRuntime runtime, FuncSumStep step) :
            base(runtime, step)
        {
            this.step = step;
            Aggregator = new SumAggregator();
        }

        internal override PlanStep Step => step;
    }

    internal class FuncCollectIterator : AggregateFuncIterator
    {
        private readonly FuncCollectStep step;
        private long memory;

        protected internal override ValueAggregator Aggregator { get; }

        internal FuncCollectIterator(QueryRuntime runtime,
            FuncCollectStep step) : base(runtime, step)
        {
            this.step = step;
            Aggregator = step.IsDistinct
                ? (ValueAggregator)new CollectDistinctAggregator(
                    runtime.IsForTest)
                : new CollectAggregator(runtime.IsForTest);
        }

        internal override PlanStep Step => step;

        internal override bool Next()
        {
            long mem = 0;
            for (;;)
            {
                if (!inputIterator.Next())
                {
                    break;
                }

                mem += Aggregator.Aggregate(inputIterator.Result, true);
            }

            memory += mem;
            runtime.TotalMemory += mem;

            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            if (resetResult)
            {
                runtime.TotalMemory -= memory;
                memory = 0;
            }
        }
    }

}
