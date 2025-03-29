/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;

    /// <summary>
    /// Represents user credentials used by
    /// <see cref="KVStoreAuthorizationProvider"/>.
    /// </summary>
    /// <remarks>
    /// The credentials must have valid user name and password that are not
    /// <c>null</c> and not empty.
    /// </remarks>
    /// <seealso cref="KVStoreAuthorizationProvider"/>
    public class KVStoreCredentials
    {
        /// <summary>
        /// Initializes a new instance of <see cref="KVStoreCredentials"/>.
        /// </summary>
        /// <remarks>
        /// You must set valid user name and password via
        /// <see cref="UserName"/> and <see cref="Password"/> properties.
        /// </remarks>
        public KVStoreCredentials()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="KVStoreCredentials"/>
        /// using specified user name and password.
        /// </summary>
        /// <param name="userName">User name of the kvstore user.</param>
        /// <param name="password">Password of the kvstore user.</param>
        public KVStoreCredentials(string userName, char[] password)
        {
            UserName = userName;
            Password = password;
        }

        /// <summary>
        /// Gets or sets the user name.
        /// </summary>
        /// <value>
        /// User name of the kvstore user.
        /// </value>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        /// <value>
        /// Password of the kvstore user.
        /// </value>
        public char [] Password { get; set; }
    }

}
