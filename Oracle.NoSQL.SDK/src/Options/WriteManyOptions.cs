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
    /// Represents options passed to <see cref="NoSQLClient.WriteManyAsync"/>
    /// API.
    /// </summary>
    /// <remarks>
    /// In addition to these options, you may also specify separate options
    /// for each Put and Delete operation when that operation is added to
    /// <see cref="WriteOperationCollection"/>.
    /// </remarks>
    /// <seealso cref="NoSQLClient.WriteManyAsync"/>
    /// <seealso cref="WriteOperationCollection"/>
    public class WriteManyOptions : IWriteManyOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the value that determines whether to abort the
        /// transaction if any Put or Delete operation fails.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If set to <c>true</c> and any Put or Delete operation fails, the
        /// entire transaction started by
        /// <see cref="NoSQLClient.WriteManyAsync"/> will be aborted.
        /// </para>
        /// <para>
        /// <c>true</c> value of this property overrides the value of
        /// <see cref="IWriteOperation.AbortIfUnsuccessful"/> property for any
        /// operation in <see cref="WriteOperationCollection"/>.  <c>false</c>
        /// value of this property has no effect on any value of
        /// <see cref="IWriteOperation.AbortIfUnsuccessful"/> property for the
        /// operations in <see cref="WriteOperationCollection"/>.
        /// </para>
        /// </remarks>
        /// <value><c>true</c> to abort the transaction if any Put or Delete
        /// operation fails, otherwise <c>false</c>.  The default is
        /// <c>false</c>.
        /// </value>
        public bool AbortIfUnsuccessful { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
        }

    }

    /// <summary>
    /// Represents options passed to <see cref="NoSQLClient.PutManyAsync"/>
    /// API.
    /// </summary>
    /// <remarks>
    /// These options include all options available in
    /// <see cref="PutOptions"/> with the addition of
    /// <em>AbortIfUnsuccessful</em> option.
    /// </remarks>
    public class PutManyOptions : PutOptions, IWriteManyOptions
    {
        /// <summary>
        /// Gets or sets the value that determines whether to abort the
        /// transaction if any Put operation fails.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> and any Put operation fails, the
        /// entire transaction started by
        /// <see cref="NoSQLClient.PutManyAsync"/> will be aborted.
        /// </remarks>
        /// <value><c>true</c> to abort the transaction if any Put operation
        /// fails, otherwise <c>false</c>.  The default is <c>false</c>.
        /// </value>
        public bool AbortIfUnsuccessful { get; set; }
    }

    /// <summary>
    /// Represents options passed to <see cref="NoSQLClient.DeleteManyAsync"/>
    /// API.
    /// </summary>
    /// <remarks>
    /// These options include all options available in
    /// <see cref="DeleteOptions"/> with the addition of
    /// <em>AbortIfUnsuccessful</em> option.
    /// </remarks>
    public class DeleteManyOptions : DeleteOptions, IWriteManyOptions
    {
        /// <summary>
        /// Gets or sets the value that determines whether to abort the
        /// transaction if any Delete operation fails.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> and any Delete operation fails, the
        /// entire transaction started by
        /// <see cref="NoSQLClient.DeleteManyAsync"/> will be aborted.
        /// </remarks>
        /// <value><c>true</c> to abort the transaction if any Delete
        /// operation fails, otherwise <c>false</c>.  The default is
        /// <c>false</c>.
        /// </value>
        public bool AbortIfUnsuccessful { get; set; }
    }

}
