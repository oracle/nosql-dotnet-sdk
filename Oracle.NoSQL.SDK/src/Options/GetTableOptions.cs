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
    /// Represents options passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/> APIs.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing GetTable operation with provided <see cref="GetTableOptions"/>.
    /// <code>
    /// var result = await client.GetTableAsync("myTable",
    ///     new GetTableOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(10)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/>
    public class GetTableOptions : IOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Namespace"/>
        public string Namespace
        {
            get => Compartment;
            set => Compartment = value;
        }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
        }
    }

    /// <summary>
    /// Represents options passed to
    /// <see cref="NoSQLClient.WaitForTableStateAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WaitForLocalReplicaInitAsync*"/>
    /// methods.
    /// </summary>
    /// <remarks>
    /// This class represents options in <see cref="GetTableOptions"/>
    /// with addition of poll delay to poll for table state change.
    /// </remarks>
    /// <seealso cref="NoSQLClient.WaitForTableStateAsync"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WaitForLocalReplicaInitAsync*"/>
    /// <seealso cref="GetTableOptions" />
    public class TableCompletionOptions : GetTableOptions, ITableCompletionOptions
    {
        /// <summary>
        /// Gets or sets the poll delay to poll for table state change.
        /// </summary>
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
