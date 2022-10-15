/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// the number of write operations passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>,
    /// <see cref="NoSQLClient.PutManyAsync"/> or
    /// <see cref="NoSQLClient.DeleteManyAsync"/> exceeds the system-defined
    /// limit.
    /// </summary>
    /// <remarks>
    /// The number of write operations may mean the number of operations in
    /// <see cref="WriteOperationCollection"/> passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>, the
    /// number of rows passed to <see cref="NoSQLClient.PutManyAsync"/> or
    /// the number of primary keys passed to
    /// <see cref="NoSQLClient.DeleteManyAsync"/>.
    /// </remarks>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>
    /// <seealso cref="NoSQLClient.PutManyAsync"/>
    /// <seealso cref="NoSQLClient.DeleteManyAsync"/>
    public class BatchOperationNumberLimitException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="BatchOperationNumberLimitException"/>.
        /// </summary>
        public BatchOperationNumberLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="BatchOperationNumberLimitException"/> with the message
        /// that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public BatchOperationNumberLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="BatchOperationNumberLimitException"/> with the message
        /// that describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public BatchOperationNumberLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
