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
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public partial class WriteManyTests : DataTestBase<WriteManyTests>
    {
        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(100), 20);

        private static readonly DataRow GoodRow =
            Fixture.GetRow(Fixture.GetRowIdFromShard(0));
        private static readonly DataRow GoodRow1 =
            Fixture.GetRow(Fixture.GetRowIdFromShard(0, 1));
        private static readonly DataRow GoodRowShard2 =
            Fixture.MakeRowFromShard(1);

        private static readonly MapValue GoodPK = MakePrimaryKey(
            Fixture.Table, GoodRow);
        private static readonly MapValue GoodPK1 = MakePrimaryKey(
            Fixture.Table, GoodRow1);
        private static readonly MapValue GoodPKShard2 = MakePrimaryKey(
            Fixture.Table, GoodRowShard2);

        private static readonly IWriteOperation GoodPutOp =
            new PutOperation(GoodRow, null, false);
        private static readonly IWriteOperation GoodPutOp1 =
            new PutOperation(GoodRow1, null, false);
        private static readonly IWriteOperation GoodPutOpShard2 =
            new PutIfAbsentOperation(GoodRowShard2, new PutOptions
            {
                TTL = TimeToLive.OfDays(3)
            }, true);

        private static readonly IWriteOperation GoodDeleteOp =
            new DeleteOperation(GoodPK, null, false);
        private static readonly IWriteOperation GoodDeleteOp1 =
            new DeleteOperation(GoodPK1, null, false);
        private static readonly IWriteOperation GoodDeleteOpShard2 =
            new DeleteOperation(GoodPKShard2, null, true);

        private static readonly IWriteOperation[] GoodWriteOps =
            {GoodPutOp, GoodDeleteOp1};

        private static readonly IEnumerable<PutOptions> BadPutOpOptions =
            GetBaseBadPutOptions<PutOptions>();

        private static readonly IEnumerable<DeleteOptions>
            BadDeleteOpOptions =
            GetBaseBadDeleteOptions<DeleteOptions>();

        private static readonly IEnumerable<WriteManyOptions>
            BadWriteManyOptions =
            from timeout in BadTimeSpans select new WriteManyOptions
            {
                Timeout = timeout
            };

        private static readonly IEnumerable<PutManyOptions>
            BadPutManyOptions = GetBadPutOptions<PutManyOptions>();

        private static readonly IEnumerable<DeleteManyOptions>
            BadDeleteManyOptions = GetBadDeleteOptions<DeleteManyOptions>();

        private static IEnumerable<IWriteOperation> BadWriteManyOps =>
            Enumerable.Empty<IWriteOperation>()
                .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                    select new PutOperation(badRow, null, false))
                .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                    select new PutIfAbsentOperation(badRow, new PutOptions(),
                        true))
                .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                    select new PutIfPresentOperation(badRow, null, false))
                .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                    select new PutIfVersionOperation(badRow, GoodRow.Version,
                        null, true))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutOperation(GoodRow, badOpt, false))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfAbsentOperation(GoodRow, badOpt, false))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfPresentOperation(GoodRow, badOpt, true))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfVersionOperation(GoodRow, GoodRow.Version,
                        badOpt, false))
                .Append(new PutIfVersionOperation(GoodRow, null, null, false))
                .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                    select new DeleteOperation(badPK, null, false))
                .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                    select new DeleteOperation(badPK, new DeleteOptions
                    {
                        MatchVersion = GoodRow.Version
                    }, false))
                .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                    select new DeleteIfVersionOperation(badPK, GoodRow.Version,
                        null, false))
                .Concat(from badOpt in BadDeleteOpOptions
                    select new DeleteOperation(GoodPK, badOpt, false))
                .Concat(from badOpt in BadDeleteOpOptions
                    select new DeleteIfVersionOperation(GoodPK,
                        GoodRow.Version, badOpt, true));

        private static IEnumerable<IEnumerable<IWriteOperation>>
            BadWriteManyOpLists =>
            Enumerable.Empty<IEnumerable<IWriteOperation>>()
                // will be tested as (WriteOperationCollection)null
            .Append(null)
            // empty collection
            .Append(Enumerable.Empty<IWriteOperation>())
            .Concat(from badOp in BadWriteManyOps select new[] {badOp})
            .Concat(from badOp in BadWriteManyOps
                select new[] {GoodPutOp, GoodDeleteOp1, badOp})
            .Concat(from badOp in BadWriteManyOps
                select new[] {GoodPutOp1, badOp, GoodDeleteOp})
            // same pk not allowed
            .Append(new[] {GoodPutOp, GoodPutOp})
            // same pk not allowed
            .Append(new[] {GoodPutOp, GoodDeleteOp})
            // same pk not allowed
            .Append(new[] {GoodDeleteOp, GoodPutOp})
            // same pk not allowed
            .Append(new[] {GoodDeleteOp, GoodDeleteOp})
            // rows from different shards
            .Append(new[] {GoodPutOp, GoodPutOpShard2})
            // rows from different shards
            .Append(new[] {GoodPutOp, GoodDeleteOpShard2})
            // rows from different shards
            .Append(new[] {GoodDeleteOpShard2, GoodDeleteOp});

        // for PutMany
        private static IEnumerable<IEnumerable<MapValue>> BadRowLists =>
            Enumerable.Empty<IEnumerable<MapValue>>()
            .Append(null)
            .Append(Enumerable.Empty<MapValue>())
            .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                select new[] {badRow})
            .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                select new[] {GoodRow, badRow})
            .Concat(from badRow in GetBadRows(Fixture.Table, GoodRow)
                select new[] {GoodRow, badRow, GoodRow1})
            .Append(new MapValue[] {GoodRow, GoodRow})
            .Append(new MapValue[] {GoodRow1, GoodRowShard2});

        // for DeleteMany
        private static IEnumerable<IEnumerable<MapValue>> BadPrimaryKeyLists =>
            Enumerable.Empty<IEnumerable<MapValue>>()
            .Append(null)
            .Append(Enumerable.Empty<MapValue>())
            .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                select new[] {badPK})
            .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                select new[] {GoodPK, badPK})
            .Concat(from badPK in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                select new[] {badPK, GoodPK1, GoodPK})
            .Append(new[] {GoodPK, GoodPK})
            .Append(new[] {GoodPK1, GoodPKShard2});

        private static IEnumerable<object[]> WriteManyNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    GoodWriteOps,
                    null
                })
            .Concat(
                from badOpList in BadWriteManyOpLists
                select new object[]
                {
                    Fixture.Table.Name,
                    badOpList,
                    null
                })
            .Concat(
                from badOpt in BadWriteManyOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    GoodWriteOps,
                    badOpt
                });

        private static IEnumerable<object[]> PutManyNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    new [] { GoodRow, GoodRow1 },
                    null
                })
            .Concat(
                from badRowList in BadRowLists
                select new object[]
                {
                    Fixture.Table.Name,
                    badRowList,
                    null
                })
            .Concat(
                from badOpt in BadPutManyOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    new [] { GoodRow, GoodRow1 },
                    badOpt
                })
            .Append(new object[]
            {
                // PutMany with ExactMatch and extra field in the row value
                Fixture.Table.Name,
                new [] {SetFieldValueInMap(GoodRow, "no_such_field", 1)},
                new PutManyOptions
                {
                    ExactMatch = true
                }
            });

        private static IEnumerable<object[]> DeleteManyNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    new[] {GoodPK, GoodPK1},
                    null
                })
            .Concat(
                from badPKList in BadPrimaryKeyLists
                select new object[]
                {
                    Fixture.Table.Name,
                    badPKList,
                    null
                })
            .Concat(
                from badOpt in BadDeleteManyOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    new[] {GoodPK, GoodPK1},
                    badOpt
                });

        // This method will add the operations to the collection by using its
        // Add methods and thus will test the collection.
        private static WriteOperationCollection MakeWriteManyCollection(
            IEnumerable<IWriteOperation> ops)
        {
            // Simulate null for negative tests.
            if (ops == null)
            {
                return null;
            }

            var woc = new WriteOperationCollection();
            WriteOperationCollection ret = null;

            foreach(var op in ops)
            {
                switch (op)
                {
                    case PutIfAbsentOperation putOp:
                        ret = woc.AddPutIfAbsent(putOp.Row, putOp.Options,
                            putOp.AbortIfUnsuccessful);
                        break;
                    case PutIfPresentOperation putOp:
                        ret = woc.AddPutIfPresent(putOp.Row, putOp.Options,
                            putOp.AbortIfUnsuccessful);
                        break;
                    case PutIfVersionOperation putOp:
                        ret = woc.AddPutIfVersion(putOp.Row,
                            putOp.MatchVersion, putOp.Options,
                            putOp.AbortIfUnsuccessful);
                        break;
                    case PutOperation putOp:
                        ret = woc.AddPut(putOp.Row, putOp.Options,
                            putOp.AbortIfUnsuccessful);
                        break;
                    case DeleteIfVersionOperation deleteOp:
                        ret = woc.AddDeleteIfVersion(deleteOp.PrimaryKey,
                            deleteOp.MatchVersion, deleteOp.Options,
                            deleteOp.AbortIfUnsuccessful);
                        break;
                    case DeleteOperation deleteOp:
                        ret = woc.AddDelete(deleteOp.PrimaryKey,
                            deleteOp.Options, deleteOp.AbortIfUnsuccessful);
                        break;
                    default:
                        Assert.IsNotNull(op); // test self-check
                        Assert.Fail(
                            "Unknown type of IWriteOperation: " +
                            op.GetType().Name);
                        break;
                }

                Assert.IsTrue(ReferenceEquals(woc, ret));
            }

            return woc;
        }

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            await DropTableAsync(Fixture.Table);
            await CreateTableAsync(Fixture.Table);
            await PutRowsAsync(Fixture.Table, Fixture.Rows);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Fixture.Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(WriteManyNegativeDataSource))]
        public async Task TestWriteManyNegativeAsync(string tableName,
            IEnumerable<IWriteOperation> ops, WriteManyOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.WriteManyAsync(tableName, MakeWriteManyCollection(ops),
                    options));
        }

        [TestMethod]
        public async Task TestWriteManyNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.WriteManyAsync("noSuchTable",
                    new WriteOperationCollection().AddPut(GoodRow)));
        }

        [TestMethod]
        public async Task TestWriteManyBatchNumberLimitAsync()
        {
            CheckNotOnPrem();

            var woc = MakeWriteManyCollection(
                from rowIndex in Enumerable.Range(0,
                    WriteManyRequest<RecordValue>.MaxOpCount + 1)
                select new PutOperation(Fixture.MakeRowFromShard(
                    0, rowIndex), null, false));
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(Fixture.Table.Name, woc));

            woc = MakeWriteManyCollection(from rowIndex in Enumerable.Range(0,
                    WriteManyRequest<RecordValue>.MaxOpCount + 1)
                select new DeleteOperation(MakePrimaryKey(Fixture.Table,
                    Fixture.MakeRowFromShard(0, rowIndex)), null, false));
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(Fixture.Table.Name, woc));
        }

        [DataTestMethod]
        [DynamicData(nameof(PutManyNegativeDataSource))]
        public async Task TestPutManyNegativeAsync(string tableName,
            IEnumerable<MapValue> rows, PutManyOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PutManyAsync(tableName, rows.ToArray(), options));
        }

        [TestMethod]
        public async Task TestPutManyNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutManyAsync("noSuchTable", new[] {GoodRow}));
        }

        [TestMethod]
        public async Task TestPutManyBatchNumberLimitAsync()
        {
            CheckNotOnPrem();

            var rows = (from rowIndex in Enumerable.Range(0,
                    WriteManyRequest<RecordValue>.MaxOpCount + 1)
                select Fixture.MakeRowFromShard(0, rowIndex)).ToList();
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.PutManyAsync(Fixture.Table.Name, rows));
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteManyNegativeDataSource))]
        public async Task TestDeleteManyNegativeAsync(string tableName,
            IEnumerable<MapValue> primaryKeys, DeleteManyOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteManyAsync(tableName, primaryKeys.ToArray(),
                    options));
        }

        [TestMethod]
        public async Task TestDeleteManyNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteManyAsync("noSuchTable", new[] { GoodPK }));
        }

        [TestMethod]
        public async Task TestDeleteManyBatchNumberLimitAsync()
        {
            CheckNotOnPrem();

            var primaryKeys = (from pkIndex in Enumerable.Range(0,
                    WriteManyRequest<RecordValue>.MaxOpCount + 1)
                select MakePrimaryKey(Fixture.Table,
                    Fixture.MakeRowFromShard(0, pkIndex))).ToList();
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.DeleteManyAsync(Fixture.Table.Name,
                        primaryKeys));
        }

    }
}
