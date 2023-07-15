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
    /// Represents options passed to <see cref="NoSQLClient.GetIndexesAsync"/>
    /// and <see cref="NoSQLClient.GetIndexAsync"/> APIs.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing GetIndex operation with provided
    /// <see cref="GetIndexOptions"/>.
    /// <code>
    /// var result = await client.GetIndexAsync("myTable", "myIndex",
    ///     new GetIndexOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(10)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.GetIndexesAsync"/>
    /// <seealso cref="NoSQLClient.GetIndexAsync"/>
    public class GetIndexOptions : IOptions
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

}
