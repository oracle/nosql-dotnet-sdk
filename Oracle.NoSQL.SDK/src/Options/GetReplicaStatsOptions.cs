/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Cloud Service only.
    /// Represents options passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
    /// API.
    /// </summary>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
    public class GetReplicaStatsOptions : IOptions
    {
        string IOptions.Namespace => null;

        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the start time from which to retrieve replica stats
        /// records.
        /// </summary>
        /// <value>
        /// Start time. If not set, the number of most recent complete stats
        /// records are returned, up to <see cref="Limit"/>, per replica.
        /// </value>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the limit on the number of replica stats records
        /// returned by one call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>.
        /// </summary>
        /// <remarks>
        /// Note that this limit is for the number of stats records for each
        /// replica. E.g. if you have 3 replicas and the limit is 1000, then up
        /// to 1000 stats records for each replica can be returned, up to 3000
        /// stats records total.
        /// </remarks>
        /// <value>
        /// Limit on the number of replica stats records. Defaults to <c>1000</c>.
        /// </value>
        public int? Limit { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);

            if (StartTime.HasValue &&
                StartTime.Value.Kind == DateTimeKind.Unspecified)
            {
                throw new ArgumentException(
                    "StartTime Kind may not be DateTimeKind.Unspecified");
            }

            CheckPositiveInt32(Limit, nameof(Limit));
        }
    }

}
