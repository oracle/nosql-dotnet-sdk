/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;

    internal enum DistributionKind
    {
        SinglePartition = 0,
        AllPartitions = 1,
        AllShards = 2
    }

    internal enum ArithmeticOpcode
    {
        AddSubtract = 14,
        MultiplyDivide = 15
    }

    internal enum SQLFuncCode
    {
        CountStar = 42,
        Count = 43,
        CountNumbers = 44,
        Sum = 45,
        Min = 47,
        Max = 48,
        ArrayCollect = 91,
        ArrayCollectDistinct = 92
    }

    internal struct ExpressionLocation
    {
        internal int StartLine { get; set; }

        internal int StartColumn { get; set; }

        internal int EndLine { get; set; }

        internal int EndColumn { get; set; }
    }

    internal readonly struct SortSpec
    {

        // Note: IsDescending will reverse the whole sorted order, including
        // special values, which happens after NullRank was applied. To
        // account for this, we reverse the value of NullRank if IsDescending
        // is true.
        
        internal SortSpec(string fieldName, bool isDescending,
            bool nullsFirst)
        {
            FieldName = fieldName;
            IsDescending = isDescending;
            NullRank = nullsFirst ^ IsDescending ? (sbyte)-1 : (sbyte)1;
        }

        internal string FieldName { get; }

        internal bool IsDescending { get; }

        internal sbyte NullRank { get; }

        internal bool NullsFirst => (NullRank == -1) ^ IsDescending;
    }

    internal abstract class PlanStep
    {
        internal abstract string Name { get; }

        internal int ResultPosition { get; set; }

        internal ExpressionLocation ExpressionLocation { get; set; }

        internal abstract bool IsAsync { get; }

        internal virtual PlanAsyncIterator CreateAsyncIterator(
            QueryRuntime runtime)
        {
            throw new InvalidOperationException(
                $"Cannot create async iterator for sync step {Name}");
        }

        internal virtual PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            throw new InvalidOperationException(
                $"Cannot create sync iterator for async step {Name}");
        }
    }

    internal abstract class PlanAsyncStep : PlanStep
    {
        internal override bool IsAsync => true;
    }

    internal abstract class PlanSyncStep : PlanStep
    {
        internal override bool IsAsync => false;
    }

    internal class SortStep : PlanAsyncStep
    {
        internal override string Name => "SORT";

        internal PlanStep InputStep { get; set; }

        internal SortSpec[] SortSpecs { get; set; }

        internal bool CountMemory { get; set; }

        internal override PlanAsyncIterator CreateAsyncIterator(
            QueryRuntime runtime)
        {
            return new SortIterator(runtime, this);
        }
    }

    internal class SFWStep : PlanAsyncStep
    {
        internal override string Name => "SFW";

        internal string[] ColumnNames { get; set; }

        internal int GroupColumnCount { get; set; }

        internal string FromVarName { get; set; }

        internal bool IsSelectStar { get; set; }

        internal PlanStep[] ColumnSteps { get; set; }

        internal PlanStep FromStep { get; set; }

        internal PlanStep OffsetStep { get; set; }

        internal PlanStep LimitStep { get; set; }

        internal override PlanAsyncIterator CreateAsyncIterator(
            QueryRuntime runtime)
        {
            return new SFWIterator(runtime, this);
        }
    }

    internal class ReceiveStep : PlanAsyncStep
    {
        internal override string Name => "RECV";

        internal DistributionKind DistributionKind { get; set; }

        internal SortSpec[] SortSpecs { get; set; }

        internal string[] PrimaryKeyFields { get; set; }

        internal override PlanAsyncIterator CreateAsyncIterator(
            QueryRuntime runtime)
        {
            return new ReceiveIterator(runtime, this);
        }
    }

    internal class ConstStep : PlanSyncStep
    {
        internal override string Name => "CONST";

        internal FieldValue Value { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new ConstIterator(runtime, this);
        }
    }

    internal class VarRefStep : PlanSyncStep
    {
        internal override string Name => "VAR_REF";

        internal string VarName { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new VarRefIterator(runtime, this);
        }
    }

    internal class ExtVarRefStep : PlanSyncStep
    {
        internal override string Name => "EXTERNAL_VAR_REF";

        internal string VarName { get; set; }

        internal int VarPosition { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new ExtVarRefIterator(runtime, this);
        }
    }

    internal class FieldStep : PlanSyncStep
    {
        internal override string Name => "FIELD_STEP";

        internal PlanStep InputStep { get; set; }

        internal string FieldName { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new FieldStepIterator(runtime, this);
        }
    }

    internal class ArithmeticOpStep : PlanSyncStep
    {
        internal override string Name => Opcode.ToString();

        internal ArithmeticOpcode Opcode { get; set; }

        internal PlanStep[] ArgSteps { get; set; }

        internal string OpSequence { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new ArithmeticOpIterator(runtime, this);
        }
    }

    internal abstract class AggregateFuncStep : PlanSyncStep
    {
        internal PlanStep InputStep { get; set; }
    }

    internal class FuncSumStep : AggregateFuncStep
    {
        internal override string Name => "FN_SUM";

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new FuncSumIterator(runtime, this);
        }
    }

    internal class FuncMinMaxStep : AggregateFuncStep
    {
        internal override string Name => IsMin ? "FN_MIN" : "FN_MAX";

        internal bool IsMin { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new FuncMinMaxIterator(runtime, this);
        }
    }

    internal class FuncCollectStep : AggregateFuncStep
    {
        internal override string Name =>
            "FN_ARRAY_COLLECT" + (IsDistinct ? "_DISTINCT" : "");

        internal bool IsDistinct { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new FuncCollectIterator(runtime, this);
        }
    }

    internal class FuncSizeStep : PlanSyncStep
    {
        internal override string Name => "FN_SIZE";

        internal PlanStep InputStep { get; set; }

        internal override PlanSyncIterator CreateSyncIterator(
            QueryRuntime runtime)
        {
            return new FuncSizeIterator(runtime, this);
        }
    }
    internal class GroupStep : PlanAsyncStep
    {
        internal override string Name => "GROUP";

        internal PlanStep InputStep { get; set; }

        internal int GroupingColumnCount { get; set; }

        internal string[] ColumnNames { get; set; }

        internal SQLFuncCode[] AggregateFuncCodes { get; set; }

        internal bool IsDistinct { get; set; }

        internal bool RemoveResult { get; set; }

        internal bool CountMemory { get; set; }

        internal override PlanAsyncIterator CreateAsyncIterator(
            QueryRuntime runtime)
        {
            return new GroupIterator(runtime, this);
        }
    }

}
