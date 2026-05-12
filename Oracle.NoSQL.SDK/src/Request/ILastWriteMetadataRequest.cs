/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    // Request-level feature gate for operations that may send row
    // last-write metadata.  WriteMany returns true when any child operation
    // carries metadata.
    internal interface ILastWriteMetadataRequest
    {
        bool HasLastWriteMetadata { get; }
    }
}
