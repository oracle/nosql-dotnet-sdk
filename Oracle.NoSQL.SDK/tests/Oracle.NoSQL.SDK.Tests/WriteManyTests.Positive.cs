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
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;

    public partial class WriteManyTests
    {
        private static IEnumerable<object[]> WriteManyDataSource =>
            from testCase in TestCases()
            select new object[]
            {
                ParentFixture.Table.Name,
                testCase.ToWriteManyTestCase()
            };

        private static IEnumerable<object[]> MultiTableWriteManyDataSource =>
            from testCase in MultiTableTestCases()
            select new object[] { testCase };

        private static IEnumerable<object[]> PutManyDataSource =>
            from testCase in TestCases() where testCase is PutManyTestCase
            select new object[]
            {
                ParentFixture.Table.Name,
                testCase
            };

        private static IEnumerable<object[]> DeleteManyDataSource =>
            from testCase in TestCases()
            where testCase is DeleteManyTestCase
            select new object[]
            {
                ParentFixture.Table.Name,
                testCase
            };

        private static PutResult<TRow> ToPutResult<TRow>(
            WriteOperationResult<TRow> result) => new PutResult<TRow>
        {
            Success = result.Success,
            Version = result.Version,
            ExistingRow = result.ExistingRow,
            ExistingVersion = result.ExistingVersion,
            GeneratedValue = result.GeneratedValue
        };

        private static DeleteResult<TRow> ToDeleteResult<TRow>(
            WriteOperationResult<TRow> result) => new DeleteResult<TRow>
        {
            Success = result.Success,
            ExistingRow = result.ExistingRow,
            ExistingVersion = result.ExistingVersion
        };

        private static DataTestFixture GetFixture(string tableName) =>
            tableName == ChildFixture.Table.Name ? ChildFixture : ParentFixture;

        private static async Task VerifyWriteManyAsync(
            WriteManyResult<RecordValue> result, WriteOperationCollection oc,
            WriteManyOptions options, bool success,
            Func<int, bool> shouldFail)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(success, result.Success);
            VerifyConsumedCapacity(result.ConsumedCapacity);

            if (success)
            {
                Assert.IsNull(result.FailedOperationIndex);
                Assert.IsNull(result.FailedOperationResult);
                Assert.IsNotNull(result.Results);
                Assert.AreEqual(oc.Count, result.Results.Count);

                var idx = 0;
                var successCount = 0;
                var readCount = 0;

                foreach (var op in oc)
                {
                    Assert.IsNotNull(op);
                    var opSuccess = shouldFail == null || !shouldFail(idx);
                    if (opSuccess)
                    {
                        successCount++;
                    }
                    else
                    {
                        Assert.IsFalse(op.AbortIfUnsuccessful);
                    }

                    var opResult = result.Results[idx];
                    Assert.IsNotNull(opResult);

                    var fixture = GetFixture(op.TableName);

                    if (op is PutOperation putOp)
                    {
                        if (((IPutOp)putOp).PutOpKind != PutOpKind.Always)
                        {
                            readCount++;
                        }

                        // test self-check
                        Assert.IsTrue(putOp.Row is DataRow);
                        var row = (DataRow)putOp.Row;
                        await VerifyPutAsync(ToPutResult(opResult),
                            fixture.Table, row, putOp.Options, opSuccess,
                            fixture.GetRow(row.Id, true), true,
                            putOp.GetType() != typeof(PutOperation),
                            verifyExistingModTime:false);
                    }
                    else
                    {
                        Assert.IsTrue(op is DeleteOperation);
                        var deleteOp = (DeleteOperation)op;
                        if (((IDeleteOp)deleteOp).MatchVersion != null)
                        {
                            readCount++;
                        }

                        // test self-check
                        Assert.IsTrue(deleteOp.PrimaryKey is DataPK);
                        var primaryKey = (DataPK)deleteOp.PrimaryKey;
                        await VerifyDeleteAsync(ToDeleteResult(opResult),
                            fixture.Table, primaryKey, deleteOp.Options,
                            opSuccess, fixture.GetRow(primaryKey.Id, true),
                            true,
                            deleteOp.GetType() != typeof(DeleteOperation),
                            verifyExistingModTime:false);
                    }

                    idx++;
                }

                if (!IsOnPrem)
                {
                    Assert.IsTrue(
                        result.ConsumedCapacity.WriteKB >= successCount);
                    Assert.IsTrue(
                        result.ConsumedCapacity.WriteUnits >= successCount);
                    Assert.IsTrue(
                        result.ConsumedCapacity.ReadKB >= readCount);
                    Assert.IsTrue(
                        result.ConsumedCapacity.ReadUnits >= readCount);
                }
            }
            else
            {
                // Operation fails because of abortIfUnsuccessful option.
                Assert.IsNull(result.Results);
                Assert.IsNotNull(result.FailedOperationIndex);
                Assert.IsTrue(result.FailedOperationIndex >= 0);
                Assert.IsTrue(result.FailedOperationIndex <= oc.Count);
                Assert.IsNotNull(result.FailedOperationResult);

                var op = oc[result.FailedOperationIndex.Value];
                Assert.IsTrue(
                    op.AbortIfUnsuccessful ||
                    options != null && options.AbortIfUnsuccessful);

                var fixture = GetFixture(op.TableName);

                if (op is PutOperation putOp)
                {
                    // Failed Put must be conditional.
                    Assert.AreNotEqual(PutOpKind.Always,
                        ((IPutOp)putOp).PutOpKind);

                    // test self-check
                    Assert.IsTrue(putOp.Row is DataRow);
                    var row = (DataRow)putOp.Row;
                    await VerifyPutAsync(
                        ToPutResult(result.FailedOperationResult),
                        fixture.Table, row, putOp.Options, false,
                        fixture.GetRow(row.Id, true), true,
                        putOp.GetType() != typeof(PutOperation),
                        verifyExistingModTime:false);
                }
                else
                {
                    Assert.IsTrue(op is DeleteOperation);
                    var deleteOp = (DeleteOperation)op;

                    // test self-check
                    Assert.IsTrue(deleteOp.PrimaryKey is DataPK);
                    var primaryKey = (DataPK)deleteOp.PrimaryKey;
                    await VerifyDeleteAsync(
                        ToDeleteResult(result.FailedOperationResult),
                        fixture.Table, primaryKey, deleteOp.Options,
                        false, fixture.GetRow(primaryKey.Id, true),
                        true, deleteOp.GetType() != typeof(DeleteOperation),
                        verifyExistingModTime:false);
                }

                // Verify that no rows has been affected by the operation.
                foreach(var op1 in oc)
                {
                    MapValue primaryKey;
                    int rowId;
                    
                    fixture = GetFixture(op1.TableName);

                    if (op1 is PutOperation putOp1)
                    {
                        // test self-check
                        Assert.IsTrue(putOp1.Row is DataRow);
                        var row = (DataRow)putOp1.Row;
                        primaryKey = MakePrimaryKey(fixture.Table, row);
                        rowId = row.Id;
                    }
                    else
                    {
                        Assert.IsTrue(op1 is DeleteOperation);
                        var deleteOp = (DeleteOperation)op1;
                        // test self-check
                        Assert.IsTrue(deleteOp.PrimaryKey is DataPK);
                        var pk = (DataPK)deleteOp.PrimaryKey;
                        primaryKey = (DataPK)deleteOp.PrimaryKey;
                        rowId = pk.Id;
                    }

                    var getResult = await client.GetAsync(fixture.Table.Name,
                        primaryKey);
                    VerifyGetResult(getResult, fixture.Table,
                        fixture.GetRow(rowId, true));
                }
            }
        }

        private WriteOperationCollection currentCollection;

        // There doesn't seem to be a way to access dynamic data in setup and
        // teardown methods, so we have to do this manually.
        private void SetForCleanup(WriteOperationCollection woc)
        {
            currentCollection = woc;
        }

        private static readonly Version MultiTableVersion =
            new Version("22.3.3");

        private static bool supportsMultiTable;

        private static void CheckSupportsMultiTable()
        {
            if (!supportsMultiTable)
            {
                Assert.Inconclusive(
                    "Multi-table WriteMany is not supported with " +
                    "this KV version");
            }
        }

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);

            supportsMultiTable =
                KVVersion == null || KVVersion >= MultiTableVersion;
            
            if (supportsMultiTable)
            {
                await DropTableAsync(ChildFixture.Table);
            }
            
            await DropTableAsync(ParentFixture.Table);
            await CreateTableAsync(ParentFixture.Table);
            await PutRowsAsync(ParentFixture.Table, ParentFixture.Rows);

            if (supportsMultiTable)
            {
                await CreateTableAsync(ChildFixture.Table);
                await PutRowsAsync(ChildFixture.Table, ChildFixture.Rows);
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            if (supportsMultiTable)
            {
                await DropTableAsync(ChildFixture.Table);
            }

            await DropTableAsync(ParentFixture.Table);
            ClassCleanup();
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (currentCollection != null)
            {
                foreach (var op in currentCollection)
                {
                    var fixture = GetFixture(op.TableName);

                    if (op is PutOperation putOp)
                    {
                        // test self-check
                        Assert.IsTrue(putOp.Row is DataRow);
                        var dataRow = (DataRow)putOp.Row;
                        var originalRow = fixture.GetRow(dataRow.Id, true);
                        if (originalRow != null) // it was Put on existing row
                        {
                            await PutRowAsync(fixture.Table, originalRow);
                        }
                        else // it was Put on new row
                        {
                            await DeleteRowAsync(fixture.Table, dataRow);
                        }
                    }
                    else
                    {
                        Assert.IsTrue(op is DeleteOperation);
                        var deleteOp = (DeleteOperation)op;
                        Assert.IsTrue(deleteOp.PrimaryKey is DataPK);
                        var dataPK = (DataPK)deleteOp.PrimaryKey;
                        var originalRow = fixture.GetRow(dataPK.Id, true);
                        if (originalRow != null)
                        {
                            // it was Delete on existing row
                            await PutRowAsync(fixture.Table, originalRow);
                        }
                        // ignore Delete on non-existing row
                    }
                }

                currentCollection = null;
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(WriteManyDataSource))]
        public async Task TestWriteManyAsync(string tableName,
            WriteManyTestCase testCase)
        {
            var woc = MakeWriteManyCollection(testCase.Ops);
            SetForCleanup(woc);
            var result = await client.WriteManyAsync(tableName, woc,
                testCase.Options);
            await VerifyWriteManyAsync(result, woc, testCase.Options,
                testCase.Success, testCase.ShouldFail);
        }

        [DataTestMethod]
        [DynamicData(nameof(MultiTableWriteManyDataSource))]
        public async Task TestMultiTableWriteManyAsync(
            WriteManyTestCase testCase)
        {
            CheckSupportsMultiTable();
            var woc = MakeWriteManyCollection(testCase.Ops, true);
            SetForCleanup(woc);
            var result = await client.WriteManyAsync(woc, testCase.Options);
            await VerifyWriteManyAsync(result, woc, testCase.Options,
                testCase.Success, testCase.ShouldFail);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutManyDataSource))]
        public async Task TestPutManyAsync(string tableName,
            PutManyTestCase testCase)
        {
            var writeManyTestCase = testCase.ToWriteManyTestCase();
            var woc = MakeWriteManyCollection(writeManyTestCase.Ops);
            SetForCleanup(woc);

            var result = await client.PutManyAsync(tableName,
                testCase.Rows.ToList(), testCase.Options);

            await VerifyWriteManyAsync(result, woc, writeManyTestCase.Options,
                testCase.Success, testCase.ShouldFail);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteManyDataSource))]
        public async Task TestDeleteManyAsync(string tableName,
            DeleteManyTestCase testCase)
        {
            var writeManyTestCase = testCase.ToWriteManyTestCase();
            var woc = MakeWriteManyCollection(writeManyTestCase.Ops);
            SetForCleanup(woc);

            var result = await client.DeleteManyAsync(tableName,
                testCase.PrimaryKeys.ToList(), testCase.Options);

            await VerifyWriteManyAsync(result, woc, writeManyTestCase.Options,
                testCase.Success, testCase.ShouldFail);
        }

    }

}
