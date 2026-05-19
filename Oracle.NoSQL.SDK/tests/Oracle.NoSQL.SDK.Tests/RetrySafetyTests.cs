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
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RetrySafetyTests : TestBase
    {
        private const string TableName = "RetrySafetyTable";
        private const string RegionId = "us-ashburn-1";

        private static readonly Exception NetworkException =
            new HttpRequestException("connection closed");

        private static NoSQLClient MakeClient() =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "localhost:8080"
            });

        private static MapValue MakeKey() => new MapValue
        {
            ["id"] = 1
        };

        private static MapValue MakeRow() => new MapValue
        {
            ["id"] = 1,
            ["name"] = "retry"
        };

        private static RowVersion MakeVersion() =>
            new RowVersion(new byte[] { 1, 2, 3 });

        private static bool ShouldRetry(Request request, Exception ex)
        {
            request.AddException(ex);
            return new NoSQLRetryHandler().ShouldRetry(request);
        }

        private static PreparedStatement MakePreparedStatement(
            sbyte operationCode) =>
            new PreparedStatement
            {
                OperationCode = operationCode
            };

        public static IEnumerable<object[]> SafeNetworkRetryRequests
        {
            get
            {
                var client = MakeClient();

                yield return new object[]
                {
                    "Get",
                    new GetRequest<RecordValue>(client, TableName,
                        MakeKey(), null)
                };
                yield return new object[]
                {
                    "GetTable",
                    new GetTableRequest(client, TableName, null)
                };
                yield return new object[]
                {
                    "GetTableUsage",
                    new GetTableUsageRequest(client, TableName, null)
                };
                yield return new object[]
                {
                    "GetIndexes",
                    new GetIndexesRequest(client, TableName, null)
                };
                yield return new object[]
                {
                    "GetIndex",
                    new GetIndexRequest(client, TableName, "idx", null)
                };
                yield return new object[]
                {
                    "GetReplicaStats",
                    new GetReplicaStatsRequest(client, TableName, RegionId,
                        null)
                };
                yield return new object[]
                {
                    "ListTables",
                    new ListTablesRequest(client, null)
                };
                yield return new object[]
                {
                    "Prepare",
                    new PrepareRequest(client, $"SELECT * FROM {TableName}",
                        null)
                };
                yield return new object[]
                {
                    "PreparedSelectQuery",
                    new QueryRequest<RecordValue>(client,
                        MakePreparedStatement(
                            QueryRequest.OperationCodeSelect), null)
                };
                yield return new object[]
                {
                    "AdminStatus",
                    new AdminStatusRequest(client, new AdminResult(client))
                };
                yield return new object[]
                {
                    "AdminList",
                    new AdminListRequest(client,
                        "SHOW AS JSON USERS".ToCharArray(), null)
                };
            }
        }

        public static IEnumerable<object[]> UnsafeNetworkRetryRequests
        {
            get
            {
                var client = MakeClient();

                yield return new object[]
                {
                    "Put",
                    new PutRequest<RecordValue>(client, TableName,
                        MakeRow(), null)
                };
                yield return new object[]
                {
                    "PutIfAbsent",
                    new PutIfAbsentRequest<RecordValue>(client, TableName,
                        MakeRow(), null)
                };
                yield return new object[]
                {
                    "PutIfPresent",
                    new PutIfPresentRequest<RecordValue>(client, TableName,
                        MakeRow(), null)
                };
                yield return new object[]
                {
                    "PutIfVersion",
                    new PutIfVersionRequest<RecordValue>(client, TableName,
                        MakeRow(), MakeVersion(), null)
                };
                yield return new object[]
                {
                    "Delete",
                    new DeleteRequest<RecordValue>(client, TableName,
                        MakeKey(), null)
                };
                yield return new object[]
                {
                    "DeleteIfVersion",
                    new DeleteIfVersionRequest<RecordValue>(client,
                        TableName, MakeKey(), MakeVersion(), null)
                };
                yield return new object[]
                {
                    "DeleteRange",
                    new DeleteRangeRequest(client, TableName, MakeKey())
                };
                yield return new object[]
                {
                    "WriteMany",
                    new WriteManyRequest<RecordValue>(client, TableName,
                        new WriteOperationCollection().AddPut(MakeRow()),
                        null)
                };
                yield return new object[]
                {
                    "TableDDL",
                    new TableDDLRequest(client,
                        $"CREATE TABLE {TableName}(id INTEGER, " +
                        "PRIMARY KEY(id))", null)
                };
                yield return new object[]
                {
                    "TableLimits",
                    new TableLimitsRequest(client, TableName,
                        new TableLimits(1, 1, 1), null)
                };
                yield return new object[]
                {
                    "TableTags",
                    new TableTagsRequest(client, TableName, null,
                        new Dictionary<string, string>
                        {
                            ["owner"] = "retry-safety"
                        }, null)
                };
                yield return new object[]
                {
                    "AddReplica",
                    new AddReplicaRequest(client, TableName, RegionId)
                };
                yield return new object[]
                {
                    "DropReplica",
                    new DropReplicaRequest(client, TableName, RegionId)
                };
                yield return new object[]
                {
                    "Admin",
                    new AdminRequest(client, "CREATE USER test".ToCharArray(),
                        null)
                };
                yield return new object[]
                {
                    "UnpreparedSelectQuery",
                    new QueryRequest<RecordValue>(client,
                        $"SELECT * FROM {TableName}", null)
                };
                yield return new object[]
                {
                    "PreparedUpdateQuery",
                    new QueryRequest<RecordValue>(client,
                        MakePreparedStatement(
                            (sbyte)(QueryRequest.OperationCodeSelect + 1)),
                        null)
                };
            }
        }

        [TestMethod]
        [DynamicData(nameof(SafeNetworkRetryRequests))]
        public void ShouldRetrySafeRequestsOnNetworkException(string name,
            Request request)
        {
            Assert.IsTrue(ShouldRetry(request, NetworkException), name);
        }

        [TestMethod]
        [DynamicData(nameof(UnsafeNetworkRetryRequests))]
        public void ShouldNotRetryUnsafeRequestsOnNetworkException(
            string name, Request request)
        {
            Assert.IsFalse(ShouldRetry(request, NetworkException), name);
        }

        [TestMethod]
        public void ShouldStillRetrySecurityInfoNotReadyForUnsafeRequests()
        {
            using var client = MakeClient();
            var request = new PutRequest<RecordValue>(client, TableName,
                MakeRow(), null);

            Assert.IsTrue(ShouldRetry(request,
                new SecurityInfoNotReadyException()));
        }

        [TestMethod]
        public void ShouldStillRetryServerSideRetryableExceptionForWrites()
        {
            using var client = MakeClient();
            var request = new PutRequest<RecordValue>(client, TableName,
                MakeRow(), null);

            Assert.IsTrue(ShouldRetry(request,
                new WriteThrottlingException()));
        }

        [TestMethod]
        public async Task ShouldBlockUnsafeNetworkRetryBeforeCustomHandler()
        {
            var retryHandler = new AlwaysRetryHandler();
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:1",
                Timeout = TimeSpan.FromSeconds(1),
                RetryHandler = retryHandler
            });
            var request = new PutRequest<RecordValue>(client, TableName,
                MakeRow(), null);

            await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => client.ExecuteRequestAsync(request, default));
            Assert.AreEqual(0, retryHandler.ShouldRetryCalls);
            Assert.AreEqual(1, request.RetryCount);
        }

        [TestMethod]
        public void PreparedQueryWriteClassificationMatchesOperationCode()
        {
            using var client = MakeClient();
            var selectRequest = new QueryRequest<RecordValue>(client,
                MakePreparedStatement(QueryRequest.OperationCodeSelect),
                null);
            var writeRequest = new QueryRequest<RecordValue>(client,
                MakePreparedStatement(
                    (sbyte)(QueryRequest.OperationCodeSelect + 1)), null);

            Assert.IsFalse(selectRequest.DoesWrites);
            Assert.IsTrue(writeRequest.DoesWrites);
            Assert.IsTrue(selectRequest.CanRetryOnNetworkException);
            Assert.IsFalse(writeRequest.CanRetryOnNetworkException);
        }

        private class AlwaysRetryHandler : IRetryHandler
        {
            internal int ShouldRetryCalls { get; private set; }

            public bool ShouldRetry(Request request)
            {
                ShouldRetryCalls++;
                return true;
            }

            public TimeSpan GetRetryDelay(Request request) => TimeSpan.Zero;
        }
    }
}
