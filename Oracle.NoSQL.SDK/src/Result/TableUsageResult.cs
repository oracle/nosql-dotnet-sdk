/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Cloud Service/Cloud Simulator only. Represents the result of
    /// GetTableUsage operation.
    /// </summary>
    /// <remarks>
    /// This class represents the result of
    /// <see cref="NoSQLClient.GetTableUsageAsync"/> API.  It encapsulates the
    /// dynamic state of requested table.
    /// </remarks>
    /// <seealso cref="NoSQLClient.GetTableUsageAsync"/>
    /// <seealso cref="TableUsageRecord"/>
    public class TableUsageResult
    {
        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName { get; internal set; }

        /// <summary>
        /// Gets the list of table usage records returned according to
        /// criteria used when calling
        /// <see cref="NoSQLClient.GetTableUsageAsync"/>.  The records are in
        /// chronological order.
        /// </summary>
        /// <value>
        /// List of table usage records.
        /// </value>
        public IReadOnlyList<TableUsageRecord> UsageRecords
            { get; internal set; }
    }

    /// <summary>
    /// Cloud Service/Cloud Simulator only.
    /// Table usage records are part of <see cref="TableUsageResult"/>.
    /// Each represents a single usage record, or slice, that includes
    /// information about read and write throughput consumed during that
    /// period as well as the current information regarding the storage
    /// capacity. In addition the count of throttling exceptions for the
    /// period is reported.
    /// </summary>
    public class TableUsageRecord
    {
        /// <summary>
        /// Gets the start time for this usage record.
        /// </summary>
        /// <value>
        /// Start time.
        /// </value>
        public DateTime StartTime { get; internal set; }

        /// <summary>
        /// Gets the time period (duration) of this usage record.
        /// </summary>
        /// <value>
        /// The duration.
        /// </value>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Gets the number of read units consumed during this period.
        /// </summary>
        /// <value>
        /// Number of read units.
        /// </value>
        public int ReadUnits { get; internal set; }

        /// <summary>
        /// Gets the number of write units consumed during this period.
        /// </summary>
        /// <value>
        /// Number of write units.
        /// </value>
        public int WriteUnits { get; internal set; }

        /// <summary>
        /// Gets the amount of storage consumed by the table in gigabytes.
        /// This information may be out of date as it is not maintained in
        /// real time.
        /// </summary>
        /// <value>
        /// Storage in gigabytes.
        /// </value>
        public int StorageGB { get; internal set; }

        /// <summary>
        /// Gets the number of read throttling exceptions on this table in
        /// the time period.
        /// </summary>
        /// <value>
        /// Number of read throttling exceptions.
        /// </value>
        /// <seealso cref="ReadThrottlingException"/>
        public int ReadThrottleCount { get; internal set; }

        /// <summary>
        /// Gets the number of write throttling exceptions on this table in
        /// the time period.
        /// </summary>
        /// <value>
        /// Number of write throttling exceptions.
        /// </value>
        /// <seealso cref="WriteThrottlingException"/>
        public int WriteThrottleCount { get; internal set; }

        /// <summary>
        /// Gets the number of storage throttling exceptions on this table in
        /// the time period.
        /// </summary>
        /// <value>
        /// Number of storage throttling exceptions.
        /// </value>
        /// <seealso cref="TableSizeLimitException"/>
        public int StorageThrottleCount { get; internal set; }
    }

}
