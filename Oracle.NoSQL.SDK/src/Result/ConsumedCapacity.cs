/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Diagnostics;

    /// <summary>
    /// Cloud Service/Cloud Simulator only.  Represents read and write
    /// throughput consumed by the operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ConsumedCapacity"/> is part of results of data operations
    /// such as <see cref="GetResult{TRow}"/>, <see cref="PutResult{TRow}"/>,
    /// <see cref="DeleteResult{TRow}"/>, <see cref="WriteManyResult{TRow}"/>,
    /// <see cref="PreparedStatement"/> and <see cref="QueryResult{TRow}"/>.
    /// It contains read and write throughput consumed by the operation in
    /// KBytes as well as in read and write units.  Throughput in read and
    /// write units is defined as follows.
    /// </para>
    /// <para>
    /// A read unit represents one eventually consistent read per second for
    /// data up to 1 KB in size.  A read that is absolutely consistent is
    /// double that, consuming 2 read units for a read of up to 1 KB in size.
    /// This means that if an application is to use
    /// <see cref="Consistency.Absolute"/>, it may need to specify additional
    /// read units when creating a table. A write unit represents 1 write per
    /// second of data up to 1 KB in size.  Note the following:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// For read operations, such as <see cref="NoSQLClient.GetAsync"/>,
    /// <see cref="NoSQLClient.PrepareAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>, the
    /// number of
    /// <see cref="ConsumedCapacity.ReadUnits"/> consumed may be larger than
    /// the number of read KBytes (<see cref="ConsumedCapacity.ReadKB"/>) if
    /// if the operation used absolute consistency.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// For update operations such as <see cref="NoSQLClient.PutAsync"/> and
    /// others, the number of read units consumed may also be larger than the
    /// number of read KBytes.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public class ConsumedCapacity
    {
        internal ConsumedCapacity()
        {
        }

        internal ConsumedCapacity(int readUnits, int readKB, int writeUnits,
            int writeKB)
        {
            ReadUnits = readUnits;
            ReadKB = readKB;
            WriteUnits = writeUnits;
            WriteKB = writeKB;
        }

        /// <summary>
        /// Gets the read throughput in read units.
        /// </summary>
        /// <value>
        /// Read throughput consumed by this operation in read units.
        /// </value>
        public int ReadUnits { get; internal set; }

        /// <summary>
        /// Gets the read throughput in KBytes.
        /// </summary>
        /// <value>
        /// Read throughput consumed by this operation in KBytes.
        /// </value>
        public int ReadKB { get; internal set; }

        /// <summary>
        /// Gets the write throughput in write units.
        /// </summary>
        /// <value>
        /// Write throughput consumed by this operation in write units.
        /// </value>
        public int WriteUnits { get; internal set; }

        /// <summary>
        /// Gets the write throughput in KBytes.
        /// </summary>
        /// <value>
        /// Write throughput consumed by this operation in KBytes.
        /// </value>
        public int WriteKB { get; internal set; }

        /// <summary>
        /// Returns a string representing this consumed capacity.
        /// </summary>
        /// <returns>A string containing information represented by this
        /// consumed capacity.</returns>
        public override string ToString()
        {
            return $"ReadUnits: {ReadUnits}, ReadKB: {ReadKB}, " +
                $"WriteUnits: {WriteUnits}, WriteKB: {WriteKB}";
        }

        internal void Add(ConsumedCapacity other)
        {
            Debug.Assert(other != null);
            ReadUnits += other.ReadUnits;
            ReadKB += other.ReadKB;
            WriteUnits += other.WriteUnits;
            WriteKB += other.WriteKB;
        }

        internal void Clear()
        {
            ReadUnits = 0;
            ReadKB = 0;
            WriteUnits = 0;
            WriteKB = 0;
        }

        internal ConsumedCapacity Clone()
        {
            return new ConsumedCapacity(ReadUnits, ReadKB, WriteUnits,
                WriteKB);
        }
    }

}
