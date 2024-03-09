namespace Oracle.NoSQL.SDK.NsonProtocol
{
    internal static partial class Protocol
    {
        internal const short V3 = 3;
        internal const short V4 = 4;
        
        internal const short SerialVersion = V4;

        internal static class FieldNames
        {
            // request fields
            internal const string AbortOnFail = "a";
            internal const string BindVariables = "bv";
            internal const string CompartmentOCID = "cc";
            internal const string Consistency = "co";
            internal const string ContinuationKey = "ck";
            internal const string Data = "d";
            internal const string DefinedTags = "dt";
            internal const string Durability = "du";
            internal const string End = "en";
            internal const string Etag = "et";
            internal const string ExactMatch = "ec";
            internal const string Fields = "f";
            internal const string FreeFormTags = "ff";
            internal const string GetQueryPlan = "gq";
            internal const string GetQuerySchema = "gs";
            internal const string Header = "h";
            internal const string Idempotent = "ip";
            internal const string IdentityCacheSize = "ic";
            internal const string Inclusive = "in";
            internal const string Index = "i";
            internal const string Indexes = "ix";
            internal const string IsJson = "j";
            internal const string IsPrepared = "is";
            internal const string IsSimpleQuery = "iq";
            internal const string Key = "k";
            internal const string KVVersion = "kv";
            internal const string LastIndex = "li";
            internal const string ListMaxToRead = "lx";
            internal const string ListStartIndex = "ls";
            internal const string MatchVersion = "mv";
            internal const string MaxReadKB = "mr";
            internal const string MaxShardUsagePercent = "ms";
            internal const string MaxWriteKB = "mw";
            internal const string Name = "m";
            internal const string Namespace = "ns";
            internal const string NumberLimit = "nl";
            internal const string NumOperations = "no";
            internal const string Operations = "os";
            internal const string OperationId = "od";
            internal const string Opcode = "o";
            internal const string Path = "pt";
            internal const string Payload = "p";
            internal const string Prepare = "pp";
            internal const string PreparedQuery = "pq";
            internal const string PreparedStatement = "ps";
            internal const string Query = "q";
            internal const string QueryVersion = "qv";
            internal const string Range = "rg";
            internal const string RangePath = "rp";
            internal const string ReadThrottleCount = "rt";
            internal const string Region = "rn";
            internal const string ReturnRow = "rr";
            internal const string ShardId = "si";
            internal const string Start = "sr";
            internal const string Statement = "st";
            internal const string StorageThrottleCount = "sl";
            internal const string Tables = "tb";
            internal const string TableDDL = "td";
            internal const string TableName = "n";
            internal const string TableOCID = "to";
            internal const string TableUsage = "u";
            internal const string TableUsagePeriod = "pd";
            internal const string Timeout = "t";
            internal const string TopoSeqNum = "ts";
            internal const string TraceLevel = "tl";
            internal const string TTL = "tt";
            internal const string Type = "y";
            internal const string UpdateTTL = "ut";
            internal const string Value = "l";
            internal const string Version = "v";
            internal const string WriteMultiple = "wm";
            internal const string WriteThrottleCount = "wt";

            // response fields
            internal const string ErrorCode = "e";
            internal const string Exception = "x";
            internal const string NumDeletions = "nd";
            internal const string RetryHint = "rh";
            internal const string Success = "ss";
            internal const string WmFailure = "wf";
            internal const string WmFailIndex = "wi";
            internal const string WmFailResult = "wr";
            internal const string WmSuccess = "ws";

            // table metadata
            internal const string Initialized = "it";
            internal const string Replicas = "rc";
            internal const string SchemaFrozen = "sf";
            internal const string TableSchema = "ac";
            internal const string TableState = "as";

            // system request
            internal const string SysopResult = "rs";
            internal const string SysopState = "ta";

            // throughput used and limits
            internal const string Consumed = "c";
            internal const string Limits = "lm";
            internal const string LimitsMode = "mo";
            internal const string ReadKB = "rk";
            internal const string ReadUnits = "ru";
            internal const string StorageGB = "sg";
            internal const string WriteKB = "wk";
            internal const string WriteUnits = "wu";

            // row metadata
            internal const string Row = "r";
            internal const string RowVersion = "rv";
            internal const string ExpirationTime = "xp";
            internal const string ModificationTime = "md";

            // operation metadata
            internal const string ExistingModTime = "em";
            internal const string ExistingValue = "el";
            internal const string ExistingVersion = "ev";
            internal const string Generated = "gn";
            internal const string ReturnInfo = "ri";

            // query response fields
            internal const string DriverQueryPlan = "dq";
            internal const string MathContextCode = "mc";
            internal const string MathContextRoundingMode = "rm";
            internal const string MathContextPrecision = "cp";
            internal const string NotTargetTables = "nt";
            internal const string NumResults = "nr";
            internal const string ProxyTopoSeqNum = "pn";
            internal const string QueryOperation = "qo";
            internal const string QueryPlanString = "qs";
            internal const string QueryResults = "qr";
            internal const string QueryResultSchema = "qc";
            internal const string ReachedLimit = "re";
            internal const string ShardIds = "sa";
            internal const string SortPhase1Results = "p1";
            internal const string TableAccessInfo = "ai";
            internal const string TopologyInfo = "tp";

            // replica stats response fields
            internal const string NextStartTime = "ni";
            internal const string ReplicaStats = "ra";
            internal const string ReplicaLag = "rl";
            internal const string Time = "tm";
        }
    }
}
