/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the information about a single index including its name and
    /// field names.
    /// </summary>
    /// <remarks>
    /// A list of <see cref="IndexResult"/> objects is the
    /// result of <see cref="NoSQLClient.GetIndexesAsync"/> API and a
    /// single <see cref="IndexResult"/> object is the result of
    /// <see cref="NoSQLClient.GetIndexAsync"/> API.
    /// </remarks>
    /// <seealso cref="NoSQLClient.GetIndexesAsync"/>
    /// <seealso cref="NoSQLClient.GetIndexAsync"/>
    public class IndexResult
    {
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        /// <value>
        /// The name of the index.
        /// </value>
        public string IndexName { get; internal set; }

        /// <summary>
        /// Gets the list of field names that define the index.
        /// </summary>
        /// <value>
        /// List of field names.
        /// </value>
        public IReadOnlyList<string> Fields { get; internal set; }

        // It would be better to encapsulate field name and type into a
        // FieldInfo object, however we have to keep Fields property as it is
        // for backward compatibility.

        /// <summary>
        /// Gets the list of field types corresponding to the list of field
        /// names.
        /// </summary>
        /// <remarks>
        /// The type in the list is only non-null if the index is on a field
        /// of type JSON and is explicitly typed. If using a server that does
        /// not support this information, this property will be <c>null</c>.
        /// </remarks>
        /// <value>
        /// The list of field types.
        /// </value>
        public IReadOnlyList<string> FieldTypes { get; internal set; }
    }

}
