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
                    select new PutOperation(null, row, Options,
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
                    select new DeleteOperation(null, primaryKey, Options,
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

        private static int GetRowId(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null)
        {
            // test self-checks
            Assert.IsTrue(fromStart.HasValue || fromEnd.HasValue);
            Assert.IsFalse(fromStart.HasValue && fromEnd.HasValue);

            return fromStart.HasValue ?
                (fixture.RowIdStart + fromStart).Value :
                (fixture.RowIdEnd + fromEnd).Value;
        }

        private static MapValue MakeRow(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null) =>
            fixture.MakeModifiedRow(fixture.MakeRow(GetRowId(fixture,
                fromStart, fromEnd)));

        private static MapValue MakeRow(int? fromStart = null,
            int? fromEnd = null) => MakeRow(ParentFixture, fromStart, fromEnd);

        private static MapValue MakePK(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null) =>
            MakeDataPK(fixture.Table, fixture.MakeRow(
                GetRowId(fixture, fromStart, fromEnd)));

        private static MapValue MakePK(int? fromStart = null,
            int? fromEnd = null) => MakePK(ParentFixture, fromStart, fromEnd);

        private static IWriteOperation MakePut(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null,
            PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutOperation(fixture.Table.Name,
                MakeRow(fixture, fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePut(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) => MakePut(ParentFixture,
            fromStart, fromEnd, options, abortIfUnsuccessful);

        private static IWriteOperation MakePutIfAbsent(
            DataTestFixture fixture,int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfAbsentOperation(fixture.Table.Name,
                MakeRow(fixture, fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePutIfAbsent(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) => MakePutIfAbsent(
            ParentFixture, fromStart, fromEnd, options, abortIfUnsuccessful);

        private static IWriteOperation MakePutIfPresent(
            DataTestFixture fixture, int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfPresentOperation(fixture.Table.Name,
                MakeRow(fixture, fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakePutIfPresent(int? fromStart = null,
            int? fromEnd = null, PutOptions options = null,
            bool abortIfUnsuccessful = false) => MakePutIfPresent(
            ParentFixture, fromStart, fromEnd, options, abortIfUnsuccessful);
        
        private static IWriteOperation MakePutIfVersion(
            DataTestFixture fixture, RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            PutOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new PutIfVersionOperation(fixture.Table.Name,
                MakeRow(fixture, fromStart, fromEnd),
                matchVersion, options, abortIfUnsuccessful);

        private static IWriteOperation MakePutIfVersion(
            RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            PutOptions options = null,
            bool abortIfUnsuccessful = false) => MakePutIfVersion(
            ParentFixture, matchVersion, fromStart, fromEnd, options,
            abortIfUnsuccessful);

        private static IWriteOperation MakeDelete(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null,
            DeleteOptions options = null, bool abortIfUnsuccessful = false) =>
            new DeleteOperation(fixture.Table.Name,
                MakePK(fixture, fromStart, fromEnd), options,
                abortIfUnsuccessful);

        private static IWriteOperation MakeDelete(int? fromStart = null,
            int? fromEnd = null, DeleteOptions options = null,
            bool abortIfUnsuccessful = false) => MakeDelete(ParentFixture,
            fromStart, fromEnd, options, abortIfUnsuccessful);

        private static IWriteOperation MakeDeleteIfVersion(
            DataTestFixture fixture, RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            DeleteOptions options = null,
            bool abortIfUnsuccessful = false) =>
            new DeleteIfVersionOperation(fixture.Table.Name,
                MakePK(fixture, fromStart, fromEnd), matchVersion, options,
                abortIfUnsuccessful);

        private static IWriteOperation MakeDeleteIfVersion(
            RowVersion matchVersion,
            int? fromStart = null, int? fromEnd = null,
            DeleteOptions options = null,
            bool abortIfUnsuccessful = false) => MakeDeleteIfVersion(
            ParentFixture, matchVersion, fromStart, fromEnd, options,
            abortIfUnsuccessful);

        private static RowVersion GetMatchVersion(DataTestFixture fixture,
            int? fromStart = null, int? fromEnd = null) =>
            fixture.GetRow(GetRowId(fixture, fromStart, fromEnd)).Version;

        private static RowVersion GetMatchVersion(int? fromStart = null,
            int? fromEnd = null) =>
            GetMatchVersion(ParentFixture, fromStart, fromEnd);

        // This method will add the operations to the collection by using its
        // Add methods and thus will test the collection.
        private static WriteOperationCollection MakeWriteManyCollection(
            IEnumerable<IWriteOperation> ops, bool useOpTableName = false)
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
                // Conditional on useOpTableName so we can test both
                // versions of Add methods (with and without table name).
                switch (op)
                {
                    case PutIfAbsentOperation putOp:
                        ret = useOpTableName
                            ? woc.AddPutIfAbsent(putOp.TableName, putOp.Row,
                                putOp.Options,
                                putOp.AbortIfUnsuccessful)
                            : woc.AddPutIfAbsent(putOp.Row, putOp.Options,
                                putOp.AbortIfUnsuccessful);
                        break;
                    case PutIfPresentOperation putOp:
                        ret = useOpTableName
                            ? woc.AddPutIfPresent(putOp.TableName, putOp.Row,
                                putOp.Options,
                                putOp.AbortIfUnsuccessful)
                            : woc.AddPutIfPresent(putOp.Row, putOp.Options,
                                putOp.AbortIfUnsuccessful);
                        break;
                    case PutIfVersionOperation putOp:
                        ret = useOpTableName
                            ? woc.AddPutIfVersion(putOp.TableName, putOp.Row,
                                putOp.MatchVersion, putOp.Options,
                                putOp.AbortIfUnsuccessful)
                            : woc.AddPutIfVersion(putOp.Row,
                                putOp.MatchVersion, putOp.Options,
                                putOp.AbortIfUnsuccessful);
                        break;
                    case PutOperation putOp:
                        ret = useOpTableName
                            ? woc.AddPut(putOp.TableName, putOp.Row,
                                putOp.Options,
                                putOp.AbortIfUnsuccessful)
                            : woc.AddPut(putOp.Row, putOp.Options,
                                putOp.AbortIfUnsuccessful);
                        break;
                    case DeleteIfVersionOperation deleteOp:
                        ret = useOpTableName
                            ? woc.AddDeleteIfVersion(deleteOp.TableName,
                                deleteOp.PrimaryKey,
                                deleteOp.MatchVersion, deleteOp.Options,
                                deleteOp.AbortIfUnsuccessful)
                            : woc.AddDeleteIfVersion(deleteOp.PrimaryKey,
                                deleteOp.MatchVersion, deleteOp.Options,
                                deleteOp.AbortIfUnsuccessful);
                        break;
                    case DeleteOperation deleteOp:
                        ret = useOpTableName
                            ? woc.AddDelete(deleteOp.TableName,
                                deleteOp.PrimaryKey, deleteOp.Options,
                                deleteOp.AbortIfUnsuccessful)
                            : woc.AddDelete(deleteOp.PrimaryKey,
                                deleteOp.Options,
                                deleteOp.AbortIfUnsuccessful);
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
    }

}
