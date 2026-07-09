/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;
    using System.Text;

    // Produces the driver-plan text used by ALL-profile query statistics.
    // The plan is already present in PreparedStatement, so formatting it does
    // not add a service request or alter query execution.
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
            Indent(builder, indent);
            builder.Append(step.Name).Append("([")
                .Append(step.ResultPosition).Append("])").AppendLine();
            Indent(builder, indent);
            builder.AppendLine("[");
            AppendContent(builder, step, indent + 2);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append(']');
        }

        private static void AppendContent(StringBuilder builder, PlanStep step,
            int indent)
        {
            switch (step)
            {
                case SortStep sort:
                    AppendChild(builder, "INPUT", sort.InputStep, indent);
                    AppendSortSpecs(builder, sort.SortSpecs, indent);
                    break;
                case SFWStep sfw:
                    AppendChild(builder, "FROM", sfw.FromStep, indent);
                    if (!string.IsNullOrEmpty(sfw.FromVarName))
                    {
                        builder.Append(" as ").Append(sfw.FromVarName);
                    }
                    builder.AppendLine().AppendLine();
                    AppendChildren(builder, "SELECT", sfw.ColumnSteps,
                        indent);
                    AppendOptionalChild(builder, "OFFSET", sfw.OffsetStep,
                        indent);
                    AppendOptionalChild(builder, "LIMIT", sfw.LimitStep,
                        indent);
                    break;
                case ReceiveStep receive:
                    AppendValue(builder, "DistributionKind",
                        receive.DistributionKind, indent);
                    AppendSortSpecs(builder, receive.SortSpecs, indent);
                    AppendValues(builder, "Primary Key Fields",
                        receive.PrimaryKeyFields, indent);
                    break;
                case ConstStep constant:
                    AppendValue(builder, "Value", constant.Value, indent);
                    break;
                case VarRefStep variable:
                    AppendValue(builder, "Variable", variable.VarName,
                        indent);
                    break;
                case ExtVarRefStep external:
                    AppendValue(builder, "Variable", external.VarName,
                        indent);
                    AppendValue(builder, "Position", external.VarPosition,
                        indent);
                    break;
                case FieldStep field:
                    AppendChild(builder, "INPUT", field.InputStep, indent);
                    builder.AppendLine();
                    AppendValue(builder, "Field", field.FieldName, indent);
                    break;
                case ArithmeticOpStep arithmetic:
                    AppendChildren(builder, "ARGUMENTS", arithmetic.ArgSteps,
                        indent);
                    AppendValue(builder, "Operators", arithmetic.OpSequence,
                        indent);
                    break;
                case AggregateFuncStep aggregate:
                    AppendChild(builder, "INPUT", aggregate.InputStep,
                        indent);
                    break;
                case FuncSizeStep size:
                    AppendChild(builder, "INPUT", size.InputStep, indent);
                    break;
                case GroupStep group:
                    AppendChild(builder, "INPUT", group.InputStep, indent);
                    builder.AppendLine();
                    AppendValue(builder, "Grouping Columns",
                        group.GroupingColumnCount, indent);
                    AppendValues(builder, "Column Names", group.ColumnNames,
                        indent);
                    AppendValues(builder, "Aggregate Functions",
                        group.AggregateFuncCodes, indent);
                    break;
            }
        }

        private static void AppendChild(StringBuilder builder, string label,
            PlanStep child, int indent)
        {
            Indent(builder, indent);
            builder.Append(label).AppendLine(":");
            if (child != null)
            {
                AppendStep(builder, child, indent);
            }
        }

        private static void AppendOptionalChild(StringBuilder builder,
            string label, PlanStep child, int indent)
        {
            if (child == null)
            {
                return;
            }

            builder.AppendLine().AppendLine();
            AppendChild(builder, label, child, indent);
        }

        private static void AppendChildren(StringBuilder builder, string label,
            PlanStep[] children, int indent)
        {
            Indent(builder, indent);
            builder.Append(label).AppendLine(":");
            if (children == null)
            {
                return;
            }

            for (var index = 0; index < children.Length; index++)
            {
                AppendStep(builder, children[index], indent);
                if (index < children.Length - 1)
                {
                    builder.AppendLine(",");
                }
            }
            builder.AppendLine();
        }

        private static void AppendSortSpecs(StringBuilder builder,
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
                var spec = specs[index];
                builder.Append(spec.FieldName)
                    .Append(spec.IsDescending ? " DESC" : " ASC")
                    .Append(spec.NullsFirst ? " NULLS FIRST" :
                        " NULLS LAST");
                if (index < specs.Length - 1)
                {
                    builder.Append(", ");
                }
            }
            builder.AppendLine();
        }

        private static void AppendValues<T>(StringBuilder builder,
            string label, T[] values, int indent)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            Indent(builder, indent);
            builder.Append(label).Append(" : ")
                .AppendJoin(", ", values).AppendLine();
        }

        private static void AppendValue(StringBuilder builder, string label,
            object value, int indent)
        {
            Indent(builder, indent);
            builder.Append(label).Append(" : ").Append(value).AppendLine();
        }

        private static void Indent(StringBuilder builder, int count) =>
            builder.Append(' ', count);
    }
}
