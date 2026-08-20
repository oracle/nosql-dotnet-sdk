/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;

    [TestClass]
    public class CreationTimeProtocolTests
    {
        private const long CreationTimeMillis = 1786513124792;

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
        public void TestGetTreatsMissingAndZeroCreationTimeAsUnavailable()
        {
            using var client = MakeClient();
            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue(), null);

            using var missingStream = CreateResponse(_ => { });
            var missingResult = new RequestSerializer().DeserializeGet(
                missingStream, request);
            Assert.IsNull(missingResult.CreationTime);

            using var zeroStream = CreateResponse(writer =>
            {
                writer.StartMap(Protocol.FieldNames.Row);
                writer.WriteInt64(Protocol.FieldNames.CreationTime, 0);
                writer.EndMap();
            });
            var zeroResult = new RequestSerializer().DeserializeGet(
                zeroStream, request);
            Assert.IsNull(zeroResult.CreationTime);
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
            var missingResult = new PutResult<RecordValue>();
            DeserializeReturnInfo(missingResult, null);
            Assert.IsNull(missingResult.ExistingCreationTime);

            var zeroResult = new PutResult<RecordValue>();
            DeserializeReturnInfo(zeroResult, 0);
            Assert.IsNull(zeroResult.ExistingCreationTime);
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
