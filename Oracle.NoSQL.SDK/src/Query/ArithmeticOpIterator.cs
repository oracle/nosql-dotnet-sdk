/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;

    /*
     * Iterator to implement the arithmetic operators
     *
     * any_atomic? ArithmeticOp(any?, ....)
     *
     * An instance of this iterator implements either addition/subtraction
     * among two or more input values, or multiplication/division among two or
     * more input values. For example,
     * arg1 + arg2 - arg3 + arg4, or arg1 * arg2 * arg3 / arg4.
     *
     * The only arithmetic op that is strictly needed for the driver is the
     * div (real division) op, to compute an AVG aggregate function as the
     * division of a SUM by a COUNT. However, having all the arithmetic ops
     * implemented allows for expressions in the SELECT list that do
     * arithmetic among aggregate functions (for example: select a, sum(x) +
     * sum(y) from foo group by a).
     */
    class ArithmeticOpIterator : OneResultIterator
    {
        private readonly ArithmeticOpStep step;
        private readonly PlanSyncIterator[] argIterators;
        private readonly FieldValue[] operands;

        internal ArithmeticOpIterator(QueryRuntime runtime,
            ArithmeticOpStep step) : base(runtime)
        {
            this.step = step;
            argIterators = new PlanSyncIterator[step.ArgSteps.Length];
            operands = new FieldValue[argIterators.Length];
            for (var i = 0; i < argIterators.Length; i++)
            {
                argIterators[i] = step.ArgSteps[i].CreateSyncIterator(
                    runtime);
            }
        }

        /*
         * If step.Opcode == ArithmeticOpcode.AddSubtract, step.ops is a
         * string of "+" and/or "-" chars, containing one such char per
         * input value. For example, if the arithmetic expression is
         * (arg1 + arg2 - arg3 + arg4) step.ops is "++-+".
         *
         * If step.Opcode == ArithmeticOpcode.MultiplyDivide, step.ops is
         * a string of "*", "/", and/or "d" chars, containing one such
         * char per input value. For example, if the arithmetic expression
         * is (arg1 * arg2 * arg3 / arg4) step.ops is "***\/". The "d"
         * char is used for the div operator.
         */
        private FieldValue DoOp(char op, FieldValue value, FieldValue operand)
        {
            if (step.Opcode == ArithmeticOpcode.AddSubtract)
            {
                switch (op)
                {
                    case '+':
                        return value.QueryAdd(operand);
                    case '-':
                        return value.QuerySubtract(operand);
                    default:
                        throw new InvalidOperationException(
                            $"Query: invalid operation {op} for " +
                            "AddSubtract step");
                }
            }
            switch (op)
            {
                case '*':
                    return value.QueryMultiply(operand);
                case '/':
                    return value.QueryDivide(operand, false);
                case 'd':
                    return value.QueryDivide(operand, true);
                default:
                    throw new InvalidOperationException(
                        $"Query: invalid operation {op} for " +
                        "MultiplyDivide step");
            }
        }

        private static bool TryConvertDoubleToNumber(double value,
            out decimal number)
        {
            try
            {
                number = (decimal)value;
                // Decimal conversion can silently erase tiny nonzero values.
                return number != 0 || value == 0;
            }
            catch (OverflowException)
            {
                number = 0;
                return false;
            }
        }

        internal override bool Next()
        {
            if (done)
            {
                return false;
            }

            // Promotion applies to the whole expression, including operands
            // after a division. Evaluate inputs before doing any arithmetic
            // so a later NULL can also propagate without arithmetic errors.
            var resultType = step.OpSequence.IndexOf('d') >= 0
                ? DbType.Double : DbType.Integer;
            for (var i = 0; i < argIterators.Length; i++)
            {
                var iterator = argIterators[i];
                if (!iterator.Next())
                {
                    done = true;
                    return false;
                }

                var operand = iterator.Result;
                if (operand == FieldValue.Null)
                {
                    Result = operand;
                    done = true;
                    return true;
                }

                operands[i] = operand;
                switch (operand.DbType)
                {
                    case DbType.Integer:
                        break;
                    case DbType.Long:
                        if (resultType == DbType.Integer)
                        {
                            resultType = DbType.Long;
                        }
                        break;
                    case DbType.Double:
                        if (resultType != DbType.Number)
                        {
                            resultType = DbType.Double;
                        }
                        break;
                    case DbType.Number:
                        resultType = DbType.Number;
                        break;
                    default:
                        throw new InvalidOperationException(
                            GetMessageWithLocation(
                                "Query: arithmetic operand has non-numeric type " +
                                operand.DbType));
                }
            }

            var initialValue = step.Opcode == ArithmeticOpcode.AddSubtract
                ? 0 : 1;
            FieldValue result = resultType switch
            {
                DbType.Integer => new IntegerValue(initialValue),
                DbType.Long => new LongValue(initialValue),
                DbType.Double => new DoubleValue(initialValue),
                DbType.Number => new NumberValue(initialValue),
                _ => throw new InvalidOperationException()
            };
            for (var i = 0; i < operands.Length; i++)
            {
                var operand = operands[i];
                if (resultType == DbType.Number)
                {
                    // NumberValue's mixed DOUBLE operations otherwise
                    // demote the accumulator and lose decimal precision.
                    if (operand.DbType == DbType.Double &&
                        TryConvertDoubleToNumber(operand.AsDouble,
                            out var decimalOperand))
                    {
                        operand = new NumberValue(decimalOperand);
                    }

                    // After falling back to DOUBLE, do not let a later
                    // NUMBER operand convert an unrepresentable accumulator
                    // to decimal. Promotion may resume after cancellation.
                    if (result.DbType == DbType.Double &&
                        operand.DbType == DbType.Number &&
                        !TryConvertDoubleToNumber(result.AsDouble, out _))
                    {
                        operand = new DoubleValue(operand.ToDouble());
                    }
                }
                result = DoOp(step.OpSequence[i], result, operand);
            }

            Result = result;
            done = true;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            base.Reset(resetResult);
            foreach (var iterator in argIterators)
            {
                iterator.Reset(resetResult);
            }
        }

        internal override PlanStep Step => step;
    }

}
