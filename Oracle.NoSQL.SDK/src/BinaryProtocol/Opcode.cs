/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    internal enum Opcode
    {
        Delete = 0,
        DeleteIfVersion = 1,
        Get = 2,
        Put = 3,
        PutIfAbsent = 4,
        PutIfPresent = 5,
        PutIfVersion  = 6,
        Query = 7,
        Prepare = 8,
        WriteMultiple = 9,
        MultiDelete = 10,
        GetTable = 11,
        GetIndexes = 12,
        GetTableUsage = 13,
        ListTables = 14,
        TableRequest = 15,
        Scan = 16,
        IndexScan = 17,
        CreateTable = 18,
        AlterTable = 19,
        DropTable = 20,
        CreateIndex = 21,
        DropIndex = 22,
        SystemRequest = 23,
        SystemStatusRequest = 24
    }

}
