/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Metadata;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;
    using static DataTestUtils;
    using static Utils;

    internal static class QueryUtils
    {
        internal class QuerySortableFieldValue :
            IComparable<QuerySortableFieldValue>,
            IEquatable<QuerySortableFieldValue>
        {
            internal FieldValue Value { get; }
            private readonly int nullRank;

            internal QuerySortableFieldValue(FieldValue value,
                bool nullsFirst = false)
            {
                Assert.IsNotNull(value); // test self-check
                Value = value;
                nullRank = nullsFirst ? -1 : 1;
            }

            // TODO:
            // The comparison and equality functionality below should be made
            // independent of FieldValue so that we don't depend on FieldValue
            // to generate correct expected query results and can verify this
            // FieldValue functionality independently.

            public int CompareTo(QuerySortableFieldValue other)
            {
                Assert.IsNotNull(other); // test self-check
                return Value.QueryCompareTotalOrder(other.Value, nullRank);
            }

            public bool Equals(QuerySortableFieldValue other)
            {
                Assert.IsNotNull(other); // test self-check
                return Value.QueryEquals(other.Value);
            }
        }

        // Allows for correct comparisons in WHERE clause that will return
        // false if any value is a NULL, JSON NULL or EMPTY value.
        internal class InExprFieldValue
        {
            internal FieldValue Value { get; }

            internal InExprFieldValue(FieldValue value)
            {
                Assert.IsNotNull(value); // test self-check
                Value = value;
            }

            public override bool Equals(object obj) =>
                obj is FieldValue fv && Value.QueryEquals(fv);

            public override int GetHashCode() => Value.GetHashCode();
            
            public static bool operator ==(InExprFieldValue val,
                FieldValue other) => val!.Value == other;

            public static bool operator !=(InExprFieldValue val,
                FieldValue other) => !(val == other);

            public static bool operator <(InExprFieldValue val,
                FieldValue other) => !val.Value.IsSpecial &&
                !other.IsSpecial && val.Value < other;

            public static bool operator >(InExprFieldValue val,
                FieldValue other) => !val.Value.IsSpecial &&
                !other.IsSpecial && val.Value > other;

            public static bool operator <=(InExprFieldValue val,
                FieldValue other) => !val.Value.IsSpecial &&
                !other.IsSpecial && val.Value <= other;

            public static bool operator >=(InExprFieldValue val,
                FieldValue other) => !val.Value.IsSpecial &&
                !other.IsSpecial && val.Value >= other;
        }

        private static FieldValue QueryMinMax(
            IEnumerable<FieldValue> values, bool isMin) =>
            values.Where(val => !val.IsSpecial).Aggregate(
                (FieldValue)null,
                (acc, val) => acc == null
                    ? val
                    : (acc.QueryCompareTotalOrder(val) * (isMin ? 1 : -1) < 0
                        ? acc
                        : val));

        internal static IReadOnlyList<RecordValue> SortRows(
            IEnumerable<RecordValue> rows, IReadOnlyList<TableField> fields)
        {
            var rowList = rows.ToList();
            rowList.Sort((row1, row2) => fields.Select(field =>
                    row1.Field(field.Name)
                        .QueryCompareTotalOrder(row2.Field(field.Name)))
                .FirstOrDefault(result => result != 0));
            return rowList;
        }

        // Simple field and array index steps that account for absence of key
        // or element.

        internal static FieldValue Field(this FieldValue val, string name,
            bool isIntermediate = false) =>
            val is MapValue mapVal && mapVal.ContainsKey(name)
                ? mapVal[name]
                : (isIntermediate ? FieldValue.Empty : FieldValue.Null);

        internal static FieldValue Field(this FieldValue val, int idx,
            bool isIntermediate = false) =>
            val is ArrayValue arrVal && idx >= 0 && idx < arrVal.Count
                ? arrVal[idx]
                : (isIntermediate ? FieldValue.Empty : FieldValue.Null);

        // This is not an efficient implementation as it creates new object
        // for each field value, but it allows us to use LINQ order by syntax
        // when constructing expected query results.
        internal static QuerySortableFieldValue QuerySortable(
            this FieldValue val, bool nullsFirst = false) =>
            new QuerySortableFieldValue(val, nullsFirst);

        internal static InExprFieldValue InExpr(this FieldValue val) =>
            new InExprFieldValue(val);

        internal static RecordValue Project(this MapValue mapVal,
            IEnumerable<string> fieldNames)
        {
            var result = new RecordValue();
            foreach (var name in fieldNames)
            {
                var fieldSteps = name.Split('.');
                Assert.IsTrue(fieldSteps.Length > 0);
                var val = fieldSteps.Aggregate<string, FieldValue>(
                    mapVal, (current, step) => current.Field(step));
                result[fieldSteps[^1]] = val;
            }

            return result;
        }

        // Note that QueryAdd, QuerySubtract, etc. may modify the original
        // value which will corrupt our expected results, so we have to make
        // a copy first.

        internal static FieldValue QueryAdd(FieldValue val1,
            FieldValue val2) => val1.IsSpecial || val2.IsSpecial
            ? FieldValue.Null
            : DeepCopy(val1).QueryAdd(val2);

        internal static FieldValue QuerySubtract(FieldValue val1,
            FieldValue val2) => val1.IsSpecial || val2.IsSpecial
            ? FieldValue.Null
            : DeepCopy(val1).QuerySubtract(val2);

        internal static FieldValue QueryDivide(FieldValue val1,
            FieldValue val2, bool isFloating) =>
            val1.IsSpecial || val2.IsSpecial
                ? FieldValue.Null
                : DeepCopy(val1).QueryDivide(val2, isFloating);

        internal static FieldValue QueryCount(
            this IEnumerable<FieldValue> values) =>
            values.Count(val => !val.IsSpecial);

        internal static FieldValue QueryMin(
            this IEnumerable<FieldValue> values) => QueryMinMax(values, true);

        internal static FieldValue QueryMax(
            this IEnumerable<FieldValue> values) =>
            QueryMinMax(values, false);

        internal static FieldValue QuerySum(
            this IEnumerable<FieldValue> values) =>
            values.Where(val => val.IsNumeric).Aggregate(
                FieldValue.Null,
                (acc, val) =>
                    acc == FieldValue.Null ? val : QueryAdd(acc, val));

        internal static FieldValue QueryAvg(
            this IEnumerable<FieldValue> values)
        {
            var numValues = values.Where(val => val.IsNumeric).ToList();
            return numValues.Count == 0
                ? FieldValue.Null
                : QueryDivide(numValues.QuerySum(), numValues.Count, true);
        }

        internal static ArrayValue QueryCollect(
            this IEnumerable<FieldValue> values) =>
            new ArrayValue(values.Where(val =>
                val != FieldValue.Null && val != FieldValue.Empty));

        internal static ArrayValue QueryCollectDistinct(
            this IEnumerable<FieldValue> values) =>
            new ArrayValue(values.Where(val =>
                val != FieldValue.Null && val != FieldValue.Empty)
                .Distinct());

        // For GROUP BY, we skip rows containing EMPTY value in any grouping
        // column.
        internal static bool ToIncludeGroup(this RecordValue group) =>
            !group.Values.Contains(FieldValue.Empty);

        internal static TMapValue Concat<TMapValue>(this TMapValue val1,
            TMapValue val2) where TMapValue : MapValue, new()
        {
            var result = new TMapValue();
            
            foreach (var kv in val1)
            {
                result.Add(kv.Key, kv.Value);
            }
            
            foreach (var kv in val2)
            {
                result.Add(kv.Key, kv.Value);
            }
            
            return result;
        }

        internal static void VerifyResultRows(IReadOnlyList<RecordValue> rows,
            IReadOnlyList<RecordValue> expectedRows,
            RecordFieldType resultType, bool isOrdered)
        {
            Assert.AreEqual(expectedRows.Count, rows.Count);
            // For unordered results, we need to sort both expected and actual
            // results to do proper verification.
            if (!isOrdered && expectedRows.Count > 1)
            {
                var fields = resultType.Fields;
                // We may have identity field if the fields passed are table
                // fields (which is the case when doing select *). We exclude
                // identity field since expectedRows currently don't have it.
                // Since we currently skip identity field during verification,
                // the sorting order with respect to it doesn't matter.
                if (fields.Any(field => field.FieldType.IsIdentity))
                {
                    fields =
                        fields.Where(field => !field.FieldType.IsIdentity)
                            .ToArray();
                }

                expectedRows = SortRows(expectedRows, fields);
                rows = SortRows(rows, fields);
            }

            for (var i = 0; i < rows.Count; i++)
            {
                VerifyFieldValue(expectedRows[i], rows[i], resultType);
            }
        }
    }
}
