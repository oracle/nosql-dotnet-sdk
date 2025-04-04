/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Cloud Service only. Represents options passed to 
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaWithCompletionAsync*"/>
    /// APIs.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaWithCompletionAsync*"/>
    public class AddReplicaOptions : ITableCompletionOptions
    {
        string IOptions.Namespace => null;

        /// <inheritdoc cref="TableDDLOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the operation.
        /// </summary>
        /// <remarks>
        /// For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/> it
        /// defaults to <see cref="NoSQLConfig.TableDDLTimeout"/>. For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaWithCompletionAsync*"/>
        /// separate default timeouts are used for issuing the operation
        /// and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// </remarks>
        /// <value>
        /// Operation timeout. If set, must be a positive value.
        /// </value>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the poll delay for polling when asynchronously
        /// waiting for operation completion.
        /// </summary>
        /// <remarks>
        /// Applies only to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaWithCompletionAsync*"/>
        /// method. Defaults to <see cref="NoSQLConfig.TablePollDelay"/>.
        /// </remarks>
        /// <value>
        /// Poll delay.  If set, must be a positive value and not greater than
        /// the timeout.
        /// </value>
        public TimeSpan? PollDelay { get; set; }

        /// <summary>
        /// Gets or sets read units for the replica table.
        /// </summary>
        /// <value>
        /// Read units. If not set, defaults to read units for the existing
        /// table.
        /// </value>
        public int? ReadUnits { get; set; }

        /// <summary>
        /// Gets or sets write units for the replica table.
        /// </summary>
        /// <value>
        /// Write units. If not set, defaults to write units for the existing
        /// table.
        /// </value>
        public int? WriteUnits { get; set; }

        /// <inheritdoc cref="TableDDLOptions.MatchETag"/>
        public string MatchETag { get; set; }

        void IOptions.Validate()
        {
            CheckPollParameters(Timeout, PollDelay, nameof(Timeout),
                nameof(PollDelay));
            CheckPositiveInt32(ReadUnits, nameof(ReadUnits));
            CheckPositiveInt32(WriteUnits, nameof(WriteUnits));
        }
    }

}
