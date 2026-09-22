/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    // Shared by a query runtime and its internal requests, so local shard
    // scans and outgoing sequence numbers use the same topology versions.
    internal sealed class QueryTopologySnapshot
    {
        internal TopologyInfo LegacyTopology { get; }

        internal int LegacySequenceNumber =>
            LegacyTopology?.SequenceNumber ?? -1;

        internal IReadOnlyDictionary<string, TopologyInfo> StoreTopologies
        {
            get;
        }

        internal QueryTopologySnapshot(TopologyInfo legacyTopology,
            IEnumerable<KeyValuePair<string, TopologyInfo>> storeTopologies)
        {
            LegacyTopology = legacyTopology;
            StoreTopologies = new ReadOnlyDictionary<string, TopologyInfo>(
                new Dictionary<string, TopologyInfo>(storeTopologies));
        }
    }
}
