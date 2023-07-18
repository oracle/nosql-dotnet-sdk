/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// On-premise only.  Represents options for admin DDL passed to methods
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
    /// and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing admin DDL with provided <see cref="AdminOptions"/>.
    /// <code>
    /// var result = await client.ExecuteAdminWithCompletionAsync(
    ///     "DROP NAMESPACE my_namespace CASCADE",
    ///     new AdminOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(120),
    ///         PollDelay = TimeSpan.FromSeconds(2)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
    public class AdminOptions : IOptions
    {
        string IOptions.Compartment => null;

        string IOptions.Namespace => null;

        /// <summary>
        /// Gets or sets the timeout for the operation.
        /// </summary>
        /// <remarks>
        /// For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
        /// it defaults to
        /// <see cref="NoSQLConfig.AdminTimeout"/>.  For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>,
        /// separate default timeouts are used for issuing the admin operation
        /// and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.AdminTimeout"/> and
        /// <see cref="NoSQLConfig.AdminPollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.AdminPollTimeout"/> is not set).
        /// </remarks>
        /// <value>
        /// Operation timeout.  If set, must be a positive value.
        /// </value>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the poll delay for polling when asynchronously
        /// waiting for operation completion.
        /// </summary>
        /// <remarks>
        /// Applies only
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
        /// method.  Defaults to <see cref="NoSQLConfig.AdminPollDelay"/>
        /// </remarks>
        /// <value>
        /// Poll delay.  If set, must be a positive value and not greater than
        /// the timeout.
        /// </value>
        public TimeSpan? PollDelay { get; set; }

        void IOptions.Validate()
        {
            CheckPollParameters(Timeout, PollDelay, nameof(Timeout),
                nameof(PollDelay));
        }
    }

}
