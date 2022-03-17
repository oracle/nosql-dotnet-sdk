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

}
