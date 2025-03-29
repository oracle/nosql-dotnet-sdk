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
    /// Represent options for the Delete operation.
    /// </summary>
    /// <remarks>
    /// These options are passed to APIs <see cref="NoSQLClient.DeleteAsync"/>
    /// and <see cref="NoSQLClient.DeleteIfVersionAsync"/>.
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing Delete operation with provided <see cref="PutOptions"/>.
    /// <code>
    /// // We first call PutAsync to create a row and then use its version
    /// // for the conditional Delete operation.
    ///
    /// var putResult = await client.PutAsync("myTable",
    ///     new MapValue
    ///     {
    ///         ["id"] = 1000,
    ///         ["name"] = "John"
    ///     });
    ///
    /// // Some time later we delete the row but only if it was not modified
    /// // meanwhile (meaning its version has not changed).
    ///
    /// var deleteResult = await client.DeleteAsync("myTable",
    ///     new MapValue
    ///     {
    ///         ["id"] = 1000
    ///     },
    ///     new DeleteOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(10),
    ///         MatchVersion = getResult.Version,
    ///         ReturnExisting = true
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.DeleteAsync"/>
    /// <seealso cref="NoSQLClient.DeleteIfVersionAsync"/>
    public class DeleteOptions : IWriteOptions
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

        /// <inheritdoc cref="PutOptions.Durability"/>
        public Durability? Durability { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether to perform the Delete
        /// operation only if there is an existing row that matches the
        /// primary key and its <see cref="RowVersion"/> matches the value
        /// provided.
        /// </summary>
        /// <remarks>
        /// You may also use <see cref="NoSQLClient.DeleteIfVersionAsync"/>
        /// API instead of setting this option.
        /// </remarks>
        /// <value>
        /// The value of <see cref="RowVersion"/> that indicates that the
        /// Delete operation should only be performed if there is an existing
        /// row matching the primary key and its version matches this value.
        /// </value>
        public RowVersion MatchVersion { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether to return existing
        /// row, its <see cref="RowVersion"/> and modification time.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c>, the existing row, its version and
        /// modification time will be returned as part of
        /// <see cref="DeleteResult{TRow}"/> under conditions described in
        /// the remarks section of <see cref="NoSQLClient.DeleteAsync"/>.
        /// </remarks>
        /// <value>
        /// <c>true</c> to return existing row information, otherwise
        /// <c>false</c>.  The default is <c>false</c>.
        /// </value>
        public bool ReturnExisting { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            Durability?.Validate();
        }

    }

}
