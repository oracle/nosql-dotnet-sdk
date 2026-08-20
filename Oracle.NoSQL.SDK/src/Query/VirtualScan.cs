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
        internal sealed class TableResumeInfo
        {
            internal int CurrentIndexRange { get; set; }

            internal byte[] PrimaryKey { get; set; }

            internal byte[] SecondaryKey { get; set; }

            internal bool MoveAfterResumeKey { get; set; }

            internal byte[] JoinDescendantResumeKey { get; set; }

            internal int[] JoinPathTableIds { get; set; }

            internal byte[] JoinPathPrimaryKey { get; set; }

            internal byte[] JoinPathSecondaryKey { get; set; }

            internal bool JoinPathMatched { get; set; }
        }

        private TableResumeInfo[] tableResumeInfos =
            { new TableResumeInfo() };

        internal int ShardId { get; set; }

        internal int PartitionId { get; set; }

        internal TableResumeInfo[] TableResumeInfos
        {
            get => tableResumeInfos;
            set => tableResumeInfos = value;
        }

        private TableResumeInfo FirstTableResumeInfo =>
            tableResumeInfos != null && tableResumeInfos.Length != 0
                ? tableResumeInfos[0] : null;

        // V4 compatibility accessors for the single-table virtual-scan form.
        internal byte[] PrimaryKey
        {
            get => FirstTableResumeInfo?.PrimaryKey;
            set => FirstTableResumeInfo.PrimaryKey = value;
        }

        internal byte[] SecondaryKey
        {
            get => FirstTableResumeInfo?.SecondaryKey;
            set => FirstTableResumeInfo.SecondaryKey = value;
        }

        internal byte[] JoinDescendantResumeKey
        {
            get => FirstTableResumeInfo?.JoinDescendantResumeKey;
            set => FirstTableResumeInfo.JoinDescendantResumeKey = value;
        }

        internal int[] JoinPathTableIds
        {
            get => FirstTableResumeInfo?.JoinPathTableIds;
            set => FirstTableResumeInfo.JoinPathTableIds = value;
        }

        internal byte[] JoinPathPrimaryKey
        {
            get => FirstTableResumeInfo?.JoinPathPrimaryKey;
            set => FirstTableResumeInfo.JoinPathPrimaryKey = value;
        }

        internal byte[] JoinPathSecondaryKey
        {
            get => FirstTableResumeInfo?.JoinPathSecondaryKey;
            set => FirstTableResumeInfo.JoinPathSecondaryKey = value;
        }

        internal bool IsInfoSent { get; set; }
        
        internal bool MoveAfterResumeKey
        {
            get => FirstTableResumeInfo?.MoveAfterResumeKey ?? false;
            set => FirstTableResumeInfo.MoveAfterResumeKey = value;
        }

        internal bool JoinPathMatched
        {
            get => FirstTableResumeInfo?.JoinPathMatched ?? false;
            set => FirstTableResumeInfo.JoinPathMatched = value;
        }

    }

}
