/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
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
        private static readonly DataTestFixture ParentFixture =
            new DataTestFixture(AllTypesTable,
                new AllTypesRowFactory(100), 20);

        private static readonly DataTestFixture ChildFixture =
            new DataTestFixture(AllTypesChildTable,
                new AllTypesChildRowFactory(ParentFixture.RowFactory), 30);

        private static readonly DataRow GoodRow =
            ParentFixture.GetRow(ParentFixture.GetRowIdFromShard(0));
        private static readonly DataRow GoodRow1 =
            ParentFixture.GetRow(ParentFixture.GetRowIdFromShard(0, 1));
        private static readonly DataRow GoodRowShard2 =
            ParentFixture.MakeRowFromShard(1);

        private static readonly MapValue GoodPK = MakePrimaryKey(
            ParentFixture.Table, GoodRow);
        private static readonly MapValue GoodPK1 = MakePrimaryKey(
            ParentFixture.Table, GoodRow1);
        private static readonly MapValue GoodPKShard2 = MakePrimaryKey(
            ParentFixture.Table, GoodRowShard2);

        private static readonly IWriteOperation GoodPutOp =
            new PutOperation(ParentFixture.Table.Name, GoodRow, null, false);
        private static readonly IWriteOperation GoodPutOp1 =
            new PutOperation(ParentFixture.Table.Name,GoodRow1, null, false);
        private static readonly IWriteOperation GoodPutOpShard2 =
            new PutIfAbsentOperation(ParentFixture.Table.Name,GoodRowShard2,
                new PutOptions
                {
                    TTL = TimeToLive.OfDays(3)
                }, true);

        private static readonly IWriteOperation GoodDeleteOp =
            new DeleteOperation(ParentFixture.Table.Name, GoodPK, null,
                false);
        private static readonly IWriteOperation GoodDeleteOp1 =
            new DeleteOperation(ParentFixture.Table.Name, GoodPK1, null,
                false);
        private static readonly IWriteOperation GoodDeleteOpShard2 =
            new DeleteOperation(ParentFixture.Table.Name, GoodPKShard2, null,
                true);

        private static readonly IWriteOperation[] GoodWriteOps =
            {GoodPutOp, GoodDeleteOp1};

        private static readonly IEnumerable<PutOptions> BadPutOpOptions =
            GetBaseBadPutOptions<PutOptions>();

        private static readonly IEnumerable<DeleteOptions>
            BadDeleteOpOptions =
            GetBaseBadDeleteOptions<DeleteOptions>();

        private static readonly IEnumerable<WriteManyOptions>
            BadWriteManyOptions =
                Enumerable.Empty<WriteManyOptions>()
                    .Concat(from timeout in BadTimeSpans
                        select new WriteManyOptions
                        {
                            Timeout = timeout
                        })
                    .Concat(from durability in BadDurabilities
                        select new WriteManyOptions
                        {
                            Durability = durability
                        });

        private static readonly IEnumerable<PutManyOptions>
            BadPutManyOptions = GetBadPutOptions<PutManyOptions>();

        private static readonly IEnumerable<DeleteManyOptions>
            BadDeleteManyOptions = GetBadDeleteOptions<DeleteManyOptions>();

        private static IEnumerable<IWriteOperation> BadWriteManyOps =>
            Enumerable.Empty<IWriteOperation>()
                .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                    select new PutOperation(ParentFixture.Table.Name, badRow,
                        null, false))
                .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                    select new PutIfAbsentOperation(ParentFixture.Table.Name,
                        badRow, new PutOptions(), true))
                .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                    select new PutIfPresentOperation(ParentFixture.Table.Name,
                        badRow, null, false))
                .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                    select new PutIfVersionOperation(ParentFixture.Table.Name,
                        badRow, GoodRow.Version, null, true))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutOperation(ParentFixture.Table.Name, GoodRow,
                        badOpt, false))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfAbsentOperation(ParentFixture.Table.Name,
                        GoodRow, badOpt, false))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfPresentOperation(ParentFixture.Table.Name,
                        GoodRow, badOpt, true))
                .Concat(from badOpt in BadPutOpOptions
                    select new PutIfVersionOperation(ParentFixture.Table.Name,
                        GoodRow, GoodRow.Version, badOpt, false))
                .Append(new PutIfVersionOperation(ParentFixture.Table.Name,
                    GoodRow, null, null, false))
                .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table,
                        GoodPK)
                    select new DeleteOperation(ParentFixture.Table.Name,
                        badPK, null, false))
                .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table,
                        GoodPK)
                    select new DeleteOperation(ParentFixture.Table.Name, badPK,
                        new DeleteOptions
                        {
                            MatchVersion = GoodRow.Version
                        }, false))
                .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table,
                        GoodPK)
                    select new DeleteIfVersionOperation(
                        ParentFixture.Table.Name, badPK, GoodRow.Version,
                        null, false))
                .Concat(from badOpt in BadDeleteOpOptions
                    select new DeleteOperation(ParentFixture.Table.Name,
                        GoodPK, badOpt, false))
                .Concat(from badOpt in BadDeleteOpOptions
                    select new DeleteIfVersionOperation(
                        ParentFixture.Table.Name, GoodPK, GoodRow.Version,
                        badOpt, true));

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
            .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                select new[] {badRow})
            .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                select new[] {GoodRow, badRow})
            .Concat(from badRow in GetBadRows(ParentFixture.Table, GoodRow)
                select new[] {GoodRow, badRow, GoodRow1})
            .Append(new MapValue[] {GoodRow, GoodRow})
            .Append(new MapValue[] {GoodRow1, GoodRowShard2});

        // for DeleteMany
        private static IEnumerable<IEnumerable<MapValue>> BadPrimaryKeyLists =>
            Enumerable.Empty<IEnumerable<MapValue>>()
            .Append(null)
            .Append(Enumerable.Empty<MapValue>())
            .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table, GoodPK)
                select new[] {badPK})
            .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table, GoodPK)
                select new[] {GoodPK, badPK})
            .Concat(from badPK in GetBadPrimaryKeys(ParentFixture.Table, GoodPK)
                select new[] {badPK, GoodPK1, GoodPK})
            .Append(new[] {GoodPK, GoodPK})
            .Append(new[] {GoodPK1, GoodPKShard2});

        private static IEnumerable<object[]> WriteManyNegativeDataSource =>
            (from badOpList in BadWriteManyOpLists
                select new object[]
                {
                    ParentFixture.Table.Name,
                    badOpList,
                    null
                })
            .Concat(
                from badOpt in BadWriteManyOptions
                select new object[]
                {
                    ParentFixture.Table.Name,
                    GoodWriteOps,
                    badOpt
                })
            .Concat(from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    GoodWriteOps,
                    null
                });

        private static IEnumerable<object[]>
            MultiTableWriteManyNegativeDataSource =>
            (from badOpList in BadWriteManyOpLists
                select new object[]
                {
                    badOpList,
                    null
                })
            .Concat(
                from badOpt in BadWriteManyOptions
                select new object[]
                {
                    GoodWriteOps,
                    badOpt
                })
            .Concat(from tableName in BadTableNames
                select new object[]
                {
                    new[]
                    {
                        new PutOperation(ParentFixture.Table.Name, GoodRow,
                            null, false),
                        new PutOperation(tableName, GoodRow, null, false)
                    },
                    null
                })
            .Concat(from tableName in BadTableNames
                select new object[]
                {
                    new IWriteOperation[]
                    {
                        new DeleteOperation(ParentFixture.Table.Name, GoodPK,
                            null, false),
                        new DeleteOperation(tableName, GoodPK, null, false),
                        new PutOperation(ParentFixture.Table.Name, GoodRow,
                            null, true)
                    },
                    null
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
                    ParentFixture.Table.Name,
                    badRowList,
                    null
                })
            .Concat(
                from badOpt in BadPutManyOptions
                select new object[]
                {
                    ParentFixture.Table.Name,
                    new [] { GoodRow, GoodRow1 },
                    badOpt
                })
            .Append(new object[]
            {
                // PutMany with ExactMatch and extra field in the row value
                ParentFixture.Table.Name,
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
                    ParentFixture.Table.Name,
                    badPKList,
                    null
                })
            .Concat(
                from badOpt in BadDeleteManyOptions
                select new object[]
                {
                    ParentFixture.Table.Name,
                    new[] {GoodPK, GoodPK1},
                    badOpt
                });

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
        public async Task TestWriteManyDuplicateTableAsync()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                client.WriteManyAsync(ParentFixture.Table.Name,
                    new WriteOperationCollection().AddPut(
                        ParentFixture.Table.Name, GoodRow)));
        }

        [DataTestMethod]
        [DynamicData(nameof(MultiTableWriteManyNegativeDataSource))]
        public async Task TestMultiTableWriteManyNegativeAsync(
            IEnumerable<IWriteOperation> ops, WriteManyOptions options)
        {
            CheckSupportsMultiTable();
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.WriteManyAsync(MakeWriteManyCollection(ops, true),
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
        public async Task TestMultiTableWriteManyNonExistentTableAsync()
        {
            CheckSupportsMultiTable();
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.WriteManyAsync(
                    new WriteOperationCollection()
                        .AddPut(ParentFixture.Table.Name, GoodRow)
                        .AddPut("noSuchTable", GoodRow1)));
        }

        [TestMethod]
        public async Task TestWriteManyBatchNumberLimitAsync()
        {
            CheckNotOnPrem();

            var woc = MakeWriteManyCollection(
                from rowIndex in Enumerable.Range(0,
                    WriteManyRequest.MaxOpCount + 1)
                select new PutOperation(null, ParentFixture.MakeRowFromShard(
                    0, rowIndex), null, false));
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(ParentFixture.Table.Name,
                        woc));

            woc = MakeWriteManyCollection(from rowIndex in Enumerable.Range(0,
                    WriteManyRequest.MaxOpCount + 1)
                select new DeleteOperation(null, MakePrimaryKey(
                        ParentFixture.Table,
                        ParentFixture.MakeRowFromShard(0, rowIndex)), null,
                    false));
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(ParentFixture.Table.Name, woc));
        }

        [TestMethod]
        public async Task TestMultiTableWriteManyBatchNumberLimitAsync()
        {
            CheckSupportsMultiTable();
            CheckNotOnPrem();

            var woc = MakeWriteManyCollection(
                from rowIndex in Enumerable.Range(0,
                    WriteManyRequest.MaxOpCount + 1)
                select new PutOperation(ParentFixture.Table.Name,
                    ParentFixture.MakeRowFromShard(0, rowIndex), null,
                    false), true);
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(woc));

            woc = MakeWriteManyCollection(from rowIndex in Enumerable.Range(
                    0, WriteManyRequest.MaxOpCount + 1)
                select new DeleteOperation(ParentFixture.Table.Name,
                    MakePrimaryKey(ParentFixture.Table,
                        ParentFixture.MakeRowFromShard(0, rowIndex)), null,
                    false), true);
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.WriteManyAsync(woc));
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
                    WriteManyRequest.MaxOpCount + 1)
                select ParentFixture.MakeRowFromShard(0, rowIndex)).ToList();
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.PutManyAsync(ParentFixture.Table.Name, rows));
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
                    WriteManyRequest.MaxOpCount + 1)
                select MakePrimaryKey(ParentFixture.Table,
                    ParentFixture.MakeRowFromShard(0, pkIndex))).ToList();
            await Assert
                .ThrowsExceptionAsync<BatchOperationNumberLimitException>(
                    () => client.DeleteManyAsync(ParentFixture.Table.Name,
                        primaryKeys));
        }

    }
}
