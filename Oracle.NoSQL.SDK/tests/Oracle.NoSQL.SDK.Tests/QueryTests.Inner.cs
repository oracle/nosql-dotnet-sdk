/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;

    public partial class QueryTests
    {
        public class QTest
        {
            internal string Description { get; }

            internal string SQL { get; }

            internal IReadOnlyList<TableField> ExpectedFields { get; set; }

            // true for insert, update, delete
            internal bool IsUpdate { get; set; }

            internal bool IsOrdered { get; set; }

            internal bool UpdateTTL { get; set; }

            internal long? MaxMemoryBytes { get; set; }

            internal long? MaxMemoryBytesFail { get; set; }

            internal IReadOnlyList<QTestCase> TestCases { get; set; }

            // For QTests without bindings, there is only 1 test case.
            internal QTestCase TestCase
            {
                get
                {
                    TestCases ??= new[] {new QTestCase(Description)};
                    return TestCases[0];
                }
            }

            internal IEnumerable<KeyValuePair<string, FieldValue>> Bindings
            {
                get => TestCase.Bindings;
                set => TestCase.Bindings = value;
            }

            internal IEnumerable<RecordValue> ExpectedRows
            {
                get => TestCase.ExpectedRows;
                set => TestCase.ExpectedRows = value;
            }

            internal IReadOnlyList<DataRow> UpdatedRows
            {
                get => TestCase.UpdatedRowList;
                set => TestCase.UpdatedRowList = value;
            }

            internal IEnumerable<int> DeletedRowIds
            {
                get => TestCase.DeletedRowIds;
                set => TestCase.DeletedRowIds = value;
            }

            internal QTest(string description, string sql,
                IReadOnlyList<TableField> expectedFields = null,
                IReadOnlyList<QTestCase> testCases = null,
                bool isUpdate = false)
            {
                Description = description ?? "QTest";
                SQL = sql;
                ExpectedFields = expectedFields;
                TestCases = testCases;
                IsUpdate = isUpdate;
            }

            public override string ToString() => $"Query: {Description}";
        }

        public class QTestCase
        {
            internal string Description { get; }

            internal IEnumerable<KeyValuePair<string, FieldValue>> Bindings
            {
                get;
                set;
            }

            internal IReadOnlyList<RecordValue> ExpectedRowList { get; set; }

            internal IEnumerable<RecordValue> ExpectedRows
            {
                get => ExpectedRowList;
                set => ExpectedRowList = value != null ?
                    value.ToList() : new List<RecordValue>();
            }

            internal IReadOnlyList<DataRow> UpdatedRowList { get; set; }

            internal IEnumerable<DataRow> UpdatedRows
            {
                get => UpdatedRowList;
                set => UpdatedRowList = value?.ToList();
            }

            internal IReadOnlyList<int> DeletedRowIdList { get; set; }

            internal IEnumerable<int> DeletedRowIds
            {
                get => DeletedRowIdList;
                set => DeletedRowIdList = value?.ToList();
            }

            internal QTestCase(string description,
                IEnumerable<KeyValuePair<string, FieldValue>> bindings = null,
                IEnumerable<RecordValue> expectedRows = null)
            {
                Description = description ?? "QTestCase";
                Bindings = bindings;
                ExpectedRows = expectedRows;
            }

            public override string ToString() => $"TestCase: {Description}";
        }

        internal class TestComparableFieldValue :
            IComparable<TestComparableFieldValue>,
            IEquatable<TestComparableFieldValue>
        {
            internal FieldValue Value { get; }
            private readonly int nullRank;

            internal TestComparableFieldValue(FieldValue value,
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

            public int CompareTo(TestComparableFieldValue other)
            {
                return Value.QueryCompare(other.Value, nullRank);
            }

            public bool Equals(TestComparableFieldValue other)
            {
                Assert.IsNotNull(other); // test self-check
                return Value.QueryEquals(other.Value);
            }
        }

        // This is not an efficient implementation as it creates new object
        // for each field value, but it allows us to use LINQ orderby syntax
        // when constructing expected query results.
        internal static TestComparableFieldValue ToTestComparable(
            FieldValue value, bool nullsFirst = false) =>
            new TestComparableFieldValue(value, nullsFirst);

        private static int TotalCompareArrays(ArrayValue value1, ArrayValue value2)
        {
            if (value1.Count != value2.Count)
            {
                return value1.Count - value2.Count;
            }

            for (var i = 0; i < value1.Count; i++)
            {
                var result = TotalCompare(value1[i], value2[i]);

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static int TotalCompareMaps(IReadOnlyList<string> keys1,
            MapValue values1, IReadOnlyList<string> keys2, MapValue values2)
        {
            if (keys1.Count != keys2.Count)
            {
                return keys1.Count - keys2.Count;
            }

            for(var i = 0; i < keys1.Count; i++)
            {
                var result = string.Compare(keys1[i], keys2[i],
                    StringComparison.Ordinal);

                if (result != 0)
                {
                    return result;
                }
            }

            foreach (var key in keys1)
            {
                var result = TotalCompare(values1[key], values2[key]);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static int
            TotalCompareMaps(MapValue value1, MapValue value2) =>
            TotalCompareMaps(
                (from key in value1.Keys orderby key select key).ToList(),
                value1,
                (from key in value2.Keys orderby key select key).ToList(),
                value2);

        private static int TotalCompareRecords(RecordValue value1,
            RecordValue value2) =>
        TotalCompareMaps(
            (from i in Enumerable.Range(0, value1.Count)
                select value1.GetKeyAtIndex(i)).ToList(),
            value1,
            (from i in Enumerable.Range(0, value2.Count)
                select value2.GetKeyAtIndex(i)).ToList(),
            value2);


        // This function is used to sort query results to verify unordered
        // queries.  The sort order doesn't matter as long as it is
        // deterministic and the expected and actual results are sorted in the
        // same manner.  The only exception is for numeric field values:
        // for queries computing expressions or aggregates as well as when
        // there is float to double conversion we allow a small delta between
        // expected and actual numeric values.  This means that they must be
        // sorted on their numeric value and not by other criteria (such as
        // their string representation).
        internal static int TotalCompare(FieldValue value1, FieldValue value2)
        {
            // There not supposed to be nulls here but it will be more
            // diagnostically helpful to deal with them during result
            // verification rather than sorting.
            if (value1 is null)
            {
                return value2 is null ? 0 : -1;
            }

            if (value2 is null)
            {
                return 1;
            }

            try
            {
                return value1.QueryCompare(value2);
            }
            catch (NotSupportedException)
            {
                // This is totally arbitrary but should be deterministic for
                // sorting expected and actual results.
                if (value1.DbType != value2.DbType)
                {
                    return value1.DbType - value2.DbType;
                }

                switch (value1.DbType)
                {
                    case DbType.Binary:
                        return string.Compare(value1.ToJsonString(),
                            value2.ToJsonString(), StringComparison.Ordinal);
                    case DbType.Array:
                        return TotalCompareArrays(value1.AsArrayValue,
                            value2.AsArrayValue);
                    case DbType.Map:
                        if (value1 is RecordValue)
                        {
                            return value2 is RecordValue
                                ? TotalCompareRecords(value1.AsRecordValue,
                                    value2.AsRecordValue)
                                : 1;
                        }
                        return value2 is RecordValue
                            ? -1
                            : TotalCompareMaps(value1.AsMapValue,
                                value2.AsMapValue);
                    default:
                        // test self-check
                        Assert.Fail(
                            "Comparison not handled for types " +
                            $"{value1.GetType().Name} and " +
                            value2.GetType().Name);
                        return 0; // not reached
                }
            }
        }
    }
}
