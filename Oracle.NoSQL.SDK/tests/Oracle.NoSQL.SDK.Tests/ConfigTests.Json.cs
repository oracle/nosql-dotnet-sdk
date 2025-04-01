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
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;

    public partial class ConfigTests
    {
        private static int jsonWriteSeq;

        private class ServiceTypeConverter : JsonConverter<ServiceType>
        {
            public override ServiceType Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                ServiceType value, JsonSerializerOptions options)
            {
                var stringValue = value.ToString();
                var i = jsonWriteSeq++ % 3;
                writer.WriteStringValue(i switch
                {
                    0 => stringValue,
                    1 => stringValue.ToUpper(),
                    _ => stringValue.ToLower()
                });
            }
        }

        private class TimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                TimeSpan value, JsonSerializerOptions options)
            {
                var i = jsonWriteSeq++ % 3;
                switch (i)
                {
                    case 0:
                        writer.WriteNumberValue(
                            (long)Math.Round(value.TotalMilliseconds));
                        break;
                    case 1:
                        writer.WriteNumberValue(value.TotalMilliseconds);
                        break;
                    case 2:
                        writer.WriteStringValue(value.ToString());
                        break;
                }
            }
        }

        private class CharArrayConverter : JsonConverter<char[]>
        {
            public override char[] Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                char[] value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }

        private class RetryHandlerConverter : JsonConverter<IRetryHandler>
        {
            public override IRetryHandler Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                IRetryHandler value, JsonSerializerOptions options)
            {
                Assert.IsTrue(value is NoSQLRetryHandler);
                var handler = (NoSQLRetryHandler)value;
                JsonSerializer.Serialize(writer, handler, options);
            }
        }

        private class AuthProviderConverter :
            JsonConverter<IAuthorizationProvider>
        {
            public override IAuthorizationProvider Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                IAuthorizationProvider value, JsonSerializerOptions options)
            {
                var isOnPrem = value is KVStoreAuthorizationProvider;
                // test self-check
                Assert.IsTrue(isOnPrem || value is IAMAuthorizationProvider);

                string json = isOnPrem
                    ? JsonSerializer.Serialize(
                        (KVStoreAuthorizationProvider)value, options)
                    : JsonSerializer.Serialize(
                        (IAMAuthorizationProvider)value, options);

                // We have to use MapValue because JsonDocument is immutable
                // and JsonNode is only available in .Net 6.
                var mapValue = FieldValue.FromJsonString(json).AsMapValue;
                mapValue["AuthorizationType"] = isOnPrem ? "KVStore" : "IAM";

                mapValue.SerializeAsJson(writer, new JsonOutputOptions
                {
                    Indented = true
                });
            }
        }

        private class RegionConverter : JsonConverter<Region>
        {
            public override Region Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer,
                Region value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.RegionId);
            }
        }

        private static readonly JsonSerializerOptions
            JsonSerializerOptions = new Func<JsonSerializerOptions>(() =>
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                options.Converters.Add(new ServiceTypeConverter());
                options.Converters.Add(new TimeSpanConverter());
                options.Converters.Add(new CharArrayConverter());
                options.Converters.Add(new RetryHandlerConverter());
                options.Converters.Add(new AuthProviderConverter());
                options.Converters.Add(new RegionConverter());
                return options;
            })();

        private static readonly string ConfigFilePath =
            TempFileCache.GetTempFile();

        private static bool CanUseConfigWithJson(NoSQLConfig config) =>
            (config.RetryHandler == null ||
             config.RetryHandler is NoSQLRetryHandler) &&
            (config.AuthorizationProvider == null ||
             config.AuthorizationProvider is KVStoreAuthorizationProvider kv &&
             kv.CredentialsProvider == null ||
             config.AuthorizationProvider is IAMAuthorizationProvider iam &&
             iam.Credentials?.PrivateKey == null &&
             iam.CredentialsProvider == null &&
             iam.DelegationTokenProvider == null &&
             config.RateLimiterCreator == null);

        private static IEnumerable<object[]> PositiveJsonDataSource =>
            from config in GoodConfigs where CanUseConfigWithJson(config)
            select new object[] { config };

        // TODO:
        // We should add negative JSON test cases that are not possible to
        // generate from NoSQLConfig instances such as malformed JSON, wrong
        // types for property values, missing "AuthorizationType" property,
        // etc.

        private static IEnumerable<object[]> NegativeJsonDataSource =>
            from config in BadConfigs
            where CanUseConfigWithJson(config)
            select new object[] { config };

        [DataTestMethod]
        [DynamicData(nameof(PositiveJsonDataSource))]
        public void TestPositiveWithJson(NoSQLConfig config)
        {
            var jsonConfig =
                JsonSerializer.Serialize(config, JsonSerializerOptions);
            File.WriteAllText(ConfigFilePath, jsonConfig);

            NoSQLClient client;
            try
            {
                client = new NoSQLClient(ConfigFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

            VerifyConfig(client.Config, config);
        }

        [DataTestMethod]
        [DynamicData(nameof(NegativeJsonDataSource))]
        public void TestNegativeWithJson(NoSQLConfig config)
        {
            var jsonConfig =
                JsonSerializer.Serialize(config, JsonSerializerOptions);
            File.WriteAllText(ConfigFilePath, jsonConfig);

            AssertThrowsDerived<ArgumentException>(() =>
            {
                var noSQLClient = new NoSQLClient(ConfigFilePath);
            });
        }

    }

}
