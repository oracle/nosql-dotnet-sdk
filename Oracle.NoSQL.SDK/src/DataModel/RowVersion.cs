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
    /// Represents the version of a row in the database.
    /// </summary>
    /// <remarks>
    /// <see cref="RowVersion"/> is an opaque type that represents the version
    /// of a row in the database.  The version is returned by successful
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
        internal byte[] data;

        internal RowVersion(byte[] data)
        {
            this.data = data;
        }

        // Used for testing
        internal string Encoded => data != null ?
            Convert.ToBase64String(data) : null;
    }

}
