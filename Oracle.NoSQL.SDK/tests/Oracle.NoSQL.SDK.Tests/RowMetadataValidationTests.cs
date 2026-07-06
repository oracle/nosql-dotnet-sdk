/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Http;
    using Oracle.NoSQL.SDK.Query;

    [TestClass]
    public class RowMetadataValidationTests
    {
        private static readonly string[] ValidMetadata =
        {
            "{}",
            "{\"user\":\"alice\"}",
            "[1, true, null]",
            "\"value\"",
            "123",
            "true",
            "false",
            "null"
        };

        private static readonly string[] InvalidMetadata =
        {
            "",
            " ",
            "custom metadata",
            "{'a':1}",
            "{\"a\":1}{\"b\":2}",
            "{}[]",
            "\"abc\"\"def\""
        };

        private static readonly Func<string, IOptions>[] OptionsFactories =
        {
            metadata => new PutOptions { LastWriteMetadata = metadata },
            metadata => new DeleteOptions { LastWriteMetadata = metadata },
            metadata => new DeleteRangeOptions { LastWriteMetadata = metadata },
            metadata => new QueryOptions { LastWriteMetadata = metadata }
        };

        private static long? GetEnabledFeatures(string versionHeader)
        {
            using var response = new HttpResponseMessage();
            if (versionHeader != null)
            {
                response.Headers.Add(HttpConstants.ServerVersion,
                    versionHeader);
            }

            return Client.GetEnabledFeatures(response);
        }

        private static NoSQLClient MakeClient() =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "localhost:8080"
            });

        [TestMethod]
        public void TestValidRowMetadata()
        {
            foreach (var metadata in ValidMetadata)
            {
                foreach (var createOptions in OptionsFactories)
                {
                    createOptions(metadata).Validate();
                }
            }
        }

        [TestMethod]
        public void TestInvalidRowMetadata()
        {
            foreach (var metadata in InvalidMetadata)
            {
                foreach (var createOptions in OptionsFactories)
                {
                    Assert.ThrowsException<ArgumentException>(() =>
                        createOptions(metadata).Validate());
                }
            }
        }

        [TestMethod]
        public void TestEnabledFeaturesParsing()
        {
            Assert.IsNull(GetEnabledFeatures(null));
            Assert.IsNull(GetEnabledFeatures("proxy=26.1.0 kv=26.1.0"));
            Assert.IsNull(GetEnabledFeatures(
                "proxy=26.1.0 kv=26.1.0 features=not-hex"));
            Assert.AreEqual(0, GetEnabledFeatures(
                "proxy=26.1.0 kv=26.1.0 features=0"));
            Assert.AreEqual(1, GetEnabledFeatures(
                "proxy=26.1.0 kv=26.1.0 features=1"));
            Assert.AreEqual(15, GetEnabledFeatures(
                "proxy=26.1.0 kv=26.1.0 features=f other=value"));
        }

        [TestMethod]
        public void TestQueryMetadataRequiresQueryV4()
        {
            using var client = MakeClient();
            Assert.IsTrue(client.ProtocolHandler.DecrementQueryVersion(
                QueryRequestBase.QueryV4));
            var request = new QueryRequest<RecordValue>(client,
                "UPDATE users SET name = 'alice'",
                new QueryOptions { LastWriteMetadata = "{}" });

            Assert.ThrowsException<NotSupportedException>(() =>
                request.Validate());
        }

        [TestMethod]
        public void TestInternalQueryRequestCopiesLastWriteMetadata()
        {
            using var client = MakeClient();
            var metadata = "{\"source\":\"test\"}";
            var preparedStatement = new PreparedStatement
            {
                DriverQueryPlan = new ReceiveStep()
            };
            var runtime = new QueryRuntime(client, preparedStatement)
            {
                Request = new QueryRequest<RecordValue>(client,
                    preparedStatement,
                    new QueryOptions { LastWriteMetadata = metadata })
            };
            var iterator = new ReceiveIterator(runtime, new ReceiveStep());
            var queryRequestField = typeof(ReceiveIterator).GetField(
                "queryRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(queryRequestField);

            var queryRequest =
                (QueryRequest<RecordValue>)queryRequestField.GetValue(
                    iterator);
            Assert.AreEqual(metadata,
                queryRequest.Options.LastWriteMetadata);
        }
    }
}
