/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// On-premise only.  Represents options passed to
    /// <see cref="NoSQLClient.GetAdminStatusAsync"/> API.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <seealso cref="NoSQLClient.GetAdminStatusAsync"/>
    public class GetAdminStatusOptions : IOptions
    {
        string IOptions.Compartment => null;

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
        }
    }

}
