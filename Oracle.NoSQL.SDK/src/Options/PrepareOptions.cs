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
    /// Represents options passed to <see cref="NoSQLClient.PrepareAsync"/>.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    public class PrepareOptions : IOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether a printout of the
        /// query execution plan will be included in the returned result.
        /// </summary>
        /// <value>
        /// <c>true</c> to return the printout of the query execution plan,
        /// otherwise <c>false</c>.  If <c>true</c>, the query execution plan
        /// will be available as <see cref="PreparedStatement.QueryPlan"/>.
        /// The default is <c>false</c>.
        /// </value>
        public bool GetQueryPlan { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
        }
    }

}
