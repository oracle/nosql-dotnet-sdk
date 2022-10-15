/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;

    public class DataRow : RecordValue
    {
        private TimeToLive? ttl;
        private TimeToLive? originalTTL;
        private DateTime? modificationTime;

        internal int Id { get; }

        // TTL value may be changed by testing Put operations with supplied
        // TTL, so we save the original value to be used to re-insert the row.
        internal TimeToLive? TTL
        {
            get => ttl;
            set
            {
                if (ttl.HasValue && !originalTTL.HasValue)
                {
                    originalTTL = ttl;
                }

                ttl = value;
            }
        }

        internal RowVersion Version { get; set; }

        internal DateTime PutTime { get; set; }

        internal DateTime ModificationTime
        {
            get => modificationTime ?? PutTime;
            set => modificationTime = value;
        }

        internal DataRow(int id, TimeToLive? ttl = null)
        {
            Id = id;
            TTL = ttl;
        }

        internal void Reset()
        {
            ttl = originalTTL;
            Version = null;
            modificationTime = null;
        }
    }

    public class DataPK : MapValue
    {
        internal int Id { get; }

        internal DataPK(int id)
        {
            Id = id;
        }
    }

    internal abstract class DataRowFactory
    {
        internal const int DefaultRowsPerShard = 20;

        internal DataRowFactory(int rowsPerShard = DefaultRowsPerShard)
        {
            RowsPerShard = rowsPerShard;
        }

        internal int RowsPerShard { get; }

        internal abstract int MaxRowKB { get; }

        internal abstract DataRow MakeRow(int id);

        internal abstract DataRow MakeModifiedRow(DataRow row);

        internal int GetRowIdFromShard(int shardId, int rowIndex = 0) =>
            shardId * RowsPerShard + rowIndex;

        internal DataRow MakeRowFromShard(int shardId, int rowIndex = 0) =>
            MakeRow(GetRowIdFromShard(shardId, rowIndex));
    }

    internal class DataTestFixture
    {
        internal DataTestFixture(TableInfo table, DataRowFactory factory,
            int rowCount, IndexInfo[] indexes = null)
        {
            Table = table;
            Indexes = indexes;
            RowFactory = factory;
            Rows = new DataRow[rowCount];
            for (var i = 0; i < rowCount; i++)
            {
                Rows[i] = factory.MakeRow(i);
            }
        }

        internal TableInfo Table { get; }

        internal IndexInfo[] Indexes { get; }

        internal DataRow[] Rows { get; }

        internal int RowCount => Rows.Length;

        internal bool HasRow(int id) => id >= 0 && id < Rows.Length;

        internal DataRow GetRow(int id, bool returnNull = false)
        {
            if (!HasRow(id))
            {
                if (returnNull)
                {
                    return null;
                }
                Assert.Fail($"Row with id {id} is not found");
            }

            return Rows[id];
        }

        internal DataRow MakeRow(int id) => RowFactory.MakeRow(id);

        internal DataRow MakeModifiedRow(DataRow row) =>
            RowFactory.MakeModifiedRow(row);

        internal int GetRowIdFromShard(int shardId, int rowIndex = 0) =>
            RowFactory.GetRowIdFromShard(shardId, rowIndex);

        internal DataRow MakeRowFromShard(int shardId, int rowIndex = 0) =>
            RowFactory.MakeRowFromShard(shardId, rowIndex);

        internal int RowIdStart => 0;

        internal int RowIdEnd => Rows.Length;

        internal DataRowFactory RowFactory { get; }

        internal int RowsPerShard => RowFactory.RowsPerShard;

        internal int MaxRowKB => RowFactory.MaxRowKB;

        // Used in testing of single-shard operations.
        internal int FirstShardCount => Math.Min(RowsPerShard, RowCount);
    }

}
