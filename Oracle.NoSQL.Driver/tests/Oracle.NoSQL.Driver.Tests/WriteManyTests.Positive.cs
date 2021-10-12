/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;

    public partial class WriteManyTests
    {
        public abstract class TestCase
        {
            internal string Description { get; }

            internal bool Success { get; }

            internal Func<int, bool> ShouldFail { get; }

            internal TestCase(string description, bool success,
                Func<int, bool> shouldFail)
            {
                Description = description ?? "WriteMany TestCase";
                Success = success;
                ShouldFail = shouldFail;
            }

            internal abstract WriteManyTestCase ToWriteManyTestCase();

            public override string ToString() => $"TestCase: {Description}";
        }

        public class WriteManyTestCase : TestCase
        {
            internal List<IWriteOperation> Ops { get; }

            internal WriteManyOptions Options { get; }

            internal WriteManyTestCase(string description,
                IEnumerable<IWriteOperation> ops, bool success = true,
                WriteManyOptions options = null,
                Func<int, bool> shouldFail = null) :
                base(description, success, shouldFail)
            {
                Ops = ops.ToList();
                Options = options;
            }

            internal override WriteManyTestCase ToWriteManyTestCase() => this;
        }

        public class PutManyTestCase : TestCase
        {
            internal List<MapValue> Rows { get; }

            internal PutManyOptions Options { get; }

            internal PutManyTestCase(string description,
                IEnumerable<MapValue> rows, bool success = true,
                PutManyOptions options = null,
                Func<int, bool> shouldFail = null) :
                base(description, success, shouldFail)
            {
                Rows = rows.ToList();
                Options = options;
            }

            internal override WriteManyTestCase ToWriteManyTestCase() =>
                new WriteManyTestCase(Description,
                    from row in Rows
                    select new PutOperation(row, Options,
                        Options.AbortIfUnsuccessful),
                    Success,
                    new WriteManyOptions
                    {
                        Compartment = Options.Compartment,
                        Timeout = Options.Timeout,
                        AbortIfUnsuccessful = Options.AbortIfUnsuccessful
                    },
                    ShouldFail);
        }

        public class DeleteManyTestCase : TestCase
        {
            internal List<MapValue> PrimaryKeys { get; }

            internal DeleteManyOptions Options { get; }

            internal DeleteManyTestCase(string description,
                IEnumerable<MapValue> primaryKeys, bool success = true,
                DeleteManyOptions options = null,
                Func<int, bool> shouldFail = null) :
                base(description, success, shouldFail)
            {
                PrimaryKeys = primaryKeys.ToList();
                Options = options;
            }

            internal override WriteManyTestCase ToWriteManyTestCase() =>
                new WriteManyTestCase(Description,
                    from primaryKey in PrimaryKeys
                    select new DeleteOperation(primaryKey, Options,
                        Options.AbortIfUnsuccessful),
                    Success,
                    new WriteManyOptions
                    {
                        Compartment = Options.Compartment,
                        Timeout = Options.Timeout,
                        AbortIfUnsuccessful = Options.AbortIfUnsuccessful
                    },
                    ShouldFail);

        }

        private static int GetRowId(int? fromStart = null,
            int? fromEnd = null)
        {
            // test self-checks
            Assert.IsTrue(fromStart.HasValue || fromEnd.HasValue);
            Assert.IsFalse(fromStart.HasValue && fromEnd.HasValue);

            return fromStart.HasValue ?
                (Fixture.RowIdStart + fromStart).Value :
                (Fixture.RowIdEnd + fromEnd).Value;
        }

        private static MapValue MakeRow(int? fromStart = null,
            int? fromEnd = null) =>
            Fixture.MakeModifiedRow(Fixture.MakeRow(GetRowId(fromStart,
                fromEnd)));

        private static MapValue MakePK(int? fromStart = null,
            int? fromEnd = null) => MakeDataPK(Fixture.Table,
            Fixture.MakeRow(GetRowId(fromStart, fromEnd)));

        private static IWriteOperation MakePut(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutOperation(MakeRow(fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePutIfAbsent(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfAbsentOperation(MakeRow(fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePutIfPresent(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfPresentOperation(MakeRow(fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePutIfVersion(
            RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfVersionOperation(MakeRow(fromStart, fromEnd),
                matchVersion, options, abortIfUnsuccessful);

        private static IWriteOperation MakeDelete(int? fromStart = null,
            int? fromEnd = null, DeleteOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new DeleteOperation(MakePK(fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakeDeleteIfVersion(
            RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            DeleteOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new DeleteIfVersionOperation(MakePK(fromStart, fromEnd),
                matchVersion, options, abortIfUnsuccessful);

        private static RowVersion GetMatchVersion(int? fromStart = null,
            int? fromEnd = null) =>
            Fixture.GetRow(GetRowId(fromStart, fromEnd)).Version;

        private static IEnumerable<TestCase> TestCases()
        {
            yield return new WriteManyTestCase(
                "put even, delete odd, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0 ?
                    MakePut(fromStart) : MakeDelete(fromStart));

            yield return new WriteManyTestCase("one put, new row, success",
                new[]
                {
                    MakePut(1, options: new PutOptions
                    {
                        ExactMatch = true
                    })
                });

            yield return new WriteManyTestCase(
                "one delete, existing row, success",
                new[]
                {
                    MakeDelete(1)
                });

            yield return new WriteManyTestCase(
                "two puts, one ifAbsent and abortOnFail, fail",
                new[]
                {
                    MakePutIfAbsent(0, abortIfUnsuccessful: true),
                    MakePut(fromEnd: 0)
                }, false);

            yield return new PutManyTestCase(
                "put 10 new, abortOnFail in opt, success",
                from fromEnd in Enumerable.Range(0, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    AbortIfUnsuccessful = true,
                    Timeout = TimeSpan.FromSeconds(20),
                    Compartment = Compartment
                });

            yield return new DeleteManyTestCase(
                "delete 10, abortOnFail in opt, success",
                from fromStart in Enumerable.Range(0, 10)
                select MakePK(fromStart),
                true,
                new DeleteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new DeleteManyTestCase(
                "delete 10 past the end, abortOnFail in opt, fail",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd),
                false,
                new DeleteManyOptions
                {
                    Compartment = Compartment,
                    AbortIfUnsuccessful = true
                });

            yield return new DeleteManyTestCase(
                "delete 10 past the end, abortOnFail not set, success",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd), true, new DeleteManyOptions
                {
                    Timeout = TimeSpan.FromSeconds(15)
                },
                idx => idx >= 4);

            yield return new WriteManyTestCase(
                "ifPresent: true, no updates, success, returnExisting",
                from fromEnd in Enumerable.Range(0, 5)
                select MakePut(null, fromEnd, new PutOptions
                {
                    IfPresent = true,
                    ReturnExisting = true
                }), true, new WriteManyOptions(),
                idx => true);

            yield return new WriteManyTestCase(
                "PutIfPresent, abortOnFail overrides opt, fail, " +
                "returnExisting: true",
                from fromEnd in Enumerable.Range(0, 5)
                select MakePutIfPresent(null, fromEnd, new PutOptions
                {
                    ReturnExisting = true
                }, fromEnd >= 3),
                false,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = false,
                    Timeout = TimeSpan.FromSeconds(30)
                });

            yield return new WriteManyTestCase(
                "PutIfPresent, abortOnFail true on last, fail, " +
                "returnExisting: true",
                from fromEnd in Enumerable.Range(0, 7)
                select MakePut(null, fromEnd, new PutOptions
                {
                    IfPresent = fromEnd == 6,
                    ReturnExisting = true
                }, fromEnd == 6),
                false);

            yield return new PutManyTestCase(
                "putMany, ifPresent: true in opt, no updates, success",
                from fromEnd in Enumerable.Range(0, 5)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfPresent = true,
                    Compartment = Compartment
                },
                idx => true);

            yield return new PutManyTestCase(
                "putMany, ifPresent: true in opt, over rowIdEnd boundary, " +
                "some updates, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfPresent = true,
                    ExactMatch = false
                },
                idx => idx >= 5);

            yield return new PutManyTestCase(
                "putMany, ifAbsent and returnExisting are true in opt, " +
                "over rowIdEnd boundary, some updates, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfAbsent = true,
                    ReturnExisting = true
                },
                idx => idx < 5);

            yield return new WriteManyTestCase(
                "put even, delete odd with correct matchVersion, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0 ?
                    MakePutIfVersion(GetMatchVersion(fromStart), fromStart) :
                    MakeDeleteIfVersion(GetMatchVersion(fromStart),
                        fromStart));

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion of row 5 in opt, " +
                "1 update, success",
                from fromStart in Enumerable.Range(0, 8)
                select MakeRow(fromStart),
                true,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(5)
                },
                idx => idx != 5);

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion and returnExisting in " +
                "opt, no updates, success",
                from fromStart in Enumerable.Range(1, 7)
                select MakeRow(fromStart),
                true,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(0),
                    ReturnExisting = true
                },
                idx => true);

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion, returnExisting and " +
                "abortOnFail in opt, no updates, fail",
                from fromStart in Enumerable.Range(1, 7)
                select MakeRow(fromStart),
                false,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(0),
                    ReturnExisting = true,
                    AbortIfUnsuccessful = true
                },
                idx => true);

            yield return new WriteManyTestCase(
                "put with different TTLs followed by delete, success",
                (from fromStart in Enumerable.Range(0, 5)
                select MakePut(fromStart, options: new PutOptions
                {
                    TTL = TimeToLive.OfDays(fromStart + 1)
                }))
                .Concat(
                    from fromStart in Enumerable.Range(5, 5)
                    select MakeDelete(fromStart, options: new DeleteOptions
                    {
                        MatchVersion = GetMatchVersion(fromStart)
                    })),
                true,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new PutManyTestCase(
                "putMany, across rowIdEnd, same TTL in opt, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    TTL = TimeToLive.OfHours(10)
                });
        }

        private static IEnumerable<object[]> WriteManyDataSource =>
            from testCase in TestCases()
            select new object[]
            {
                Fixture.Table.Name,
                testCase.ToWriteManyTestCase()
            };

        private static IEnumerable<object[]> PutManyDataSource =>
            from testCase in TestCases() where testCase is PutManyTestCase
            select new object[]
            {
                Fixture.Table.Name,
                testCase
            };

        private static IEnumerable<object[]> DeleteManyDataSource =>
            from testCase in TestCases()
            where testCase is DeleteManyTestCase
            select new object[]
            {
                Fixture.Table.Name,
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

        private static async Task VerifyWriteManyAsync(
            WriteManyResult<RecordValue> result, WriteOperationCollection woc,
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
                Assert.AreEqual(woc.Count, result.Results.Count);

                var idx = 0;
                var successCount = 0;
                var readCount = 0;

                foreach (var op in woc)
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
                            Fixture.Table, row, putOp.Options, opSuccess,
                            Fixture.GetRow(row.Id, true), true,
                            putOp.GetType() != typeof(PutOperation));
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
                            Fixture.Table, primaryKey, deleteOp.Options,
                            opSuccess, Fixture.GetRow(primaryKey.Id, true),
                            true,
                            deleteOp.GetType() != typeof(DeleteOperation));
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
                Assert.IsTrue(result.FailedOperationIndex <= woc.Count);
                Assert.IsNotNull(result.FailedOperationResult);

                var op = woc.ToArray()[result.FailedOperationIndex.Value];
                Assert.IsTrue(
                    op.AbortIfUnsuccessful ||
                    options != null && options.AbortIfUnsuccessful);

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
                        Fixture.Table, row, putOp.Options, false,
                        Fixture.GetRow(row.Id, true), true,
                        putOp.GetType() != typeof(PutOperation));
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
                        Fixture.Table, primaryKey, deleteOp.Options,
                        false, Fixture.GetRow(primaryKey.Id, true),
                        true, deleteOp.GetType() != typeof(DeleteOperation));
                }

                // Verify that no rows has been affected by the operation.
                foreach(var op1 in woc)
                {
                    MapValue primaryKey;
                    int rowId;
                    if (op1 is PutOperation putOp1)
                    {
                        // test self-check
                        Assert.IsTrue(putOp1.Row is DataRow);
                        var row = (DataRow)putOp1.Row;
                        primaryKey = MakePrimaryKey(Fixture.Table, row);
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

                    var getResult = await client.GetAsync(Fixture.Table.Name,
                        primaryKey);
                    VerifyGetResult(getResult, Fixture.Table,
                        Fixture.GetRow(rowId, true));
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

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (currentCollection != null)
            {
                foreach (var op in currentCollection)
                {
                    if (op is PutOperation putOp)
                    {
                        // test self-check
                        Assert.IsTrue(putOp.Row is DataRow);
                        var dataRow = (DataRow)putOp.Row;
                        var originalRow = Fixture.GetRow(dataRow.Id, true);
                        if (originalRow != null) // it was Put on existing row
                        {
                            await PutRowAsync(Fixture.Table, originalRow);
                        }
                        else // it was Put on new row
                        {
                            await DeleteRowAsync(Fixture.Table, dataRow);
                        }
                    }
                    else
                    {
                        Assert.IsTrue(op is DeleteOperation);
                        var deleteOp = (DeleteOperation)op;
                        Assert.IsTrue(deleteOp.PrimaryKey is DataPK);
                        var dataPK = (DataPK)deleteOp.PrimaryKey;
                        var originalRow = Fixture.GetRow(dataPK.Id, true);
                        if (originalRow != null)
                        {
                            // it was Delete on existing row
                            await PutRowAsync(Fixture.Table, originalRow);
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
