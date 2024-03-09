/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

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
        /// <summary>
        /// Gets the version contents as byte array.
        /// </summary>
        /// <remarks>
        /// This can be used to pass the version to a query via
        /// <c>row_version</c> SQL function, as shown in the example.
        /// </remarks>
        /// <value>
        /// The <c>byte[]</c> contents of this instance.
        /// </value>
        /// <example>
        /// Passing row version as a byte array to a query.
        /// <code>
        /// var preparedStatement = await client.PrepareAsync(
        ///     @"UPDATE MyTable $t SET NAME = 'John' WHERE id = ? AND row_version($t) = ?");
        /// preparedStatement.SetVariable(1, 10);
        /// preparedStatement.SetVariable(2, rowVersion.Bytes);
        /// var queryResult = await client.QueryAsync(preparedStatement);
        /// </code>
        /// </example>
        public byte[] Bytes { get; }

        /// <summary>
        /// Initializes new instance of <see cref="RowVersion"/> with the
        /// specified <c>byte[]</c> value.
        /// </summary>
        /// <remarks>
        /// This constructor can be used to create row version from a
        /// <c>byte[]</c> that was obtained from a query using
        /// <c>row_version</c> SQL function, as shown in the example.
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
            Bytes = value ?? throw new ArgumentNullException(
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
        public string Encoded => Convert.ToBase64String(Bytes);

        /// <summary>
        /// Converts value of this instance to string as Base64-encoded
        /// representation of its binary contents.
        /// </summary>
        /// <remarks>
        /// You may use the string representation of row version similar to
        /// <see cref="Bytes"/> property to pass to pass the version to a
        /// query via <c>row_version</c> SQL function, as shown in the
        /// example.
        /// </remarks>
        /// <returns>
        /// String representation of this instance, which is the same as
        /// the value of <see cref="Encoded"/> property.
        /// </returns>
        /// <example>
        /// Passing row version as a string to a query.
        /// <code>
        /// var rowVersion = getResult.Version;
        /// var queryResult = await client.QueryAsync(
        ///     "UPDATE MyTable $t SET NAME = 'John' WHERE id = 10 AND " +
        ///     $"row_version($t) = CAST('{rowVersion}' AS Binary");
        /// </code>
        /// </example>
        public override string ToString() => Encoded;
    }

}
