/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
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
    class ArithmeticOpIterator : PlanSyncIterator
    {
        private readonly ArithmeticOpStep step;
        private readonly PlanSyncIterator[] argIterators;

        internal ArithmeticOpIterator(QueryRuntime runtime,
            ArithmeticOpStep step) : base(runtime)
        {
            this.step = step;
            argIterators = new PlanSyncIterator[step.ArgSteps.Length];
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

        internal override bool Next()
        {
            FieldValue result = step.Opcode == ArithmeticOpcode.AddSubtract ?
                0 : 1;
            for (var i = 0; i < argIterators.Length; i++)
            {
                var iterator = argIterators[i];
                if (!iterator.Next())
                {
                    return false;
                }

                var operand = iterator.Result;
                if (operand == FieldValue.Null)
                {
                    Result = operand;
                    return true;
                }

                result = DoOp(step.OpSequence[i], result, operand);
            }

            Result = result;
            return true;
        }

        internal override void Reset(bool resetResult = false)
        {
            foreach (var iterator in argIterators)
            {
                iterator.Reset(resetResult);
            }
        }

        internal override PlanStep Step => step;
    }

}
