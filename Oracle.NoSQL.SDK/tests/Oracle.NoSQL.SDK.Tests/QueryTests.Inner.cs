/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
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

    }
}
