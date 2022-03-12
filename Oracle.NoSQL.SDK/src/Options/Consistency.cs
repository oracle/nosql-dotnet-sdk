/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    /// <summary>
    /// Consistency is used to provide consistency guarantees for read
    /// operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Absolute"/> consistency may be specified to guarantee that
    /// current values are read.
    /// <see cref="Eventual"/> consistency means that the values read may be
    /// very slightly out of date.
    /// <see cref="Absolute"/> consistency results in higher cost,
    /// consuming twice the number of read units for the same data relative to
    /// <see cref="Eventual"/> consistency, and should only be used when
    /// required.
    /// </para>
    /// <para>
    /// It is possible to set a default <see cref="Consistency"/> for a
    /// <see cref="NoSQLClient"/> instance by providing it in the initial
    /// configuration as <see cref="NoSQLConfig.Consistency"/>.  In JSON
    /// configuration file, you may use string values "Eventual" or
    /// "Absolute".  If no consistency is specified for an operation and there
    /// is no default value, <see cref="Eventual"/> is used.
    /// </para>
    /// <para>
    /// <see cref="Consistency"/> may be specified in options for all read
    /// operations.
    /// </para>
    /// </remarks>
    /// <seealso cref="GetOptions"/>
    /// <seealso cref="QueryOptions"/>
    public enum Consistency
    {
        /// <summary>
        /// Absolute consistency.
        /// </summary>
        Absolute,

        /// <summary>
        /// Eventual consistency.
        /// </summary>
        Eventual
    }

}
