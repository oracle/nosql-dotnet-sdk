/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query.BinaryProtocol
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Query;
    using static SDK.BinaryProtocol.Protocol;
    using static PlanValidator;

    internal static class PlanSerializer
    {
        private enum StepType : sbyte
        {
            None = -1,
            Recv = 17,
            SFW = 14,
            Sort = 47,
            Const = 0,
            VarRef = 1,
            ExternalVarRef = 2,
            ArrayConstructor = 3,
            ValueCompare = 5,
            AndOr = 7,
            FieldStep = 11,
            ArithOp = 8,
            FnSize = 15,
            Case = 19,
            IsNull = 26,
            FnSum = 39,
            FnMinMax = 41,
            Group = 65,
            Sort2 = 66,
            FnCollect = 78,
            SeqAggr = 48,
            Union = 90
        }

        private static void DeserializeBase(MemoryStream stream,
            PlanStep step)
        {
            step.ResultPosition = ReadUnpackedInt32(stream);
            stream.Seek(4, SeekOrigin.Current); // State position, not used
            step.ExpressionLocation = new ExpressionLocation
            {
                StartLine = ReadUnpackedInt32(stream),
                StartColumn = ReadUnpackedInt32(stream),
                EndLine = ReadUnpackedInt32(stream),
                EndColumn = ReadUnpackedInt32(stream)
            };
        }

        private static SortSpec[] DeserializeSortSpecs(MemoryStream stream,
            PlanStep parent)
        {
            var fields = ReadStringArray(stream);
            var fieldCount = fields?.Length ?? 0;
            var specs = ReadArray<(bool isDesc, bool nullsFirst)>(
                stream,
                memoryStream => (ReadBoolean(memoryStream),
                    ReadBoolean(memoryStream)));

            var specCount = specs?.Length ?? 0;
            var isUnion = parent is UnionStep;
            // Java UNION uses the sort-key array to select its execution
            // mode. Grouped UNION plans can carry extra attributes (one per
            // branch), including when there are zero grouping keys. Consume
            // all attributes but use only those corresponding to actual keys.
            if (isUnion && fields == null)
            {
                return null;
            }
            if (isUnion ? fieldCount > specCount : fieldCount != specCount)
            {
                throw new BadProtocolException(
                    "Query plan: received non-matching number of " +
                    $"sort fields {fieldCount} and " +
                    $"sort attributes {specCount} in {parent.Name} step");
            }

            if (fieldCount == 0)
            {
                // An empty key array still selects sorted UNION execution;
                // only a null key array means sequential execution.
                return isUnion ? Array.Empty<SortSpec>() : null;
            }

            Debug.Assert(fields != null && specs != null);

            var sortSpecs = new SortSpec[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                sortSpecs[i] = new SortSpec(fields[i], specs[i].isDesc,
                    specs[i].nullsFirst);
            }

            return sortSpecs;
        }

        private static SQLFuncCode DeserializeSQLFuncCode(MemoryStream stream,
            PlanStep parent)
        {
            var val = ReadUnpackedInt16(stream);
            if (!Enum.IsDefined(typeof(SQLFuncCode), (int)val))
            {
                throw new BadProtocolException(
                    $"Query plan: received invalid function code: {val} " +
                    $"in {parent.Name} step");
            }

            return (SQLFuncCode)val;
        }

        private static QueryFuncCode DeserializeQueryFuncCode(
            MemoryStream stream, PlanStep parent)
        {
            var value = ReadUnpackedInt16(stream);
            if (!Enum.IsDefined(typeof(QueryFuncCode), (int)value))
            {
                throw new BadProtocolException(
                    $"Query plan: received invalid function code: {value} " +
                    $"in {parent.Name} step");
            }
            return (QueryFuncCode)value;
        }

        private static SortStep DeserializeSortStep(MemoryStream stream,
            StepType stepType, short queryVersion)
        {
            var step = new SortStep();
            DeserializeBase(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            step.SortSpecs = DeserializeSortSpecs(stream, step);
            step.CountMemory = stepType != StepType.Sort2 ||
                ReadBoolean(stream);
            ValidateSortStep(step);
            return step;
        }

        private static SFWStep DeserializeSFWStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new SFWStep();
            DeserializeBase(stream, step);
            step.ColumnNames = ReadStringArray(stream);
            step.GroupColumnCount = ReadUnpackedInt32(stream);
            step.FromVarName = ReadString(stream);
            step.IsSelectStar = ReadBoolean(stream);
            step.ColumnSteps = DeserializeMultipleSteps(stream, queryVersion);
            step.FromStep = DeserializeStep(stream, queryVersion);
            step.OffsetStep = DeserializeStep(stream, queryVersion);
            step.LimitStep = DeserializeStep(stream, queryVersion);
            ValidateSFWStep(step);
            return step;
        }

        private static ReceiveStep DeserializeReceiveStep(MemoryStream stream)
        {
            var step = new ReceiveStep();
            DeserializeBase(stream, step);
            step.DistributionKind = (DistributionKind)ReadUnpackedInt16(
                stream);
            step.SortSpecs = DeserializeSortSpecs(stream, step);
            step.PrimaryKeyFields = ReadStringArray(stream);
            ValidateReceiveStep(step);
            return step;
        }

        private static ConstStep DeserializeConstStep(MemoryStream stream)
        {
            var step = new ConstStep();
            DeserializeBase(stream, step);
            step.Value = ReadFieldValue(stream);
            ValidateConstStep(step);
            return step;
        }

        private static VarRefStep DeserializeVarRefStep(
            MemoryStream stream)
        {
            var step = new VarRefStep();
            DeserializeBase(stream, step);
            step.VarName = ReadString(stream);
            ValidateVarReferenceStep(step);
            return step;
        }

        private static ExtVarRefStep DeserializeExtVarRefStep(
            MemoryStream stream)
        {
            var step = new ExtVarRefStep();
            DeserializeBase(stream, step);
            step.VarName = ReadString(stream);
            step.VarPosition = ReadUnpackedInt32(stream);
            ValidateExternalVarReferenceStep(step);
            return step;
        }

        private static FieldStep DeserializeFieldStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new FieldStep();
            DeserializeBase(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            step.FieldName = ReadString(stream);
            ValidateFieldStep(step);
            return step;
        }

        private static ArithmeticOpStep DeserializeArithmeticStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new ArithmeticOpStep();
            DeserializeBase(stream, step);
            step.Opcode = (ArithmeticOpcode)ReadUnpackedInt16(stream);
            step.ArgSteps = DeserializeMultipleSteps(stream, queryVersion);
            step.OpSequence = ReadString(stream);
            ValidateArithmeticOpStep(step);
            return step;
        }

        private static FuncSumStep DeserializeFuncSumStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new FuncSumStep();
            DeserializeBase(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateFuncSumStep(step);
            return step;
        }

        private static FuncMinMaxStep DeserializeFuncMinMaxStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new FuncMinMaxStep();
            DeserializeBase(stream, step);
            var code = DeserializeSQLFuncCode(stream, step);
            step.IsMin = code == SQLFuncCode.Min;
            if (!step.IsMin && code != SQLFuncCode.Max)
            {
                throw new BadProtocolException(
                    "Query plan: received invalid sql function code for " +
                    $"min/max operation: {code}");
            }

            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateFuncMinMaxStep(step);
            return step;
        }

        private static FuncSizeStep DeserializeFuncSizeStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new FuncSizeStep();
            DeserializeBase(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateFuncSizeStep(step);
            return step;
        }

        private static FuncCollectStep DeserializeFuncCollectStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new FuncCollectStep();
            DeserializeBase(stream, step);
            step.IsDistinct = ReadBoolean(stream);
            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateFuncCollectStep(step);
            return step;
        }

        private static GroupStep DeserializeGroupStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new GroupStep();
            DeserializeBase(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            step.GroupingColumnCount = ReadUnpackedInt32(stream);
            CheckNotNegative(step.GroupingColumnCount,
                "group by column count", step);
            step.ColumnNames = ReadStringArray(stream);
            CheckNotEmpty(step.ColumnNames, "column names", step);
            var aggregateCount = step.ColumnNames.Length -
                                 step.GroupingColumnCount;
            if (aggregateCount < 0)
            {
                throw new BadProtocolException(
                    "Query plan: received group by column count " +
                    $"{step.GroupingColumnCount} that exceeds total column " +
                    $"count {step.ColumnNames.Length}");
            }

            if (aggregateCount != 0)
            {
                step.AggregateFuncCodes = new SQLFuncCode[aggregateCount];
                for (var i = 0; i < aggregateCount; i++)
                {
                    step.AggregateFuncCodes[i] = DeserializeSQLFuncCode(stream, step);
                }
            }

            step.IsDistinct = ReadBoolean(stream);
            step.RemoveResult = ReadBoolean(stream);
            step.CountMemory = ReadBoolean(stream);
            // Before V6 the regrouping flag was not sent. Preserve the
            // existing C# behavior for those proxy partial array results.
            step.IsRegrouping = queryVersion < QueryRequestBase.QueryV6 ||
                ReadBoolean(stream);
            ValidateGroupStep(step);
            return step;
        }

        private static UnionStep DeserializeUnionStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new UnionStep();
            DeserializeBase(stream, step);
            step.BranchSteps = DeserializeMultipleSteps(stream, queryVersion);
            step.SortSpecs = DeserializeSortSpecs(stream, step);
            ValidateUnionStep(step);
            return step;
        }

        private static ArrayConstructorStep DeserializeArrayConstructorStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new ArrayConstructorStep();
            DeserializeBase(stream, step);
            step.IsConditional = ReadBoolean(stream);
            step.ArgSteps = DeserializeMultipleSteps(stream, queryVersion);
            ValidateArrayConstructorStep(step);
            return step;
        }

        private static ValueCompareStep DeserializeValueCompareStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new ValueCompareStep();
            DeserializeBase(stream, step);
            step.FuncCode = DeserializeQueryFuncCode(stream, step);
            step.LeftStep = DeserializeStep(stream, queryVersion);
            step.RightStep = DeserializeStep(stream, queryVersion);
            ValidateValueCompareStep(step);
            return step;
        }

        private static AndOrStep DeserializeAndOrStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new AndOrStep();
            DeserializeBase(stream, step);
            step.FuncCode = DeserializeQueryFuncCode(stream, step);
            step.ArgSteps = DeserializeMultipleSteps(stream, queryVersion);
            ValidateAndOrStep(step);
            return step;
        }

        private static CaseStep DeserializeCaseStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new CaseStep();
            DeserializeBase(stream, step);
            step.ConditionSteps = DeserializeMultipleSteps(stream, queryVersion);
            step.ThenSteps = DeserializeMultipleSteps(stream, queryVersion);
            step.ElseStep = DeserializeStep(stream, queryVersion);
            ValidateCaseStep(step);
            return step;
        }

        private static IsNullStep DeserializeIsNullStep(MemoryStream stream,
            short queryVersion)
        {
            var step = new IsNullStep();
            DeserializeBase(stream, step);
            step.FuncCode = DeserializeQueryFuncCode(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateIsNullStep(step);
            return step;
        }

        private static SeqAggregateStep DeserializeSeqAggregateStep(
            MemoryStream stream, short queryVersion)
        {
            var step = new SeqAggregateStep();
            DeserializeBase(stream, step);
            step.FuncCode = DeserializeQueryFuncCode(stream, step);
            step.InputStep = DeserializeStep(stream, queryVersion);
            ValidateSeqAggregateStep(step);
            return step;
        }

        private static PlanStep[] DeserializeMultipleSteps(MemoryStream stream,
            short queryVersion)
        {
            return ReadArray(stream, s => DeserializeStep(s, queryVersion));
        }

        internal static PlanStep DeserializeStep(MemoryStream stream,
            short queryVersion = QueryRequestBase.QueryV3)
        {
            var stepType = (StepType)ReadByte(stream);
            switch (stepType)
            {
                case StepType.None:
                    return null;
                case StepType.Sort: case StepType.Sort2:
                    return DeserializeSortStep(stream, stepType, queryVersion);
                case StepType.SFW:
                    return DeserializeSFWStep(stream, queryVersion);
                case StepType.Recv:
                    return DeserializeReceiveStep(stream);
                case StepType.Const:
                    return DeserializeConstStep(stream);
                case StepType.VarRef:
                    return DeserializeVarRefStep(stream);
                case StepType.ExternalVarRef:
                    return DeserializeExtVarRefStep(stream);
                case StepType.ArrayConstructor:
                    return DeserializeArrayConstructorStep(stream, queryVersion);
                case StepType.ValueCompare:
                    return DeserializeValueCompareStep(stream, queryVersion);
                case StepType.AndOr:
                    return DeserializeAndOrStep(stream, queryVersion);
                case StepType.FieldStep:
                    return DeserializeFieldStep(stream, queryVersion);
                case StepType.ArithOp:
                    return DeserializeArithmeticStep(stream, queryVersion);
                case StepType.FnSum:
                    return DeserializeFuncSumStep(stream, queryVersion);
                case StepType.FnMinMax:
                    return DeserializeFuncMinMaxStep(stream, queryVersion);
                case StepType.FnSize:
                    return DeserializeFuncSizeStep(stream, queryVersion);
                case StepType.Case:
                    return DeserializeCaseStep(stream, queryVersion);
                case StepType.IsNull:
                    return DeserializeIsNullStep(stream, queryVersion);
                case StepType.FnCollect:
                    return DeserializeFuncCollectStep(stream, queryVersion);
                case StepType.Group:
                    return DeserializeGroupStep(stream, queryVersion);
                case StepType.Union:
                    return DeserializeUnionStep(stream, queryVersion);
                case StepType.SeqAggr:
                    return DeserializeSeqAggregateStep(stream, queryVersion);
                default:
                    throw new BadProtocolException(
                        "Query plan: received invalid or unsupported step " +
                        $"type: {stepType}");
            }
        }

    }

}
