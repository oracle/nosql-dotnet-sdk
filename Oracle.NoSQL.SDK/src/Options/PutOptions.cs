/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    internal enum PutOpKind
    {
        Always,
        IfAbsent,
        IfPresent,
        IfVersion
    }

    /// <summary>
    /// Represent options for the Put operation.
    /// </summary>
    /// <remarks>
    /// These options are passed to APIs <see cref="NoSQLClient.PutAsync"/>,
    /// <see cref="NoSQLClient.PutIfAbsentAsync"/>,
    /// <see cref="NoSQLClient.PutIfPresentAsync"/> and
    /// <see cref="NoSQLClient.PutIfVersionAsync"/>.
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing Put operation with provided <see cref="PutOptions"/>.
    /// <code>
    /// var result = await client.PutAsync("myTable",
    ///     new MapValue
    ///     {
    ///         ["id"] = 1000,
    ///         ["name"] = "John"
    ///     },
    ///     new PutOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(10),
    ///         IfAbsent = true,
    ///         TTL = TimeToLive.OfDays(5)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.PutAsync"/>
    /// <seealso cref="NoSQLClient.PutIfAbsentAsync"/>
    /// <seealso cref="NoSQLClient.PutIfPresentAsync"/>
    /// <seealso cref="NoSQLClient.PutIfVersionAsync"/>
    public class PutOptions : IWriteOptions
    {
        private RowVersion matchVersion;
        internal PutOpKind putOpKind = PutOpKind.Always;

        internal bool UpdateTTL => TTL.HasValue || UpdateTTLToDefault;

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

        /// <summary>
        /// On-premise only.
        /// Gets or sets <see cref="Durability"/> value to use for the
        /// operation.
        /// </summary>
        /// <remarks>
        /// <see cref="Durability"/> determines <see cref="SyncPolicy"/> for
        /// Master and Replicas as well as <see cref="ReplicaAckPolicy"/> for
        /// the Replicas.
        /// </remarks>
        /// <value>
        /// Durability used for the operation.  If not set, defaults to
        /// <see cref="NoSQLConfig.Durability"/>.
        /// </value>
        /// <seealso cref="Durability"/>
        public Durability? Durability { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether to perform the Put
        /// operation only if there is no existing row that matches the
        /// primary key.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="PutOptions.IfPresent"/>
        /// and <see cref="PutOptions.MatchVersion"/>.  If set to <c>true</c>,
        /// those properties will be unset.  You may also use
        /// <see cref="NoSQLClient.PutIfAbsentAsync"/> API instead of setting
        /// this option.
        /// </remarks>
        /// <value>
        /// <c>true</c> to perform the Put operation only if there is no
        /// existing row matching the primary key, otherwise <c>false</c>.
        /// The default is <c>false</c>.
        /// </value>
        public bool IfAbsent
        {
            get => putOpKind == PutOpKind.IfAbsent;
            set
            {
                if (value)
                {
                    putOpKind = PutOpKind.IfAbsent;
                    matchVersion = null;
                }
                else
                {
                    putOpKind = PutOpKind.Always;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value that determines whether to perform the Put
        /// operation only if there is an existing row that matches the
        /// primary key.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="PutOptions.IfAbsent"/>
        /// and <see cref="PutOptions.MatchVersion"/>.  If set to <c>true</c>,
        /// those properties will be unset.  You may also use
        /// <see cref="NoSQLClient.PutIfPresentAsync"/> API instead of setting
        /// this option.
        /// </remarks>
        /// <value>
        /// <c>true</c> to perform the Put operation only if there is an
        /// existing row matching the primary key, otherwise <c>false</c>.
        /// The default is <c>false</c>.
        /// </value>
        public bool IfPresent
        {
            get => putOpKind == PutOpKind.IfPresent;
            set
            {
                if (value)
                {
                    putOpKind = PutOpKind.IfPresent;
                    matchVersion = null;
                }
                else
                {
                    putOpKind = PutOpKind.Always;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value that determines whether to perform the Put
        /// operation only if there is an existing row that matches the
        /// primary key and its <see cref="RowVersion"/> matches the value
        /// provided.
        /// </summary>
        /// <remarks>
        /// This property is exclusive with <see cref="PutOptions.IfAbsent"/>
        /// and <see cref="PutOptions.IfPresent"/>.  If set to a non-null value,
        /// those properties will be unset.  You may also use
        /// <see cref="NoSQLClient.PutIfVersionAsync"/> API instead of setting
        /// this option.
        /// </remarks>
        /// <value>
        /// The value of <see cref="RowVersion"/> that indicates that the Put
        /// operation should only be performed if there is an existing row
        /// matching the primary key and its version matches this value.
        /// </value>
        public RowVersion MatchVersion
        {
            get => matchVersion;
            set
            {
                matchVersion = value;
                putOpKind = matchVersion != null ?
                    PutOpKind.IfVersion : PutOpKind.Always;
            }
        }

        /// <summary>
        /// Gets or sets a value that determines whether to return existing
        /// row and its <see cref="RowVersion"/> if the conditional Put
        /// operation fails.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c>, the existing row and its version will be
        /// returned as <see cref="PutResult{TRow}.ExistingRow"/> and
        /// <see cref="PutResult{TRow}.ExistingVersion"/>.
        /// </remarks>
        /// <value>
        /// <c>true</c> to return existing row and its version if conditional
        /// Put operation fails, otherwise <c>false</c>. The default is
        /// <c>false</c>.
        /// </value>
        public bool ReturnExisting { get; set; }

        /// <summary>
        /// Gets or sets <see cref="TimeToLive"/> value of the row.
        /// </summary>
        /// <remarks>
        /// If set, it causes the time to live on the row to be set to the
        /// specified value on put.  This value overrides any default time to
        /// live setting on the table.
        /// </remarks>
        /// <value>
        /// Time to live value.
        /// </value>
        public TimeToLive? TTL { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether to update the time to
        /// live (TTL) value of the existing row to the table's default TTL if
        /// there is an existing row and the Put operation is successful.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> and there is an existing row matching the
        /// primary key, its TTL will be set to the table's default TTL.
        /// If the table has no default TTL or the value of
        /// <see cref="PutOptions.TTL"/> is set, this property has no effect.
        /// </remarks>
        /// <value>
        /// <c>true</c> to update TTL to the table's default, otherwise
        /// <c>false</c>.  The default is <c>false</c>, meaning that updating
        /// an existing row has no effect on its TTL.
        /// </value>
        public bool UpdateTTLToDefault { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether the provided row
        /// value must be an exact match for the table schema.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> and the <see cref="MapValue"/> provided for
        /// the row does not match the table schema, the operation will fail.
        /// Exact match means that there are no required fields missing and
        /// that there are no extra, unknown fields.
        /// </remarks>
        /// <value>
        /// <c>true</c> to require the row to exactly match the table schema,
        /// otherwise <c>false</c>.  The default is <c>false</c>, meaning that
        /// exact match is not required.
        /// </value>
        public bool ExactMatch { get; set; }

        /// <summary>
        /// Gets or sets the number of generated identity values that are
        /// requested from the server during the Put operation.
        /// </summary>
        /// <remarks>
        /// This value takes precedence over the DDL identity CACHE option set
        /// during creation of the identity column.
        /// </remarks>
        /// <value>
        /// Number of generated identity values requested from the server
        /// during the Put operation. Must be a positive integer. If not set,
        /// the DDL identity CACHE value is used.
        /// </value>
        /// <seealso cref="PutResult{TRow}.GeneratedValue"/>
        public int? IdentityCacheSize { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            Durability?.Validate();
            CheckPositiveInt32(IdentityCacheSize, nameof(IdentityCacheSize));
            if (UpdateTTLToDefault && TTL.HasValue)
            {
                throw new ArgumentException(
                    "Cannot specify TTL together with UpdateTTLToDefault " +
                    "option");
            }
        }
    }

}
