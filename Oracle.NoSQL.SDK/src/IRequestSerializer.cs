/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {
    using System.IO;

    internal interface IRequestSerializer
    {
        string ContentType { get; }

        short SerialVersion { get; }

        // This method allows serializer to fall back to an older protocol
        // version if the service does not support the current version.
        // Returns true if the fallback was successful.  The implementation
        // depends on the protocol versions in use and is specific to the
        // protocol.
        // This method can be call concurrently by multiple threads, however
        // we already handle synchronization and race conditions in
        // ProtocolHandler.DecrementSerialVersion() which calls this method.
        // If used outside of ProtocolHandler, this method would need to be
        // thread-safe.
        bool DecrementSerialVersion(short versionUsed) => false;

        // Called before the request is serialized.
        void StartWrite(MemoryStream stream, Request request)
        {
        }

        // Called before the response is deserialized.
        void StartRead(MemoryStream stream, Request request)
        {
        }

        void SerializeTableDDL(MemoryStream stream, TableDDLRequest request);

        TableResult DeserializeTableDDL(MemoryStream stream,
            TableDDLRequest request);

        void SerializeAdmin(MemoryStream stream, AdminRequest request);

        AdminResult DeserializeAdmin(MemoryStream stream,
            AdminRequest request);

        void SerializeGetTable(MemoryStream stream, GetTableRequest request);

        TableResult DeserializeGetTable(MemoryStream stream,
            GetTableRequest request);

        void SerializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request);

        AdminResult DeserializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request);

        void SerializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request);

        TableUsageResult DeserializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request);

        void SerializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request);

        IndexResult[] DeserializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request);

        void SerializeListTables(MemoryStream stream,
            ListTablesRequest request);

        ListTablesResult DeserializeListTables(MemoryStream stream,
            ListTablesRequest request);

        void SerializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request);

        GetResult<TRow> DeserializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request);

        void SerializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request);

        PutResult<TRow> DeserializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request);

        void SerializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request);

        DeleteResult<TRow> DeserializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request);

        void SerializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request);

        DeleteRangeResult DeserializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request);

        void SerializeWriteMany(MemoryStream stream,
            WriteManyRequest request);

        WriteManyResult<TRow> DeserializeWriteMany<TRow>(MemoryStream stream,
            WriteManyRequest request);

        void SerializePrepare(MemoryStream stream, PrepareRequest request);

        PreparedStatement DeserializePrepare(MemoryStream stream,
            PrepareRequest request);

        void SerializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request);

        QueryResult<TRow> DeserializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request);

    }

}
