/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;

    internal enum ErrorCode
    {
        // Error codes for user-generated errors, range from 1 to 50
        //(exclusive).
        // These include illegal arguments, exceeding size limits for some
        // objects, resource not found, etc.
        UnknownOperation = 1,
        TableNotFound = 2,
        IndexNotFound = 3,
        IllegalArgument = 4,
        RowSizeLimitExceeded = 5,
        KeySizeLimitExceeded = 6,
        BatchOpNumberLimitExceeded = 7,
        RequestSizeLimitExceeded = 8,
        TableExists = 9,
        IndexExists = 10,
        InvalidAuthorization = 11,
        InsufficientPermission = 12,
        ResourceExists = 13,
        ResourceNotFound = 14,
        TableLimitExceeded = 15,
        IndexLimitExceeded = 16,
        BadProtocolMessage = 17,
        EvolutionLimitExceeded = 18,
        TableDeploymentLimitExceeded = 19,
        TenantDeploymentLimitExceeded = 20,
        OperationNotSupported = 21,
        ETagMismatch = 22,
        UnsupportedProtocol = 24,
        TableNotReady = 26,
        UnsupportedQueryVersion = 27,

        // Error codes for user throttling, range from 50 to 100(exclusive).
        ReadLimitExceeded = 50,
        WriteLimitExceeded = 51,
        SizeLimitExceeded = 52,
        OperationLimitExceeded = 53,

        // Error codes for server issues, range from 100 to 150(exclusive).

        // Retry-able server issues, range from 100 to 125(exclusive).
        // These are internal problems, presumably temporary, and need to be
        // sent back to the application for retry.
        RequestTimeout = 100,
        ServerError = 101,
        ServiceUnavailable = 102,
        TableBusy = 103,
        SecurityInfoUnavailable = 104,
        RetryAuthentication = 105,

        // Other server issues, begin from 125.
        // These include server illegal state, unknown server error, etc.
        // They might be retry-able, or not.
        UnknownError = 125,
        IllegalState = 126
    }

    internal static partial class Protocol
    {
        // Special case for TABLE_NOT_FOUND errors on writeMany with multiple
        // tables. Earlier server versions do not support this and will return
        // a TABLE_NOT_FOUND error with the table names in a single string,
        // separated by commas, with no brackets, like:
        // table1,table2,table3
        // Later versions may legitimately return TABLE_NOT_FOUND error, but
        // table names will be inside a bracketed list, like:
        // [table1, table2, table3]
        private static Exception HandleWriteManyTableNotFound(
            WriteManyRequest wmr, string message)
        {
            if (!wmr.IsSingleTable && message != null &&
                message.Contains(',') && !message.Contains('['))
            {
                return new NotSupportedException(
                    "WriteMany operation with multiple tables is not " +
                    "supported by the version of the connected server");
            }

            return new TableNotFoundException(message);
        }

        internal static Exception MapException(ErrorCode errorCode,
            string message, Request request)
        {
            switch (errorCode)
            {
                case ErrorCode.UnknownError:
                    return new NoSQLException($"Unknown error: {message}");
                case ErrorCode.UnknownOperation:
                    return new NoSQLException(
                        $"Unknown operation: {message}");
                case ErrorCode.TableNotFound:
                    if (request is WriteManyRequest wmr)
                    {
                        return HandleWriteManyTableNotFound(wmr, message);
                    }
                    return new TableNotFoundException(message);
                case ErrorCode.IndexLimitExceeded:
                    return new IndexLimitException(message);
                case ErrorCode.TableLimitExceeded:
                    return new TableLimitException(message);
                case ErrorCode.EvolutionLimitExceeded:
                    return new EvolutionLimitException(message);
                case ErrorCode.IndexNotFound:
                    return new IndexNotFoundException(message);
                case ErrorCode.IllegalArgument:
                    return new ArgumentException(message);
                case ErrorCode.ReadLimitExceeded:
                    return new ReadThrottlingException(message);
                case ErrorCode.WriteLimitExceeded:
                    return new WriteThrottlingException(message);
                case ErrorCode.SizeLimitExceeded:
                    return new TableSizeLimitException(message);
                case ErrorCode.RowSizeLimitExceeded:
                    return new RowSizeLimitException(message);
                case ErrorCode.KeySizeLimitExceeded:
                    return new KeySizeLimitException(message);
                case ErrorCode.BatchOpNumberLimitExceeded:
                    return new BatchOperationNumberLimitException(message);
                case ErrorCode.RequestSizeLimitExceeded:
                    return new RequestSizeLimitException(message);
                case ErrorCode.TableExists:
                    return new TableExistsException(message);
                case ErrorCode.IndexExists:
                    return new IndexExistsException(message);
                case ErrorCode.TableDeploymentLimitExceeded:
                case ErrorCode.TenantDeploymentLimitExceeded:
                    return new DeploymentLimitException(message);
                case ErrorCode.IllegalState:
                    return new InvalidOperationException(message);
                case ErrorCode.ServiceUnavailable:
                    return new ServiceUnavailableException(message);
                case ErrorCode.ServerError:
                    return new ServerException(message);
                case ErrorCode.BadProtocolMessage:
                    if (message.Contains("driver serial version",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return new UnsupportedProtocolException(message);
                    }
                    if (message.Contains("invalid query version",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return new UnsupportedQueryVersionException(message);
                    }
                    return new BadProtocolException(message);
                case ErrorCode.UnsupportedProtocol:
                    return new UnsupportedProtocolException(message);
                case ErrorCode.TableBusy:
                    return new TableBusyException(message);
                case ErrorCode.UnsupportedQueryVersion:
                    return new UnsupportedQueryVersionException(message);
                case ErrorCode.RequestTimeout:
                    return new TimeoutException(message);
                case ErrorCode.InvalidAuthorization:
                case ErrorCode.RetryAuthentication:
                    return new InvalidAuthorizationException(message);
                case ErrorCode.InsufficientPermission:
                    return new UnauthorizedException(message);
                case ErrorCode.SecurityInfoUnavailable:
                    return new SecurityInfoNotReadyException(message);
                case ErrorCode.OperationLimitExceeded:
                    return new ControlOperationThrottlingException(message);
                case ErrorCode.ResourceExists:
                    return new ResourceExistsException(message);
                case ErrorCode.ResourceNotFound:
                    return new ResourceNotFoundException(message);
                case ErrorCode.OperationNotSupported:
                    return new NotSupportedException(message);
                case ErrorCode.ETagMismatch:
                    return new ETagMismatchException(message);
                case ErrorCode.TableNotReady:
                    return new TableNotReadyException(message);
                default:
                    return new BadProtocolException(
                        $"Unknown error code {errorCode}: {message}");
            }
        }
    }

}
