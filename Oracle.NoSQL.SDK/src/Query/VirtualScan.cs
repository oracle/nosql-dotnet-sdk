/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    internal class VirtualScan
    {
        internal int ShardId { get; set; }

        internal int PartitionId { get; set; }

        internal byte[] PrimaryKey { get; set; }

        internal byte[] SecondaryKey { get; set; }

        internal byte[] JoinDescendantResumeKey { get; set; }

        internal int[] JoinPathTableIds { get; set; }

        internal byte[] JoinPathPrimaryKey { get; set; }

        internal byte[] JoinPathSecondaryKey { get; set; }

        internal bool IsInfoSent { get; set; }
        
        internal bool MoveAfterResumeKey { get; set; }

        internal bool JoinPathMatched { get; set; }

    }

}
