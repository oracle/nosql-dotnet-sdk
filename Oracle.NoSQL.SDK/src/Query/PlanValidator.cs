/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;
    using System.Diagnostics;

    internal static class PlanValidator
    {
        internal static void CheckNotNegative(int value, string name,
            PlanStep parent)
        {
            if (value < 0)
            {
                throw new BadProtocolException(
                    $"Query plan: received invalid value of {name} in " +
                    $"{parent.Name} step: {value}");
            }
        }

        internal static void CheckNotNull(object value, string name,
            PlanStep parent)
        {
            if (value == null)
            {
                throw new BadProtocolException(
                    $"Query plan: received null {name} in {parent.Name} step");
            }
        }

        internal static void CheckNotEmpty(Array value, string name,
            PlanStep parent)
        {
            CheckNotNull(value, name, parent);
            if (value.Length == 0)
            {
                throw new BadProtocolException(
                    $"Query plan: received empty array of {name} in " +
                    $"{parent.Name} step");
            }
        }

        internal static void CheckStepExists(PlanStep step, string name,
            PlanStep parent, int index = -1)
        {
            if (step == null)
            {
                var indexName = index >= 0 ? "" + index : "";
                throw new BadProtocolException(
                    $"Query plan: missing {name} step {indexName} " +
                    $"in {parent.Name} step");
            }
        }

        internal static void CheckStepIsSync(PlanStep step, string name,
            PlanStep parent, int index = -1)
        {
            CheckStepExists(step, name, parent, index);
            if (step.IsAsync)
            {
                var indexName = index >= 0 ? "" + index : "";
                throw new BadProtocolException(
                    $"Query plan: unexpected async step {step.Name} for " +
                    $"{name} step {indexName} in {parent.Name} step");
            }
        }

        // Note that some checks are performed by serializer in cases where
        // correctness is required for deserialization itself.

        private static void ValidateBase(PlanStep step)
        {
            CheckNotNegative(step.ResultPosition, "result position", step);
            CheckNotNegative(step.ExpressionLocation.StartLine,
                "expression location start line", step);
            CheckNotNegative(step.ExpressionLocation.StartColumn,
                "expression location start column", step);
            CheckNotNegative(step.ExpressionLocation.EndLine,
                "expression location end line", step);
            CheckNotNegative(step.ExpressionLocation.EndLine,
                "expression location end column", step);
        }

        internal static void ValidateSortStep(SortStep step)
        {
            ValidateBase(step);
            CheckStepExists(step.InputStep, "input", step);
        }

        internal static void ValidateSFWStep(SFWStep step)
        {
            ValidateBase(step);
            CheckStepExists(step.FromStep, "from", step);
            CheckNotEmpty(step.ColumnNames, "column names", step);
            CheckNotEmpty(step.ColumnSteps, "column steps", step);

            if (step.ColumnSteps.Length != step.ColumnNames.Length)
            {
                throw new BadProtocolException(
                    "Query plan: number of column steps" +
                    $"{step.ColumnSteps.Length} does not match " +
                    $"number of column names {step.ColumnNames.Length} in " +
                    $"{step.Name} step");
            }

            if (step.IsSelectStar && step.ColumnSteps.Length != 1)
            {
                throw new BadProtocolException(
                    "Query plan: multiple column steps for select * in " +
                    $"{step.Name} step");
            }

            if (step.GroupColumnCount > step.ColumnSteps.Length)
            {
                throw new BadProtocolException(
                    "Query plan: group column count " +
                    $"{step.GroupColumnCount} exceeds column count " +
                    $"{step.ColumnNames.Length} in {step.Name} step");
            }

            for (var i = 0; i < step.ColumnSteps.Length; i++)
            {
                CheckStepIsSync(step.ColumnSteps[i], "column", step, i);
            }

            if (step.OffsetStep != null)
            {
                CheckStepIsSync(step.OffsetStep, "offset", step);
            }

            if (step.LimitStep != null)
            {
                CheckStepIsSync(step.LimitStep, "limit", step);
            }
        }

        internal static void ValidateReceiveStep(ReceiveStep step)
        {
            ValidateBase(step);
            if (!Enum.IsDefined(typeof(DistributionKind),
                step.DistributionKind))
            {
                throw new BadProtocolException(
                    "Query plan: received invalid distribution kind: " +
                    $"{step.DistributionKind} for {step.Name} step");
            }

            if (step.SortSpecs != null && step.DistributionKind ==
                DistributionKind.SinglePartition)
            {
                throw new BadProtocolException(
                    "Query plan: received distribution kind " +
                    $"{step.DistributionKind} for sorting {step.Name} step");
            }
        }

        internal static void ValidateConstStep(ConstStep step)
        {
            ValidateBase(step);
            Debug.Assert(step.Value != null); // see ReadFieldValue
        }

        internal static void ValidateVarReferenceStep(VarRefStep step)
        {
            ValidateBase(step);
            CheckNotNull(step.VarName, "variable name", step);
        }

        internal static void ValidateExternalVarReferenceStep(
            ExtVarRefStep step)
        {
            ValidateBase(step);
            CheckNotNull(step.VarName, "variable name", step);
            CheckNotNegative(step.VarPosition, "variable position", step);
        }

        internal static void ValidateFieldStep(FieldStep step)
        {
            ValidateBase(step);
            CheckStepIsSync(step.InputStep, "input", step);
            CheckNotNull(step.FieldName, "field name", step);
        }

        internal static void ValidateArithmeticOpStep(ArithmeticOpStep step)
        {
            ValidateBase(step);
            if (!Enum.IsDefined(typeof(ArithmeticOpcode), step.Opcode))
            {
                throw new BadProtocolException(
                    "Query plan: received invalid arithmetic opcode: " +
                    $"{step.Opcode} for ${step.Name} step");
            }

            CheckNotNull(step.ArgSteps, "parameter steps", step);
            CheckNotNull(step.OpSequence, "operations sequence", step);
            if (step.ArgSteps.Length != step.OpSequence.Length)
            {
                throw new BadProtocolException(
                    "Query plan: received non-matching numbers of " +
                    $"parameters {step.ArgSteps.Length} and " +
                    $"operations {step.OpSequence.Length} for {step.Name} " +
                    "step");
            }

            for (var i = 0; i < step.ArgSteps.Length; i++)
            {
                CheckStepIsSync(step.ArgSteps[i], "parameter step",
                    step, i);
            }
        }

        internal static void ValidateFuncSumStep(FuncSumStep step)
        {
            ValidateBase(step);
            CheckStepIsSync(step.InputStep, "input", step);
        }

        internal static void ValidateFuncMinMaxStep(FuncMinMaxStep step)
        {
            ValidateBase(step);
            CheckStepIsSync(step.InputStep, "input", step);
        }

        internal static void ValidateFuncCollectStep(FuncCollectStep step)
        {
            ValidateBase(step);
            CheckStepIsSync(step.InputStep, "input", step);
        }

        internal static void ValidateFuncSizeStep(FuncSizeStep step)
        {
            ValidateBase(step);
            CheckStepIsSync(step.InputStep, "input", step);
        }

        internal static void ValidateGroupStep(GroupStep step)
        {
            ValidateBase(step);
            CheckStepExists(step.InputStep, "input", step);
        }

        internal static void ValidateUnionStep(UnionStep step)
        {
            ValidateBase(step);
            CheckNotEmpty(step.BranchSteps, "branch steps", step);
            for (var i = 0; i < step.BranchSteps.Length; i++)
            {
                CheckStepExists(step.BranchSteps[i], "branch", step, i);
                if (!step.BranchSteps[i].IsAsync)
                {
                    throw new BadProtocolException(
                        "Query plan: unexpected sync step " +
                        step.BranchSteps[i].Name +
                        " for UNION branch " + i);
                }
            }
        }

        internal static void ValidateArrayConstructorStep(
            ArrayConstructorStep step)
        {
            ValidateBase(step);
            // Java permits ARRAY_CONSTRUCTOR with no arguments, which is the
            // driver representation of an empty array literal.
            CheckNotNull(step.ArgSteps, "argument steps", step);
            for (var i = 0; i < step.ArgSteps.Length; i++)
            {
                CheckStepIsSync(step.ArgSteps[i], "argument", step, i);
            }
        }

        internal static void ValidateValueCompareStep(ValueCompareStep step)
        {
            ValidateBase(step);
            if (step.FuncCode < QueryFuncCode.Equal ||
                step.FuncCode > QueryFuncCode.LessOrEqual)
            {
                throw new BadProtocolException(
                    $"Query plan: invalid comparison function {step.FuncCode}");
            }
            CheckStepIsSync(step.LeftStep, "left operand", step);
            CheckStepIsSync(step.RightStep, "right operand", step);
        }

        internal static void ValidateAndOrStep(AndOrStep step)
        {
            ValidateBase(step);
            if (step.FuncCode != QueryFuncCode.And &&
                step.FuncCode != QueryFuncCode.Or)
            {
                throw new BadProtocolException(
                    $"Query plan: invalid logical function {step.FuncCode}");
            }
            CheckNotEmpty(step.ArgSteps, "argument steps", step);
            for (var i = 0; i < step.ArgSteps.Length; i++)
            {
                CheckStepIsSync(step.ArgSteps[i], "argument", step, i);
            }
        }

        internal static void ValidateCaseStep(CaseStep step)
        {
            ValidateBase(step);
            CheckNotEmpty(step.ConditionSteps, "condition steps", step);
            CheckNotEmpty(step.ThenSteps, "then steps", step);
            if (step.ConditionSteps.Length != step.ThenSteps.Length)
            {
                throw new BadProtocolException(
                    "Query plan: CASE has different numbers of conditions and then steps");
            }
            for (var i = 0; i < step.ConditionSteps.Length; i++)
            {
                CheckStepIsSync(step.ConditionSteps[i], "condition", step, i);
                CheckStepIsSync(step.ThenSteps[i], "then", step, i);
            }
            if (step.ElseStep != null)
            {
                CheckStepIsSync(step.ElseStep, "else", step);
            }
        }

        internal static void ValidateIsNullStep(IsNullStep step)
        {
            ValidateBase(step);
            if (step.FuncCode != QueryFuncCode.IsNull &&
                step.FuncCode != QueryFuncCode.IsNotNull)
            {
                throw new BadProtocolException(
                    $"Query plan: invalid null-check function {step.FuncCode}");
            }
            CheckStepIsSync(step.InputStep, "input", step);
        }

        internal static void ValidateSeqAggregateStep(SeqAggregateStep step)
        {
            ValidateBase(step);
            if (step.FuncCode < QueryFuncCode.SeqCount ||
                step.FuncCode > QueryFuncCode.SeqMaxIgnoreNulls)
            {
                throw new BadProtocolException(
                    $"Query plan: invalid sequence aggregate function {step.FuncCode}");
            }
            CheckStepIsSync(step.InputStep, "input", step);
        }

    }
}
