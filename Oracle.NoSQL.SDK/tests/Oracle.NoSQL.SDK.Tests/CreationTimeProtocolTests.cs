/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;

    [TestClass]
    public class CreationTimeProtocolTests
    {
        private const long CreationTimeMillis = 1786513124792;

        public static IEnumerable<object[]> TimestampCases => new[]
        {
            // A native creation time differs from the modification time.
            new object[] { (long?)CreationTimeMillis, CreationTimeMillis + 1000 },
            // A supporting server supplies the modification time for a
            // pre-25.3 row, and zero for a pre-19.5 row.
            new object[] { (long?)CreationTimeMillis, CreationTimeMillis },
            new object[] { (long?)0, 0L },
            // Missing/zero ct must not be synthesized from a nonzero md/em.
            new object[] { null, CreationTimeMillis },
            new object[] { (long?)0, CreationTimeMillis }
        };

        [TestMethod]
        public void TestGetDeserializesCreationTime()
        {
            using var client = MakeClient();
            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue(), null);
            using var stream = CreateResponse(writer =>
            {
                writer.StartMap(Protocol.FieldNames.Row);
                writer.WriteInt64(Protocol.FieldNames.CreationTime,
                    CreationTimeMillis);
                writer.EndMap();
            });

            var result = new RequestSerializer().DeserializeGet(stream,
                request);

            Assert.AreEqual(GetCreationTime(), result.CreationTime);
        }

        [TestMethod]
        public void TestGetMissingRowHasNoCreationTime()
        {
            using var client = MakeClient();
            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue(), null);

            using var missingStream = CreateResponse(_ => { });
            var missingResult = new RequestSerializer().DeserializeGet(
                missingStream, request);
            Assert.IsNull(missingResult.Row);
            Assert.IsNull(missingResult.Version);
            Assert.IsNull(missingResult.ModificationTime);
            Assert.IsNull(missingResult.CreationTime);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestGetExistingRowWithoutCreationTimePreservesMetadata(
            bool writeZeroCreationTime)
        {
            using var client = MakeClient();
            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue { ["id"] = 1 }, null);
            // Opaque version bytes are only round-tripped by this decoder
            // test; they are not interpreted or submitted to a server.
            var versionBytes = new byte[] { 0, 1, 127, 128, 255 };
            using var stream = CreateResponse(writer =>
            {
                writer.StartMap(Protocol.FieldNames.Row);
                if (writeZeroCreationTime)
                {
                    writer.WriteInt64(Protocol.FieldNames.CreationTime, 0);
                }
                // Include a real row payload and metadata after ct so this
                // cannot pass by treating the response as a missing row or
                // by stopping deserialization at an unavailable timestamp.
                writer.StartMap(Protocol.FieldNames.Value);
                writer.WriteInt32("id", 1);
                writer.WriteString("name", "legacy-row");
                writer.EndMap();
                writer.WriteByteArray(Protocol.FieldNames.RowVersion,
                    versionBytes);
                writer.WriteInt64(Protocol.FieldNames.ModificationTime,
                    CreationTimeMillis);
                writer.EndMap();
            });

            var result = new RequestSerializer().DeserializeGet(stream, request);

            Assert.IsNotNull(result.Row);
            Assert.AreEqual(2, result.Row.Count);
            Assert.AreEqual(1, result.Row["id"].AsInt32);
            Assert.AreEqual("legacy-row", result.Row["name"].AsString);
            Assert.IsNotNull(result.Version);
            CollectionAssert.AreEqual(versionBytes, result.Version.InternalBytes);
            AssertTimestamp(CreationTimeMillis, result.ModificationTime);
            Assert.IsNull(result.CreationTime);
        }

        [TestMethod]
        public void TestReturnInfoDeserializesCreationTimeForAllWriteResults()
        {
            var putResult = new PutResult<RecordValue>();
            var deleteResult = new DeleteResult<RecordValue>();
            var operationResult = new WriteOperationResult<RecordValue>();

            DeserializeReturnInfo(putResult, CreationTimeMillis);
            DeserializeReturnInfo(deleteResult, CreationTimeMillis);
            DeserializeReturnInfo(operationResult, CreationTimeMillis);

            var expected = GetCreationTime();
            Assert.AreEqual(expected, putResult.ExistingCreationTime);
            Assert.AreEqual(expected, deleteResult.ExistingCreationTime);
            Assert.AreEqual(expected, operationResult.ExistingCreationTime);
        }

        [TestMethod]
        public void TestReturnInfoTreatsMissingAndZeroCreationTimeAsUnavailable()
        {
            foreach (var creationTime in new long?[] { null, 0 })
            {
                foreach (var result in new IWriteResult<RecordValue>[]
                         {
                             new PutResult<RecordValue>(),
                             new DeleteResult<RecordValue>(),
                             new WriteOperationResult<RecordValue>()
                         })
                {
                    DeserializeReturnInfo(result, creationTime);
                    Assert.IsNull(result.ExistingCreationTime);
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(TimestampCases))]
        public void TestGetPreservesServerTimestampSemantics(
            long? creationTime, long modificationTime)
        {
            using var client = MakeClient();
            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue(), null);
            var versionBytes = new byte[] { 1, 2, 3, 255 };
            using var stream = CreateResponse(writer =>
            {
                writer.StartMap(Protocol.FieldNames.Row);
                writer.StartMap(Protocol.FieldNames.Value);
                writer.WriteInt32("id", 1);
                writer.EndMap();
                writer.WriteByteArray(Protocol.FieldNames.RowVersion,
                    versionBytes);
                WriteTimestamps(writer, creationTime, modificationTime,
                    Protocol.FieldNames.ModificationTime);
                writer.EndMap();
            });

            var result = new RequestSerializer().DeserializeGet(stream, request);
            Assert.IsNotNull(result.Row);
            Assert.AreEqual(1, result.Row.Count);
            Assert.AreEqual(1, result.Row["id"].AsInt32);
            Assert.IsNotNull(result.Version);
            CollectionAssert.AreEqual(versionBytes, result.Version.InternalBytes);
            AssertTimestamp(creationTime, result.CreationTime);
            AssertTimestamp(modificationTime, result.ModificationTime);
        }

        [DataTestMethod]
        [DynamicData(nameof(TimestampCases))]
        public void TestConditionalWriteResponsesPreserveServerTimestamps(
            long? creationTime, long modificationTime)
        {
            using var client = MakeClient();
            using var stream = CreateResponse(writer =>
            {
                writer.WriteBoolean(Protocol.FieldNames.Success, false);
                WriteReturnInfo(writer, creationTime, modificationTime);
            });
            var serializer = new RequestSerializer();
            var put = serializer.DeserializePut(stream,
                new PutIfAbsentRequest<RecordValue>(client, "table",
                    new RecordValue(), new PutOptions { ReturnExisting = true }));
            Assert.IsFalse(put.Success);
            AssertTimestamp(creationTime, put.ExistingCreationTime);
            AssertTimestamp(modificationTime, put.ExistingModificationTime);

            stream.Position = 0;
            var delete = serializer.DeserializeDelete(stream,
                new DeleteRequest<RecordValue>(client, "table", new MapValue(),
                    new DeleteOptions { ReturnExisting = true }));
            Assert.IsFalse(delete.Success);
            AssertTimestamp(creationTime, delete.ExistingCreationTime);
            AssertTimestamp(modificationTime, delete.ExistingModificationTime);
        }

        [DataTestMethod]
        [DynamicData(nameof(TimestampCases))]
        public void TestWriteManyFailureAndNonAbortingResponses(
            long? creationTime, long modificationTime)
        {
            using var client = MakeClient();
            foreach (var abort in new[] { true, false })
            {
                var request = new WriteManyRequest<RecordValue>(client, "table",
                    new WriteOperationCollection()
                        .AddPut(new MapValue { ["id"] = 0 })
                        .AddPutIfAbsent(new MapValue { ["id"] = 1 },
                            new PutOptions { ReturnExisting = true }),
                    new WriteManyOptions { AbortIfUnsuccessful = abort });
                using var stream = CreateResponse(writer =>
                {
                    if (abort)
                    {
                        writer.StartMap(Protocol.FieldNames.WmFailure);
                        writer.WriteInt32(Protocol.FieldNames.WmFailIndex, 1);
                        writer.StartMap(Protocol.FieldNames.WmFailResult);
                    }
                    else
                    {
                        writer.StartArray(Protocol.FieldNames.WmSuccess);
                        writer.StartMap();
                        writer.WriteBoolean(Protocol.FieldNames.Success, true);
                        writer.EndMap();
                        writer.StartMap();
                    }

                    writer.WriteBoolean(Protocol.FieldNames.Success, false);
                    WriteReturnInfo(writer, creationTime, modificationTime);
                    writer.EndMap();
                    if (abort)
                    {
                        writer.EndMap();
                    }
                    else
                    {
                        writer.EndArray();
                    }
                });

                var result = new RequestSerializer()
                    .DeserializeWriteMany<RecordValue>(stream, request);
                WriteOperationResult<RecordValue> failed;
                if (abort)
                {
                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(1, result.FailedOperationIndex);
                    Assert.IsNull(result.Results);
                    failed = result.FailedOperationResult;
                }
                else
                {
                    Assert.IsTrue(result.Success);
                    Assert.IsNull(result.FailedOperationIndex);
                    Assert.IsNull(result.FailedOperationResult);
                    Assert.AreEqual(2, result.Results.Count);
                    Assert.IsTrue(result.Results[0].Success);
                    failed = result.Results[1];
                }

                Assert.IsNotNull(failed);
                Assert.IsFalse(failed.Success);
                AssertTimestamp(creationTime, failed.ExistingCreationTime);
                AssertTimestamp(modificationTime, failed.ExistingModificationTime);
            }
        }

        private static void WriteReturnInfo(NsonWriter writer,
            long? creationTime, long modificationTime)
        {
            writer.StartMap(Protocol.FieldNames.ReturnInfo);
            WriteTimestamps(writer, creationTime, modificationTime,
                Protocol.FieldNames.ExistingModTime);
            writer.EndMap();
        }

        private static void WriteTimestamps(NsonWriter writer,
            long? creationTime, long modificationTime, string modificationField)
        {
            if (creationTime.HasValue)
            {
                writer.WriteInt64(Protocol.FieldNames.CreationTime,
                    creationTime.Value);
            }
            writer.WriteInt64(modificationField, modificationTime);
        }

        private static void AssertTimestamp(long? millis, DateTime? actual)
        {
            if (!millis.HasValue || millis == 0)
            {
                Assert.IsNull(actual);
            }
            else
            {
                Assert.AreEqual(DateTime.UnixEpoch.AddMilliseconds(millis.Value),
                    actual);
                Assert.AreEqual(DateTimeKind.Utc, actual.Value.Kind);
            }
        }

        private static NoSQLClient MakeClient() => new NoSQLClient(
            new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "localhost:8080"
            });

        private static MemoryStream CreateResponse(Action<NsonWriter> write)
        {
            var stream = new MemoryStream();
            var writer = Protocol.GetNsonWriter(stream);
            writer.StartMap();
            write(writer);
            writer.EndMap();
            stream.Position = 0;
            return stream;
        }

        private static void DeserializeReturnInfo(IWriteResult<RecordValue> result,
            long? creationTime)
        {
            using var stream = CreateResponse(writer =>
            {
                if (creationTime.HasValue)
                {
                    writer.WriteInt64(Protocol.FieldNames.CreationTime,
                        creationTime.Value);
                }
            });

            var reader = Protocol.GetNsonReader(stream);
            reader.Next();
            Protocol.DeserializeReturnInfo(reader, result);
        }

        private static DateTime GetCreationTime() =>
            DateTime.UnixEpoch.AddMilliseconds(CreationTimeMillis);
    }
}
