/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System.Text;

    // Produces the Java-compatible driver-plan text used by ALL-profile query
    // statistics. The plan is already present in PreparedStatement, so
    // formatting it does not add a service request or alter query execution.
    internal static class PlanFormatter
    {
        internal static string Format(PlanStep step)
        {
            if (step == null)
            {
                return null;
            }

            var builder = new StringBuilder();
            AppendStep(builder, step, 0);
            return builder.ToString();
        }

        private static void AppendStep(StringBuilder builder, PlanStep step,
            int indent)
        {
            // Java renders variable references as single-line iterators rather
            // than using the standard name/[content] block.
            if (step is VarRefStep variable)
            {
                Indent(builder, indent);
                builder.Append("VAR_REF(").Append(variable.VarName)
                    .Append(")([").Append(step.ResultPosition).Append("])");
                return;
            }

            if (step is ExtVarRefStep external)
            {
                Indent(builder, indent);
                // EXTENAL is intentionally spelled as in Java's driver-plan
                // format, which is the compatibility contract for this text.
                builder.Append("EXTENAL_VAR_REF(").Append(external.VarName)
                    .Append(", ").Append(external.VarPosition).Append(")([")
                    .Append(step.ResultPosition).Append("])");
                return;
            }

            Indent(builder, indent);
            builder.Append(GetStepName(step)).Append("([")
                .Append(step.ResultPosition).Append("])\n");
            Indent(builder, indent);
            builder.Append("[\n");
            AppendContent(builder, step, indent + 2);
            builder.Append('\n');
            Indent(builder, indent);
            builder.Append(']');
        }

        private static string GetStepName(PlanStep step)
        {
            if (step is ArithmeticOpStep arithmetic)
            {
                return arithmetic.Opcode == ArithmeticOpcode.AddSubtract ?
                    "OP_ADD_SUB" : "OP_MULT_DIV";
            }

            // Java names this iterator FN_COLLECT and emits distinctness in
            // its content instead of encoding it in the iterator name.
            return step is FuncCollectStep ? "FN_COLLECT" : step.Name;
        }

        private static void AppendContent(StringBuilder builder, PlanStep step,
            int indent)
        {
            switch (step)
            {
                case SortStep sort:
                    AppendStep(builder, sort.InputStep, indent);
                    if (sort.SortSpecs != null && sort.SortSpecs.Length > 0)
                    {
                        builder.Append('\n');
                        AppendSortFields(builder, sort.SortSpecs, indent);
                    }
                    break;
                case SFWStep sfw:
                    AppendSFW(builder, sfw, indent);
                    break;
                case ReceiveStep receive:
                    AppendReceive(builder, receive, indent);
                    break;
                case ConstStep constant:
                    Indent(builder, indent);
                    builder.Append(constant.Value.ToJsonString());
                    break;
                case FieldStep field:
                    AppendStep(builder, field.InputStep, indent);
                    builder.Append(",\n");
                    Indent(builder, indent);
                    builder.Append(field.FieldName);
                    break;
                case ArithmeticOpStep arithmetic:
                    AppendArithmetic(builder, arithmetic, indent);
                    break;
                case FuncCollectStep collect:
                    Indent(builder, indent);
                    builder.Append("\"distinct\" : ")
                        .Append(collect.IsDistinct ? "true" : "false")
                        .Append(",\n");
                    AppendStep(builder, collect.InputStep, indent);
                    break;
                case AggregateFuncStep aggregate:
                    AppendStep(builder, aggregate.InputStep, indent);
                    break;
                case FuncSizeStep size:
                    AppendStep(builder, size.InputStep, indent);
                    break;
                case GroupStep group:
                    AppendGroupStep(builder, group, indent);
                    break;
                case UnionStep union:
                    for (var i = 0; i < union.BranchSteps.Length; i++)
                    {
                        AppendStep(builder, union.BranchSteps[i], indent);
                        if (i < union.BranchSteps.Length - 1)
                        {
                            builder.Append(",\n");
                        }
                    }
                    break;
            }
        }

        private static void AppendSFW(StringBuilder builder, SFWStep sfw,
            int indent)
        {
            Indent(builder, indent);
            builder.Append("FROM:\n");
            AppendStep(builder, sfw.FromStep, indent);
            if (!string.IsNullOrEmpty(sfw.FromVarName))
            {
                builder.Append(" as ").Append(sfw.FromVarName);
            }
            builder.Append("\n\n");

            AppendGroupBy(builder, sfw.GroupColumnCount, indent);

            Indent(builder, indent);
            builder.Append("SELECT:\n");
            if (sfw.ColumnSteps != null)
            {
                for (var index = 0; index < sfw.ColumnSteps.Length; index++)
                {
                    AppendStep(builder, sfw.ColumnSteps[index], indent);
                    if (index < sfw.ColumnSteps.Length - 1)
                    {
                        builder.Append(",\n");
                    }
                }
            }

            AppendOptionalStep(builder, "OFFSET", sfw.OffsetStep, indent);
            AppendOptionalStep(builder, "LIMIT", sfw.LimitStep, indent);
        }

        private static void AppendReceive(StringBuilder builder,
            ReceiveStep receive, int indent)
        {
            Indent(builder, indent);
            builder.Append("DistributionKind : ")
                .Append(GetDistributionName(receive.DistributionKind))
                .Append(",\n");
            AppendSortFields(builder, receive.SortSpecs, indent);
            AppendStringValues(builder, "Primary Key Fields",
                receive.PrimaryKeyFields, indent);
        }

        private static string GetDistributionName(DistributionKind kind)
        {
            switch (kind)
            {
                case DistributionKind.SinglePartition:
                    return "SINGLE_PARTITION";
                case DistributionKind.AllPartitions:
                    return "ALL_PARTITIONS";
                case DistributionKind.AllShards:
                    return "ALL_SHARDS";
                default:
                    return kind.ToString();
            }
        }

        private static void AppendArithmetic(StringBuilder builder,
            ArithmeticOpStep arithmetic, int indent)
        {
            if (arithmetic.ArgSteps == null)
            {
                return;
            }

            for (var index = 0; index < arithmetic.ArgSteps.Length; index++)
            {
                Indent(builder, indent);
                builder.Append(arithmetic.OpSequence[index]).Append(",\n");
                AppendStep(builder, arithmetic.ArgSteps[index], indent);
                if (index < arithmetic.ArgSteps.Length - 1)
                {
                    builder.Append(",\n");
                }
            }
        }

        private static void AppendGroupBy(StringBuilder builder,
            int groupColumnCount, int indent)
        {
            if (groupColumnCount < 0)
            {
                return;
            }

            Indent(builder, indent);
            builder.Append("GROUP BY:\n");
            Indent(builder, indent);
            if (groupColumnCount == 0)
            {
                builder.Append("No grouping expressions");
            }
            else if (groupColumnCount == 1)
            {
                builder.Append(
                    "Grouping by the first expression in the SELECT list");
            }
            else
            {
                builder.Append("Grouping by the first ")
                    .Append(groupColumnCount)
                    .Append(" expressions in the SELECT list");
            }
            builder.Append("\n\n");
        }

        private static void AppendGroupStep(StringBuilder builder,
            GroupStep group, int indent)
        {
            Indent(builder, indent);
            builder.Append("Grouping Columns : ");
            for (var index = 0; index < group.GroupingColumnCount; index++)
            {
                builder.Append(group.ColumnNames[index]);
                if (index < group.GroupingColumnCount - 1)
                {
                    builder.Append(", ");
                }
            }
            builder.Append('\n');

            Indent(builder, indent);
            builder.Append("Aggregate Functions : ");
            if (group.AggregateFuncCodes != null)
            {
                for (var index = 0;
                     index < group.AggregateFuncCodes.Length;
                     index++)
                {
                    builder.Append(GetFunctionName(
                        group.AggregateFuncCodes[index]));
                    if (index < group.AggregateFuncCodes.Length - 1)
                    {
                        builder.Append(",\n");
                    }
                }
            }
            builder.Append('\n');

            if (group.InputStep != null)
            {
                AppendStep(builder, group.InputStep, indent);
            }
        }

        private static string GetFunctionName(SQLFuncCode code)
        {
            switch (code)
            {
                case SQLFuncCode.CountStar:
                    return "FN_COUNT_STAR";
                case SQLFuncCode.Count:
                    return "FN_COUNT";
                case SQLFuncCode.CountNumbers:
                    return "FN_COUNT_NUMBERS";
                case SQLFuncCode.Sum:
                    return "FN_SUM";
                case SQLFuncCode.Min:
                    return "FN_MIN";
                case SQLFuncCode.Max:
                    return "FN_MAX";
                case SQLFuncCode.ArrayCollect:
                    return "FN_ARRAY_COLLECT";
                case SQLFuncCode.ArrayCollectDistinct:
                    return "FN_ARRAY_COLLECT_DISTINCT";
                default:
                    return code.ToString();
            }
        }

        private static void AppendOptionalStep(StringBuilder builder,
            string label, PlanStep step, int indent)
        {
            if (step == null)
            {
                return;
            }

            builder.Append("\n\n");
            Indent(builder, indent);
            builder.Append(label).Append(":\n");
            AppendStep(builder, step, indent);
        }

        private static void AppendSortFields(StringBuilder builder,
            SortSpec[] specs, int indent)
        {
            if (specs == null || specs.Length == 0)
            {
                return;
            }

            Indent(builder, indent);
            builder.Append("Sort Fields : ");
            for (var index = 0; index < specs.Length; index++)
            {
                builder.Append(specs[index].FieldName);
                if (index < specs.Length - 1)
                {
                    builder.Append(", ");
                }
            }
            builder.Append(",\n");
        }

        private static void AppendStringValues(StringBuilder builder,
            string label, string[] values, int indent)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            Indent(builder, indent);
            builder.Append(label).Append(" : ")
                .AppendJoin(", ", values).Append(",\n");
        }

        private static void Indent(StringBuilder builder, int count) =>
            builder.Append(' ', count);
    }
}
