/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Cloud Service/Cloud Simulator only.  Represents options passed to
    /// <see cref="NoSQLClient.GetTableUsageAsync"/> API.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If neither <see cref="StartTime"/> nor <see cref="EndTime"/> is
    /// specified, only a single most recent table usage record is returned.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// For both <see cref="StartTime"/> and <see cref="EndTime"/> the
    /// <see cref="DateTime.Kind"/> must be either
    /// <see cref="DateTimeKind.Utc"/> or <see cref="DateTimeKind.Local"/> and
    /// cannot be <see cref="DateTimeKind.Unspecified"/>.  It is preferred to
    /// use <see cref="DateTimeKind.Utc"/> to avoid time zone ambiguities.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If both <see cref="StartTime"/> and <see cref="EndTime"/> are
    /// specified, they must be of the same <see cref="DateTime.Kind"/>,
    /// preferred kind being <see cref="DateTimeKind.Utc"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="StartTime"/> is specified, but not
    /// <see cref="EndTime"/>, <see cref="EndTime"/> defaults to the current
    /// time.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="EndTime"/> is specified, but not
    /// <see cref="StartTime"/>, up to <see cref="Limit"/> usage records will
    /// be returned ending at <see cref="EndTime"/>.  E.g. if
    /// <see cref="EndTime"/> is 10pm today and <see cref="Limit"/>
    /// is 60, 60 1-minute usage records will be returned between 9pm and 10pm
    /// today.  If <see cref="Limit"/> is not specified, a large system limit
    /// will be used which may result in large number of table usage
    /// records returned.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// You may not specify <see cref="Limit"/> unless you specify at least
    /// one of <see cref="StartTime"/> and <see cref="EndTime"/>.  Without
    /// the time period only one most recent usage record is returned.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="Limit"/> is not specified, a large system defined limit
    /// will be used which may result in large number of table usage records
    /// returned unless the time period is appropriately restricted.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Executing GetTableUsage operation with provided
    /// <see cref="GetTableUsageOptions"/>.
    /// <code>
    /// var result = await client.GetTableUsageAsync("MyTable",
    ///     new GetTableUsageOptions
    ///     {
    ///         Compartment = "my_compartment",
    ///         Timeout = TimeSpan.FromSeconds(10),
    ///         StartTime = DateTime.Now - TimeSpan.FromHours(1)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.GetTableUsageAsync"/>
    public class GetTableUsageOptions : IOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the start time for the time period from which to
        /// return table usage records.
        /// </summary>
        /// <value>
        /// Start time.
        /// </value>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time for the time period from which to
        /// return table usage records.
        /// </summary>
        /// <value>
        /// End time.
        /// </value>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the limit on the number of table usage records
        /// returned.
        /// </summary>
        /// <value>
        /// Limit on the number of table usage records.
        /// </value>
        public int? Limit { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);

            if (StartTime.HasValue &&
                StartTime.Value.Kind == DateTimeKind.Unspecified ||
                EndTime.HasValue &&
                EndTime.Value.Kind == DateTimeKind.Unspecified)
            {
                throw new ArgumentException(
                    "StartTime or EndTime Kind may not be " +
                    "DateTimeKind.Unspecified");
            }

            if (StartTime.HasValue && EndTime.HasValue &&
                StartTime.Value.Kind != EndTime.Value.Kind)
            {
                throw new ArgumentException(
                    "StartTime and EndTime must be of the same kind " +
                    "(Utc or Local)");
            }

            if (StartTime > EndTime)
            {
                throw new ArgumentException(
                    "StartTime may not be greater than EndTime");
            }

            CheckPositiveInt32(Limit, nameof(Limit));

            if (Limit.HasValue && !StartTime.HasValue && !EndTime.HasValue)
            {
                throw new ArgumentException(
                    "Limit can only be specified together with the time " +
                    "range (use StartTime and/or EndTime");
            }
        }

    }

}
