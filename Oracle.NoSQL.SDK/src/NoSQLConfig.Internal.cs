/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using static ValidateUtils;

    public partial class NoSQLConfig
    {
        private class ServiceTypeConverter : JsonConverter<ServiceType>
        {
            public override ServiceType Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                return (ServiceType)Enum.Parse(typeof(ServiceType),
                    reader.GetString(), true);
            }

            public override void Write(Utf8JsonWriter writer,
                ServiceType value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class TimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.TokenType == JsonTokenType.Number ?
                    TimeSpan.FromMilliseconds(reader.GetInt64()) :
                    TimeSpan.Parse(reader.GetString());
            }

            public override void Write(Utf8JsonWriter writer,
                TimeSpan value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class CharArrayConverter : JsonConverter<char[]>
        {
            public override char[] Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.GetString().ToCharArray();
            }

            public override void Write(Utf8JsonWriter writer,
                char[] value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class RetryHandlerConverter : JsonConverter<IRetryHandler>
        {
            public override IRetryHandler Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                return JsonSerializer.Deserialize<NoSQLRetryHandler>(
                    ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer,
                IRetryHandler value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class AuthProviderConverter :
            JsonConverter<IAuthorizationProvider>
        {
            private const string AuthType = "AuthorizationType";

            public override IAuthorizationProvider Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException(
                        "Value of \"AuthorizationProvider\" property " +
                        "must be an object");
                }

                using var doc = JsonDocument.ParseValue(ref reader);
                if (!doc.RootElement.TryGetProperty(AuthType,
                    out var elem))
                {
                    throw new JsonException($"Missing {AuthType}");
                }

                var authType = elem.ValueKind == JsonValueKind.String
                    ? elem.GetString()
                    : throw new JsonException(
                        $"Value of ${AuthType} must be a string");
                var text = doc.RootElement.GetRawText();

                if (string.Equals(authType, "IAM",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer
                        .Deserialize<IAMAuthorizationProvider>(
                            text, options);
                }

                if (string.Equals(authType, "KVStore",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return JsonSerializer
                        .Deserialize<KVStoreAuthorizationProvider>(
                            text, options);
                }

                throw new JsonException(
                    $"Unrecognized value of {AuthType}: {authType}");
            }

            public override void Write(Utf8JsonWriter writer,
                IAuthorizationProvider value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class RegionConverter : JsonConverter<Region>
        {
            public override Region Read(ref Utf8JsonReader reader,
                Type typeToConvert, JsonSerializerOptions options)
            {
                return Region.FromRegionId(reader.GetString());
            }

            public override void Write(Utf8JsonWriter writer,
                Region value, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        private class NoRetriesHandler : IRetryHandler
        {
            public bool ShouldRetry(Request request) => false;

            // return value is irrelevant here
            public TimeSpan GetRetryDelay(Request request) => TimeSpan.Zero;
        }

        internal static readonly JsonSerializerOptions
            JsonSerializerOptions;

        internal Uri Uri { get; set; }

        static NoSQLConfig()
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            JsonSerializerOptions.Converters.Add(
                new ServiceTypeConverter());
            JsonSerializerOptions.Converters.Add(
                new TimeSpanConverter());
            JsonSerializerOptions.Converters.Add(
                new CharArrayConverter());
            JsonSerializerOptions.Converters.Add(
                new RetryHandlerConverter());
            JsonSerializerOptions.Converters.Add(
                new AuthProviderConverter());
            JsonSerializerOptions.Converters.Add(
                new RegionConverter());
        }

        private static Uri EndpointToUri(string endpoint)
        {
            var host = endpoint;
            string protocol = null;
            int port = -1;

            var index = endpoint.IndexOf("://", StringComparison.Ordinal);
            if (index != -1)
            {
                protocol = endpoint.Substring(0, index).ToLower();
                if (protocol != "http" && protocol != "https")
                {
                    throw new ArgumentException(
                        $"Invalid service protocol {protocol} in endpoint " +
                            endpoint);
                }
                host = endpoint.Substring(index + 3);
            }
            if (host.Contains("/"))
            {
                throw new ArgumentException(
                    $"Endpoint may not contain path: {endpoint}");
            }

            var parts = host.Split(':');
            if (parts.Length == 0 || parts.Length > 2)
            {
                throw new ArgumentException($"Invalid endpoint: {endpoint}");
            }

            host = parts[0];

            if (parts.Length == 2 &&
                (!int.TryParse(parts[1], out port) || port < 0))
            {
                throw new ArgumentException(
                    $"Invalid port value {parts[1]} for endpoint {endpoint}");
            }

            // If protocol is not specified and the port isn't 443, assume
            // we're using http.
            if (protocol == null)
            {
                if (port == -1)
                {
                    port = 443;
                }
                protocol = port == 443 ? "https" : "http";
            }
            else if (port == -1)
            {
                port = protocol == "https" ? 443 : 8080;
            }

            try
            {
                return new Uri($"{protocol}://{host}:{port}");
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException(
                    $"Failed to construct Uri from endpoint {endpoint}", ex);
            }
        }

        // Shallow copy.
        internal NoSQLConfig Clone()
        {
            return (NoSQLConfig)MemberwiseClone();
        }

        internal void InitUri()
        {
            if (Region != null)
            {
                if (Endpoint != null)
                {
                    throw new ArgumentException(
                        "Cannot specify Endpoint property together with " +
                        "Region property");
                }

                Endpoint = Region.Endpoint;
            }

            if (Endpoint != null)
            {
                Uri = EndpointToUri(Endpoint);
            }
        }

        private void Validate()
        {
            CheckEnumValue(ServiceType);
            CheckEnumValue(Consistency);
            CheckTimeout(Timeout);
            CheckTimeout(TableDDLTimeout, nameof(TableDDLTimeout));
            CheckTimeout(AdminTimeout, nameof(AdminTimeout));
            CheckTimeout(SecurityInfoNotReadyTimeout,
                nameof(SecurityInfoNotReadyTimeout));
            CheckPollParameters(TablePollTimeout, TablePollDelay,
                nameof(TablePollTimeout), nameof(TablePollDelay));
            CheckPollParameters(AdminPollTimeout, AdminPollDelay,
                nameof(AdminPollTimeout), nameof(AdminPollDelay));
            CheckPositiveInt32(MaxMemoryMB, nameof(MaxMemoryMB));

            if (RetryHandler is NoSQLRetryHandler noSQLRetryHandler)
            {
                noSQLRetryHandler.Validate();
            }

            // AuthorizationProvider is validated in ConfigureAuthorization().

            ConnectionOptions?.Validate();
            RateLimitingHandler.ValidateConfig(this);
        }

        internal void Init()
        {
            Validate();
            InitUri();

            RetryHandler ??= new NoSQLRetryHandler();

            if (AuthorizationProvider == null)
            {
                if (ServiceType == ServiceType.Unspecified)
                {
                    ServiceType = Region != null
                        ? ServiceType.Cloud
                        : ServiceType.CloudSim;
                }

                switch (ServiceType)
                {
                    case ServiceType.CloudSim:
                        AuthorizationProvider =
                            new CloudSimAuthorizationProvider();
                        break;
                    case ServiceType.Cloud:
                        AuthorizationProvider = new IAMAuthorizationProvider();
                        break;
                    case ServiceType.KVStore: // non-secure kvstore
                        break;
                }
            }
            else
            {
                switch (ServiceType)
                {
                    case ServiceType.Unspecified:
                        if (AuthorizationProvider is IAMAuthorizationProvider)
                        {
                            ServiceType = ServiceType.Cloud;
                        }
                        else if (AuthorizationProvider is
                            KVStoreAuthorizationProvider)
                        {
                            ServiceType = ServiceType.KVStore;
                        }
                        else
                        {
                            throw new ArgumentException(
                                "Could not determine the service type with " +
                                "the information provided.  Please specify " +
                                "the service type");
                        }
                        break;
                    case ServiceType.CloudSim:
                        throw new ArgumentException(
                            "Cannot specify AuthorizationProvider with " +
                            "ServiceType CloudSim");
                    // We check for common errors below but still allow
                    // specifying custom AuthorizationProvider for cloud or
                    // kvstore.
                    case ServiceType.Cloud:
                        if (AuthorizationProvider is
                            KVStoreAuthorizationProvider)
                        {
                            throw new ArgumentException(
                                "Cannot specify AuthorizationProvider as " +
                                "KVStoreAuthorizationProvider with " +
                                "ServiceType Cloud");
                        }
                        break;
                    case ServiceType.KVStore:
                        if (AuthorizationProvider is IAMAuthorizationProvider)
                        {
                            throw new ArgumentException(
                                "Cannot specify AuthorizationProvider as " +
                                "IAMAuthorizationProvider with " +
                                "ServiceType KVStore");
                        }
                        break;
                }
            }

            if (Region != null && ServiceType != ServiceType.Cloud)
            {
                throw new ArgumentException(
                    "Cannot specify Region with ServiceType " +
                    $"{ServiceType}, ServiceType must be Cloud");
            }

            ConnectionOptions?.Init();
            AuthorizationProvider?.ConfigureAuthorization(this);

            // Special case where the value of url may be set above by
            // IAMAuthorizationProvider.ConfigureAuthorization() by getting
            // region from OCI config file or resource principal environment.
            // In any case, the Uri must be set by this point.
            if (Uri == null)
            {
                throw new ArgumentException("Missing service endpoint or region");
            }
        }

        // NoSQLConfig does not implement IDisposable because there are
        // no disposable resources from the user's point of view.  Any
        // resources that may need to be disposed are internally created and
        // released by the driver only.  The user does not need to dispose of
        // NoSQLConfig instance.
        internal void ReleaseResources()
        {
            if (AuthorizationProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            ConnectionOptions?.ReleaseResources();
        }
    }

}
