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
    /// Represents options passed to <see cref="NoSQLClient.GetAsync"/> API.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing Get operation with provided <see cref="GetOptions"/>.
    /// <code>
    /// var result = await client.GetAsync("myTable",
    ///     new MapValue // primary key
    ///     {
    ///         ["id"] = 1000
    ///     },
    ///     new GetOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(10),
    ///         Consistency = Consistency.Absolute
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.GetAsync"/>
    public class GetOptions : IReadOptions
    {
        /// <summary>
        /// Cloud service only.  Gets or sets the compartment id or name for
        /// the operation.
        /// </summary>
        /// <remarks>
        /// See remarks section of <see cref="NoSQLClient"/>.
        /// </remarks>
        /// <value>
        /// Compartment id or name.  If not set, defaults to
        /// <see cref="NoSQLConfig.Compartment"/>.
        /// </value>
        public string Compartment { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the request.
        /// </summary>
        /// <value>
        /// Request timeout.  If set, must be a positive value.  If not set,
        /// defaults to <see cref="NoSQLConfig.Timeout"/>.
        /// </value>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets <see cref="Consistency"/> used for the operation.
        /// </summary>
        /// <value>
        /// Consistency used for the operation.  If not set, defaults to
        /// <see cref="NoSQLConfig.Consistency"/>.
        /// </value>
        /// <seealso cref="Consistency"/>
        public Consistency? Consistency { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            CheckEnumValue(Consistency);
        }
    }

    /// <summary>
    /// Consistency is used to provide consistency guarantees for read
    /// operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Consistency.Absolute"/> consistency may be specified to
    /// guarantee that current values are read.
    /// <see cref="Consistency.Eventual"/> consistency means that the values
    /// read may be very slightly out of date.
    /// <see cref="Consistency.Absolute"/> consistency results in higher cost,
    /// consuming twice the number of read units for the same data relative to
    /// <see cref="Consistency.Eventual"/> consistency, and should only be
    /// used when required.
    /// </para>
    /// <para>
    /// It is possible to set a default <see cref="Consistency"/> for a
    /// <see cref="NoSQLClient"/> instance by providing it in the initial
    /// configuration as <see cref="NoSQLConfig.Consistency"/>.  In JSON
    /// configuration file, you may use string values "Eventual" or
    /// "Absolute".  If no consistency is specified for an operation and there
    /// is no default value, <see cref="Consistency.Eventual"/> is used.
    /// </para>
    /// <para>
    /// <see cref="Consistency"/> may be specified in options for all read
    /// operations.
    /// </para>
    /// </remarks>
    /// <seealso cref="GetOptions"/>
    /// <seealso cref="QueryOptions"/>
    public enum Consistency
    {
        /// <summary>
        /// Absolute consistency.
        /// </summary>
        Absolute,

        /// <summary>
        /// Eventual consistency.
        /// </summary>
        Eventual
    }

}
