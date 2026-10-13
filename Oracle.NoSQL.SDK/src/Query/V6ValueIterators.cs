/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using static V6IteratorUtils;

    internal class ArrayConstructorIterator : OneResultIterator
    {
        private readonly ArrayConstructorStep step;
        private readonly PlanSyncIterator[] arguments;

        internal ArrayConstructorIterator(QueryRuntime runtime,
            ArrayConstructorStep step) : base(runtime)
        {
            this.step = step;
            arguments = CreateIterators(runtime, step.ArgSteps);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            ArrayValue result;
            if (step.IsConditional)
            {
                if (!arguments[0].Next())
                {
                    done = true;
                    return false;
                }

                var first = arguments[0].Result;
                if (!arguments[0].Next())
                {
                    Result = first;
                    done = true;
                    return true;
                }

                result = new ArrayValue();
                AddIfNotNull(result, first);
                AddIfNotNull(result, arguments[0].Result);
            }
            else
            {
                result = new ArrayValue();
            }

            foreach (var iterator in arguments)
            {
                while (iterator.Next())
                {
                    AddIfNotNull(result, iterator.Result);
                }
            }

            Result = result;
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            ResetIterators(arguments, resetResult);
        }

        internal override PlanStep Step => step;

        private static void AddIfNotNull(ArrayValue result, FieldValue value)
        {
            if (value != FieldValue.Null)
            {
                result.Add(value);
            }
        }
    }

    internal class IsNullIterator : OneResultIterator
    {
        private readonly IsNullStep step;
        private readonly PlanSyncIterator input;

        internal IsNullIterator(QueryRuntime runtime, IsNullStep step) :
            base(runtime)
        {
            this.step = step;
            input = step.InputStep.CreateSyncIterator(runtime);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            var isNull = input.Next() && input.Result == FieldValue.Null;
            Result = step.FuncCode == QueryFuncCode.IsNull
                ? (isNull ? BooleanValue.True : BooleanValue.False)
                : (isNull ? BooleanValue.False : BooleanValue.True);
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            input.Reset(resetResult);
        }

        internal override PlanStep Step => step;
    }

    internal class AndOrIterator : OneResultIterator
    {
        private readonly AndOrStep step;
        private readonly PlanSyncIterator[] arguments;

        internal AndOrIterator(QueryRuntime runtime, AndOrStep step) :
            base(runtime)
        {
            this.step = step;
            arguments = CreateIterators(runtime, step.ArgSteps);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            var isAnd = step.FuncCode == QueryFuncCode.And;
            var result = isAnd;
            var hasNull = false;
            foreach (var iterator in arguments)
            {
                if (!iterator.Next())
                {
                    result = isAnd ? false : result;
                }
                else if (iterator.Result == FieldValue.Null)
                {
                    hasNull = true;
                    continue;
                }
                else
                {
                    var value = iterator.Result.AsBoolean;
                    result = isAnd ? result && value : result || value;
                }

                if (isAnd ? !result : result)
                {
                    hasNull = false;
                    break;
                }
            }

            Result = hasNull ? FieldValue.Null :
                (result ? BooleanValue.True : BooleanValue.False);
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            ResetIterators(arguments, resetResult);
        }

        internal override PlanStep Step => step;
    }

    internal class CaseIterator : PlanSyncIterator
    {
        private readonly CaseStep step;
        private readonly PlanSyncIterator[] conditions;
        private readonly PlanSyncIterator[] thenSteps;
        private readonly PlanSyncIterator elseStep;
        private PlanSyncIterator activeStep;
        private bool done;

        internal CaseIterator(QueryRuntime runtime, CaseStep step) : base(runtime)
        {
            this.step = step;
            conditions = CreateIterators(runtime, step.ConditionSteps);
            thenSteps = CreateIterators(runtime, step.ThenSteps);
            elseStep = step.ElseStep?.CreateSyncIterator(runtime);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            if (activeStep == null)
            {
                for (var i = 0; i < conditions.Length; i++)
                {
                    if (conditions[i].Next() &&
                        conditions[i].Result != FieldValue.Null &&
                        conditions[i].Result.AsBoolean)
                    {
                        activeStep = thenSteps[i];
                        break;
                    }
                }
                activeStep ??= elseStep;
                if (activeStep == null)
                {
                    done = true;
                    return false;
                }
            }

            if (!activeStep.Next())
            {
                done = true;
                return false;
            }

            Result = activeStep.Result;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            done = false;
            activeStep = null;
            ResetIterators(conditions, resetResult);
            ResetIterators(thenSteps, resetResult);
            elseStep?.Reset(resetResult);
        }

        internal override PlanStep Step => step;
    }

    internal class ValueCompareIterator : OneResultIterator
    {
        private static readonly Regex TimestampPattern = new Regex(
            @"\A([0-9]{1,4})-([0-9]{1,2})-([0-9]{1,2})" +
            @"(?:T([0-9]{1,2})(?::([0-9]{1,2})(?::([0-9]{1,2})" +
            @"(?:\.([0-9]{1,9}))?)?)?)?(Z|[+-][0-9]{2}:[0-9]{2})?\z",
            RegexOptions.CultureInvariant);

        private readonly ValueCompareStep step;
        private readonly PlanSyncIterator left;
        private readonly PlanSyncIterator right;

        internal ValueCompareIterator(QueryRuntime runtime,
            ValueCompareStep step) : base(runtime)
        {
            this.step = step;
            left = step.LeftStep.CreateSyncIterator(runtime);
            right = step.RightStep.CreateSyncIterator(runtime);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            var hasLeft = left.Next();
            var leftValue = hasLeft ? left.Result : null;
            if (hasLeft && left.Next())
            {
                throw new InvalidOperationException(GetMessageWithLocation(
                    "The left comparison operand is a sequence with more than one item"));
            }
            var hasRight = right.Next();
            var rightValue = hasRight ? right.Result : null;
            if (hasRight && right.Next())
            {
                throw new InvalidOperationException(GetMessageWithLocation(
                    "The right comparison operand is a sequence with more than one item"));
            }

            var comparison = new ComparisonResult();
            if (!hasLeft && !hasRight)
            {
                comparison.Value = 0;
            }
            else if (!hasLeft || !hasRight)
            {
                if (step.FuncCode == QueryFuncCode.NotEqual)
                {
                    comparison.Value = 1;
                }
                else
                {
                    comparison.Incompatible = true;
                }
            }
            else
            {
                CompareValues(leftValue, rightValue, comparison);
            }

            Result = comparison.HasNull ? FieldValue.Null :
                comparison.Incompatible ? BooleanValue.False :
                (ApplyComparison(comparison.Value) ? BooleanValue.True :
                    BooleanValue.False);
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            left.Reset(resetResult);
            right.Reset(resetResult);
        }

        internal override PlanStep Step => step;

        private void CompareValues(FieldValue leftValue, FieldValue rightValue,
            ComparisonResult result)
        {
            result.Clear();
            if (leftValue == FieldValue.Null || rightValue == FieldValue.Null)
            {
                result.HasNull = true;
                return;
            }
            if (leftValue.DbType == DbType.JsonNull ||
                rightValue.DbType == DbType.JsonNull)
            {
                var bothJsonNull = leftValue.DbType == DbType.JsonNull &&
                    rightValue.DbType == DbType.JsonNull;
                if (bothJsonNull)
                {
                    result.Value = 0;
                }
                else if (step.FuncCode == QueryFuncCode.NotEqual)
                {
                    result.Value = 1;
                }
                else
                {
                    result.Incompatible = true;
                }
                return;
            }

            if (leftValue.DbType == DbType.Empty ||
                rightValue.DbType == DbType.Empty)
            {
                if (leftValue.DbType == DbType.Empty &&
                    rightValue.DbType == DbType.Empty &&
                    (step.FuncCode == QueryFuncCode.Equal ||
                     step.FuncCode == QueryFuncCode.GreaterOrEqual ||
                     step.FuncCode == QueryFuncCode.LessOrEqual))
                {
                    result.Value = 0;
                }
                else if (leftValue.DbType != rightValue.DbType &&
                         step.FuncCode == QueryFuncCode.NotEqual)
                {
                    result.Value = 1;
                }
                else
                {
                    result.Incompatible = true;
                }
                return;
            }

            var leftType = leftValue.DbType;
            var rightType = rightValue.DbType;
            if (leftValue.IsNumeric && rightValue.IsNumeric)
            {
                result.Value = leftValue.QueryCompare(rightValue);
                return;
            }
            if (leftType == DbType.Timestamp && rightType == DbType.Timestamp)
            {
                // JSON omits trailing fractional zeros. Its lexical order
                // (notably the trailing 'Z') is not chronological order.
                result.Value = leftValue.QueryCompare(rightValue);
                return;
            }
            if (leftType == DbType.Timestamp && rightType == DbType.String)
            {
                result.Value = leftValue.AsDateTime.CompareTo(
                    ParseTimestamp(rightValue.AsString));
                return;
            }
            if (leftType == DbType.String && rightType == DbType.Timestamp)
            {
                result.Value = ParseTimestamp(leftValue.AsString).CompareTo(
                    rightValue.AsDateTime);
                return;
            }
            if (leftType == DbType.String && rightType == DbType.String)
            {
                result.Value = string.CompareOrdinal(leftValue.AsString,
                    rightValue.AsString);
                return;
            }
            if (leftType == DbType.Boolean && rightType == DbType.Boolean)
            {
                result.Value = leftValue.QueryCompare(rightValue);
                return;
            }
            if (leftType == DbType.Binary && rightType == DbType.Binary)
            {
                if (step.FuncCode == QueryFuncCode.Equal ||
                    step.FuncCode == QueryFuncCode.NotEqual)
                {
                    result.Value = leftValue.QueryEquals(rightValue) ? 0 : 1;
                }
                else
                {
                    result.Incompatible = true;
                }
                return;
            }
            if (leftType == DbType.Map && rightType == DbType.Map)
            {
                if (step.FuncCode == QueryFuncCode.Equal ||
                    step.FuncCode == QueryFuncCode.NotEqual)
                {
                    CompareMaps(leftValue.AsMapValue, rightValue.AsMapValue,
                        result);
                }
                else
                {
                    result.Incompatible = true;
                }
                return;
            }
            if (leftType == DbType.Array && rightType == DbType.Array)
            {
                CompareArrays(leftValue.AsArrayValue, rightValue.AsArrayValue,
                    result);
                return;
            }
            result.Incompatible = true;
        }

        private DateTime ParseTimestamp(string value)
        {
            // KV compares TIMESTAMP with STRING by parsing the string in
            // its default ISO format, with UTC as the default time zone.
            // Do not accept culture-specific dates through DateTime.Parse.
            var match = TimestampPattern.Match(value);
            if (!match.Success)
            {
                throw new InvalidOperationException(GetMessageWithLocation(
                    $"invalid timestamp string: {value}"));
            }

            try
            {
                int Component(int index) => match.Groups[index].Success
                    ? int.Parse(match.Groups[index].Value,
                        CultureInfo.InvariantCulture) : 0;
                var timestamp = new DateTime(Component(1), Component(2),
                    Component(3), Component(4), Component(5), Component(6),
                    DateTimeKind.Utc);
                if (match.Groups[7].Success)
                {
                    var nanos = int.Parse(match.Groups[7].Value.PadRight(9, '0'),
                        CultureInfo.InvariantCulture);
                    // Match the SDK's existing DateTime wire representation:
                    // timestamps are rounded to 100 ns (seven digits).
                    timestamp = timestamp.AddTicks((nanos + 50L) / 100);
                }

                var zone = match.Groups[8].Value;
                if (zone.Length > 1)
                {
                    // KV's non-UTC offset parser requires the complete ISO
                    // date and, when present, the complete time component.
                    if (zone != "+00:00" &&
                        (match.Groups[1].Length != 4 ||
                         match.Groups[2].Length != 2 ||
                         match.Groups[3].Length != 2 ||
                         (match.Groups[4].Success &&
                          (match.Groups[4].Length != 2 ||
                           match.Groups[5].Length != 2 ||
                           match.Groups[6].Length != 2))))
                    {
                        throw new ArgumentException(nameof(value));
                    }
                    var hours = int.Parse(zone.Substring(1, 2),
                        CultureInfo.InvariantCulture);
                    var minutes = int.Parse(zone.Substring(4, 2),
                        CultureInfo.InvariantCulture);
                    if (hours > 18 || minutes > 59 ||
                        (hours == 18 && minutes != 0))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                    var offset = hours * 60 + minutes;
                    timestamp = timestamp.AddMinutes(zone[0] == '+'
                        ? -offset : offset);
                }
                return timestamp;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(GetMessageWithLocation(
                    $"invalid timestamp string: {value}"), exception);
            }
        }

        private void CompareMaps(MapValue leftValue, MapValue rightValue,
            ComparisonResult result)
        {
            if (leftValue.Count != rightValue.Count)
            {
                result.Value = 1;
                return;
            }

            foreach (var pair in leftValue)
            {
                if (!rightValue.TryGetValue(pair.Key, out var rightItem))
                {
                    result.Value = 1;
                    return;
                }

                CompareValues(pair.Value, rightItem, result);
                if (result.Value != 0 || result.HasNull ||
                    result.Incompatible)
                {
                    return;
                }
            }
        }

        private void CompareArrays(ArrayValue leftValue, ArrayValue rightValue,
            ComparisonResult result)
        {
            if ((step.FuncCode == QueryFuncCode.Equal ||
                 step.FuncCode == QueryFuncCode.NotEqual) &&
                leftValue.Count != rightValue.Count)
            {
                result.Value = 1;
                return;
            }

            var count = Math.Min(leftValue.Count, rightValue.Count);
            for (var i = 0; i < count; i++)
            {
                CompareValues(leftValue[i], rightValue[i], result);
                if (result.Value != 0 || result.HasNull || result.Incompatible)
                {
                    return;
                }
            }

            result.Value = leftValue.Count.CompareTo(rightValue.Count);
        }

        private bool ApplyComparison(int comparison) => step.FuncCode switch
        {
            QueryFuncCode.Equal => comparison == 0,
            QueryFuncCode.NotEqual => comparison != 0,
            QueryFuncCode.GreaterThan => comparison > 0,
            QueryFuncCode.GreaterOrEqual => comparison >= 0,
            QueryFuncCode.LessThan => comparison < 0,
            QueryFuncCode.LessOrEqual => comparison <= 0,
            _ => throw new InvalidOperationException()
        };

        private sealed class ComparisonResult
        {
            internal int Value { get; set; }
            internal bool Incompatible { get; set; }
            internal bool HasNull { get; set; }

            internal void Clear()
            {
                Value = 0;
                Incompatible = false;
                HasNull = false;
            }
        }
    }

    internal class SeqAggregateIterator : OneResultIterator
    {
        private readonly SeqAggregateStep step;
        private readonly PlanSyncIterator input;

        internal SeqAggregateIterator(QueryRuntime runtime,
            SeqAggregateStep step) : base(runtime)
        {
            this.step = step;
            input = step.InputStep.CreateSyncIterator(runtime);
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            long count = 0;
            var sumType = DbType.Long;
            long longSum = 0;
            double doubleSum = 0;
            decimal numberSum = 0;
            FieldValue minMax = null;
            var sawValue = false;
            var hasInput = false;
            while (input.Next())
            {
                hasInput = true;
                var value = input.Result;
                switch (step.FuncCode)
                {
                    case QueryFuncCode.SeqCount:
                        if (value == FieldValue.Null)
                        {
                            Result = FieldValue.Null;
                            done = true;
                            return true;
                        }
                        ++count;
                        break;
                    case QueryFuncCode.SeqCountIgnoreNulls:
                        if (value != FieldValue.Null) ++count;
                        break;
                    case QueryFuncCode.SeqCountNumbersIgnoreNulls:
                        if (value.IsNumeric) ++count;
                        break;
                    case QueryFuncCode.SeqSum:
                    case QueryFuncCode.SeqAverage:
                        if (value.IsNumeric)
                        {
                            AddToSum(value, ref sumType, ref longSum,
                                ref doubleSum, ref numberSum);
                            ++count;
                        }
                        break;
                    default:
                        if (value == FieldValue.Null &&
                            (step.FuncCode == QueryFuncCode.SeqMin ||
                             step.FuncCode == QueryFuncCode.SeqMax))
                        {
                            Result = FieldValue.Null;
                            done = true;
                            return true;
                        }
                        if (!value.IsSpecial && value.SupportsComparison)
                        {
                            if (!sawValue || (step.FuncCode == QueryFuncCode.SeqMin ||
                                              step.FuncCode == QueryFuncCode.SeqMinIgnoreNulls
                                ? value.QueryCompare(minMax) < 0
                                : value.QueryCompare(minMax) > 0))
                            {
                                minMax = value;
                            }
                            sawValue = true;
                        }
                        break;
                }
            }

            if (!hasInput && step.FuncCode != QueryFuncCode.SeqCount &&
                step.FuncCode != QueryFuncCode.SeqCountIgnoreNulls)
            {
                done = true;
                return false;
            }

            Result = step.FuncCode switch
            {
                QueryFuncCode.SeqCount or QueryFuncCode.SeqCountIgnoreNulls or
                    QueryFuncCode.SeqCountNumbersIgnoreNulls => new LongValue(count),
                QueryFuncCode.SeqSum => count == 0 ? FieldValue.Null :
                    GetSum(sumType, longSum, doubleSum, numberSum),
                QueryFuncCode.SeqAverage => count == 0 ? FieldValue.Null :
                    sumType == DbType.Number
                        ? new NumberValue(numberSum / count)
                        : new DoubleValue((sumType == DbType.Long
                            ? longSum : doubleSum) / count),
                _ => minMax ?? FieldValue.Null
            };
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            input.Reset(resetResult);
        }

        internal override PlanStep Step => step;

        private static void AddToSum(FieldValue value, ref DbType sumType,
            ref long longSum, ref double doubleSum, ref decimal numberSum)
        {
            if (sumType == DbType.Number || value.DbType == DbType.Number)
            {
                try
                {
                    var sum = sumType switch
                    {
                        DbType.Long => longSum,
                        DbType.Double => (decimal)doubleSum,
                        _ => numberSum
                    };
                    var operand = value.ToDecimal();
                    var sumUnderflows = sumType == DbType.Double &&
                        doubleSum != 0 && sum == 0;
                    var operandUnderflows = value.DbType == DbType.Double &&
                        value.AsDouble != 0 && operand == 0;
                    if (!sumUnderflows && !operandUnderflows)
                    {
                        numberSum = sum + operand;
                        sumType = DbType.Number;
                        return;
                    }
                    // Conversion can silently round tiny nonzero doubles to
                    // zero. Preserve them through the DOUBLE fallback below.
                }
                catch (OverflowException)
                {
                    // Like QueryAdd, fall back to double if either the
                    // conversion or the addition exceeds decimal's range.
                    // Neither accumulator has changed when this happens.
                }
            }
            else if (sumType == DbType.Long &&
                     (value.DbType == DbType.Integer ||
                      value.DbType == DbType.Long))
            {
                longSum = unchecked(longSum + value.ToInt64());
                return;
            }

            doubleSum = (sumType switch
            {
                DbType.Long => longSum,
                DbType.Number => (double)numberSum,
                _ => doubleSum
            }) + value.ToDouble();
            sumType = DbType.Double;
        }

        private static FieldValue GetSum(DbType sumType, long longSum,
            double doubleSum, decimal numberSum) => sumType switch
        {
            DbType.Long => new LongValue(longSum),
            DbType.Double => new DoubleValue(doubleSum),
            DbType.Number => new NumberValue(numberSum),
            _ => throw new InvalidOperationException()
        };
    }

    internal static class V6IteratorUtils
    {
        internal static PlanSyncIterator[] CreateIterators(QueryRuntime runtime,
            PlanStep[] steps)
        {
            var result = new PlanSyncIterator[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                result[i] = steps[i].CreateSyncIterator(runtime);
            }
            return result;
        }

        internal static void ResetIterators(IEnumerable<PlanSyncIterator> iterators,
            bool resetResult)
        {
            foreach (var iterator in iterators)
            {
                iterator.Reset(resetResult);
            }
        }
    }
}
