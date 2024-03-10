/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    /// <summary>
    /// Describes the current state of the table.
    /// </summary>
    /// <seealso cref="TableResult"/>
    public enum TableState
    {
        /// <summary>
        /// The table is ready to be used. This is the steady state after
        /// creation or modification.
        /// </summary>
        Active,

        /// <summary>
        /// The table is being created and cannot yet be used.
        /// </summary>
        Creating,

        /// <summary>
        /// The table has been dropped or does not exist.
        /// </summary>
        Dropped,

        /// <summary>
        /// The table is being dropped and cannot be used.
        /// </summary>
        Dropping,

        /// <summary>
        /// The table is being updated. It is available for normal use, but
        /// additional table modification operations are not permitted while
        /// the table is in this state.
        /// </summary>
        Updating
    }

}
