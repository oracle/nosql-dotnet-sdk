/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Represents the version of a row in the database.
    /// </summary>
    /// <remarks>
    /// <see cref="RowVersion"/> represents the version of a row in the
    /// database.  The version is returned by successful
    /// <see cref="NoSQLClient.GetAsync"/> operation and can be used by
    /// <see cref="NoSQLClient.PutIfVersionAsync"/>,
    /// <see cref="NoSQLClient.DeleteIfVersionAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> APIs to
    /// conditionally perform those operations to ensure an atomic
    /// read-modify-write cycle.  This is an opaque object from an application
    /// perspective.  Using <see cref="RowVersion"/> in this way adds cost to
    /// operations so it should be done only if necessary.
    /// </remarks>
    /// <example>
    /// Using <see cref="RowVersion"/> for a conditional Put operation.
    /// <code>
    /// var row = new MapValue
    /// {
    ///     ["id"] = 1000,
    ///     ["name"] = "John"
    /// };
    ///
    /// var result = await client.PutAsync("myTable", row);
    /// var version = result.Version;
    ///
    /// // Some time later we modify the row but only if nobody has modified
    /// // it since (meaning that its version has not changed)
    ///
    /// row["name"] = "Jane";
    /// result = await client.PutIfVersionAsync("myTable", row, version);
    /// Console.WriteLine(result.Success);
    /// </code>
    /// </example>
    public class RowVersion
    {
        private readonly byte[] bytes;

        /// <summary>
        /// Gets the version contents as byte array.
        /// </summary>
        /// <remarks>
        /// Row versions are opaque values.  The byte array returned by this
        /// property is an internal serialized representation and should not be
        /// compared for equality or interpreted by applications.  The
        /// serialized representation may differ between service, proxy and
        /// client versions even when the values identify the same row version.
        /// Use <see cref="RowVersion"/> instances directly with conditional
        /// APIs such as <see cref="NoSQLClient.PutIfVersionAsync"/> and
        /// <see cref="NoSQLClient.DeleteIfVersionAsync"/>.
        /// </remarks>
        /// <value>
        /// The internal <c>byte[]</c> contents of this instance.
        /// </value>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("RowVersion is opaque. Use RowVersion directly with conditional APIs.", false)]
        public byte[] Bytes => bytes;

        internal byte[] InternalBytes => bytes;

        /// <summary>
        /// Initializes new instance of <see cref="RowVersion"/> with the
        /// specified <c>byte[]</c> value.
        /// </summary>
        /// <remarks>
        /// This constructor can be used to create row version from a
        /// <c>byte[]</c> that was obtained from a query using
        /// <c>row_version</c> SQL function, as shown in the example.  Do not
        /// compare the byte array with the bytes from another
        /// <see cref="RowVersion"/>.  Row versions are opaque and their
        /// serialized representation may differ between service, proxy and
        /// client versions.
        /// </remarks>
        /// <param name="value">The value of the contents of this version.
        /// </param>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <example>
        /// Obtaining row version from a query and using it in
        /// <see cref="NoSQLClient.PutIfVersionAsync"/>.
        /// <code>
        /// var queryResult = await client.QueryAsync(
        ///     "SELECT row_version($t) AS version FROM MyTable $t WHERE id = 1");
        /// if (queryResult.Rows.Count != 0)
        /// {
        ///     var rowVersion = new RowVersion(queryResult.Rows[0]["version"]);
        ///     var newRow = new MapValue
        ///     {
        ///         ["id"] = 1,
        ///         ["name"] = "Jane"
        ///     };
        ///     var putResult = await client.PutIfVersionAsync("MyTable",
        ///         newRow, rowVersion);
        /// }
        /// </code>
        /// </example>
        public RowVersion(byte[] value)
        {
            bytes = value ?? throw new ArgumentNullException(
                nameof(value),
                "Argument to RowVersion constructor cannot be null");
        }

        /// <summary>
        /// Gets the contents of this version encoded as Base64 string.
        /// </summary>
        /// <value>
        /// <c>string</c> representing Base64-encoded contents of this
        /// instance.
        /// </value>
        public string Encoded => Convert.ToBase64String(bytes);

        /// <summary>
        /// Converts value of this instance to string as Base64-encoded
        /// representation of its binary contents.
        /// </summary>
        /// <remarks>
        /// Row versions are opaque values.  This string representation should
        /// not be compared for equality with values returned by queries because
        /// the serialized representation may differ between service, proxy and
        /// client versions.
        /// </remarks>
        /// <returns>
        /// String representation of this instance, which is the same as
        /// the value of <see cref="Encoded"/> property.
        /// </returns>
        public override string ToString() => Encoded;
    }

}
