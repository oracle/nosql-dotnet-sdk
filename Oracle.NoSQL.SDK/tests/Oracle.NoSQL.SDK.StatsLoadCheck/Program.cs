/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.StatsLoadCheck
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    // Manual/functional stats load tool.  It runs SDK operations against a
    // configured endpoint so stats output can be compared with Java SDK output
    // and with measured workload throughput/latency.
    internal static class Program
    {
        private const string DefaultConfig =
            "Oracle.NoSQL.SDK.Samples/cloudsim.json";
        private const string DefaultOperation = "listTables";
        private const string DefaultProfile = "MORE";
        private const string DefaultTable = "Users";
        private const long DefaultTotal = 1000000;
        private const int DefaultConcurrency = 100;
        private const int DefaultProgressMs = 5000;
        private const int DefaultIntervalSec = 1;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                var options = Options.Parse(args);
                if (options.ShowHelp)
                {
                    Usage();
                    return 0;
                }

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                var config = LoadConfig(options);
                using var client = new NoSQLClient(config);

                Console.WriteLine(
                    "Starting stats load check with { total: " +
                    $"{options.Total}, concurrency: {options.Concurrency}, " +
                    $"profile: '{options.Profile}', operation: " +
                    $"'{options.Operation}' }}");

                var result = await RunLoadAsync(client, options, cts.Token);
                Console.WriteLine(
                    $"progress done={result.Done}/{options.Total} " +
                    $"errors={result.Errors} rate={result.Rate:F2} req/s");

                return result.Errors == 0 ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " +
                    ex.Message);
                for (var inner = ex.InnerException;
                    inner != null;
                    inner = inner.InnerException)
                {
                    Console.Error.WriteLine("Caused by: " +
                        inner.GetType().Name + ": " + inner.Message);
                }

                return 1;
            }
        }

        private static NoSQLConfig LoadConfig(Options options)
        {
            var config = NoSQLConfig.FromJsonFile(options.ConfigPath);
            config.StatsProfile = ParseProfile(options.Profile);
            config.StatsEnableLog = options.EnableLog;
            config.StatsPrettyPrint = options.PrettyPrint;
            config.StatsInterval = TimeSpan.FromSeconds(options.IntervalSec);
            if (config.StatsEnableLog)
            {
                config.StatsLogger = ConsoleStatsLogger.Instance;
            }
            return config;
        }

        private static StatsControl.Profile ParseProfile(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "NONE" => StatsControl.Profile.None,
                "REGULAR" => StatsControl.Profile.Regular,
                "MORE" => StatsControl.Profile.More,
                "ALL" => StatsControl.Profile.All,
                _ => throw new ArgumentException(
                    "Profile must be NONE, REGULAR, MORE, or ALL.")
            };
        }

        private static async Task<LoadResult> RunLoadAsync(
            NoSQLClient client, Options options, CancellationToken ct)
        {
            var next = -1L;
            var done = 0L;
            var errors = 0L;
            var stopwatch = Stopwatch.StartNew();
            using var progressCts = CancellationTokenSource
                .CreateLinkedTokenSource(ct);

            var progressTask = options.ProgressMs > 0
                ? ReportProgressAsync(options, () => done, () => errors,
                    stopwatch, progressCts.Token)
                : Task.CompletedTask;

            var workers = new Task[options.Concurrency];
            for (var workerIndex = 0;
                workerIndex < options.Concurrency;
                workerIndex++)
            {
                workers[workerIndex] = Task.Run(async () =>
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        var index = Interlocked.Increment(ref next);
                        if (index >= options.Total)
                        {
                            return;
                        }

                        try
                        {
                            await RunOperationAsync(client, options, index,
                                ct);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errors);
                        }
                        finally
                        {
                            Interlocked.Increment(ref done);
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workers);
            progressCts.Cancel();
            try
            {
                await progressTask;
            }
            catch (OperationCanceledException)
            {
            }

            stopwatch.Stop();
            return new LoadResult(done, errors,
                done / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001));
        }

        private static async Task ReportProgressAsync(Options options,
            Func<long> getDone, Func<long> getErrors, Stopwatch stopwatch,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(options.ProgressMs, ct);
                var done = getDone();
                var rate = done /
                    Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                Console.WriteLine(
                    $"progress done={done}/{options.Total} " +
                    $"errors={getErrors()} rate={rate:F2} req/s");
            }
        }

        private static Task RunOperationAsync(NoSQLClient client,
            Options options, long index, CancellationToken ct)
        {
            return options.Operation.ToLowerInvariant() switch
            {
                "listtables" => client.ListTablesAsync(ct),
                "gettable" => client.GetTableAsync(options.Table, null, ct),
                "tableusage" or "gettableusage" =>
                    client.GetTableUsageAsync(options.Table, null, ct),
                "table" => RunTableAsync(client, options, ct),
                "get" => client.GetAsync(options.Table,
                    BuildKey(options, index), null, ct),
                "put" => client.PutAsync(options.Table,
                    BuildRow(options, index), null, ct),
                "delete" => client.DeleteAsync(options.Table,
                    BuildKey(options, index), null, ct),
                "multidelete" => client.DeleteRangeAsync(options.Table,
                    BuildKey(options, index), (DeleteRangeOptions)null, ct),
                "writemultiple" => RunWriteMultipleAsync(client, options,
                    index, ct),
                "prepare" => client.PrepareAsync(BuildQuery(options, index),
                    null, ct),
                "query" => client.QueryAsync(BuildQuery(options, index),
                    null, ct),
                "basicflow" => RunBasicFlowAsync(client, options, index, ct),
                "fullflow" => RunFullFlowAsync(client, options, index, ct),
                _ => throw new ArgumentException(
                    "Unknown operation: " + options.Operation)
            };
        }

        private static Task RunTableAsync(NoSQLClient client, Options options,
            CancellationToken ct)
        {
            var ddl = $"CREATE TABLE IF NOT EXISTS {options.Table}" +
                "(id INTEGER, name STRING, PRIMARY KEY(id))";
            return client.ExecuteTableDDLWithCompletionAsync(
                ddl, new TableLimits(100, 100, 1), ct);
        }

        private static Task RunWriteMultipleAsync(NoSQLClient client,
            Options options, long index, CancellationToken ct)
        {
            var deleteKey = options.Keys.Count > 0
                ? BuildKey(options, index)
                : new MapValue
                {
                    ["id"] = ToIntegerField(index - 1)
                };

            var operations = new WriteOperationCollection()
                .AddPut(BuildRow(options, index))
                .AddDelete(deleteKey);
            return client.WriteManyAsync(options.Table, operations, null, ct);
        }

        private static async Task RunBasicFlowAsync(NoSQLClient client,
            Options options, long index, CancellationToken ct)
        {
            await RunTableAsync(client, options, ct);
            await client.GetTableAsync(options.Table, null, ct);
            await client.PutAsync(options.Table, BuildRow(options, index),
                null, ct);
            await client.GetAsync(options.Table, BuildKey(options, index),
                null, ct);
            await client.QueryAsync(BuildQuery(options, index), null, ct);
            await client.DeleteAsync(options.Table, BuildKey(options, index),
                null, ct);
        }

        private static async Task RunFullFlowAsync(NoSQLClient client,
            Options options, long index, CancellationToken ct)
        {
            var tableName = $"{options.Table}_FullFlow_{index}";
            var ddl = $"CREATE TABLE IF NOT EXISTS {tableName}" +
                "(id LONG, seq INTEGER, name STRING, " +
                "PRIMARY KEY(SHARD(id), seq))";

            try
            {
                await client.ExecuteTableDDLWithCompletionAsync(
                    ddl, new TableLimits(100, 100, 1), ct);
                await client.GetTableAsync(tableName, null, ct);

                var row1 = new MapValue
                {
                    ["id"] = index,
                    ["seq"] = 1,
                    ["name"] = $"user-{index}-1"
                };
                var row2 = new MapValue
                {
                    ["id"] = index,
                    ["seq"] = 2,
                    ["name"] = $"user-{index}-2"
                };

                await client.PutAsync(tableName, row1, null, ct);
                await client.PutAsync(tableName, row2, null, ct);
                await client.GetAsync(tableName, FullFlowKey(index, 1),
                    null, ct);
                await client.GetAsync(tableName, FullFlowKey(index, 2),
                    null, ct);
                await client.QueryAsync(
                    $"INSERT INTO {tableName}(id, seq, name) " +
                    $"VALUES({index}, 3, 'user-{index}-3')", null, ct);
                await client.QueryAsync(
                    $"SELECT * FROM {tableName} WHERE id = {index} " +
                    "AND seq = 3", null, ct);
                await client.DeleteAsync(tableName, FullFlowKey(index, 1),
                    null, ct);
            }
            finally
            {
                await client.ExecuteTableDDLWithCompletionAsync(
                    $"DROP TABLE IF EXISTS {tableName}",
                    (TableDDLOptions)null, ct);
            }
        }

        private static MapValue FullFlowKey(long id, int seq) => new()
        {
            ["id"] = id,
            ["seq"] = seq
        };

        private static string BuildQuery(Options options, long index)
        {
            var query = options.Query ??
                $"SELECT * FROM {options.Table}";
            return SubstituteIndex(query, index);
        }

        private static MapValue BuildKey(Options options, long index)
        {
            return BuildMap(options.Keys.Count != 0
                ? options.Keys
                : new List<KeyValuePair<string, string>>
                {
                    new("id", "{i}")
                }, index);
        }

        private static MapValue BuildRow(Options options, long index)
        {
            if (options.Rows.Count != 0)
            {
                return BuildMap(options.Rows, index);
            }

            return new MapValue
            {
                ["id"] = ToIntegerField(index),
                ["name"] = $"user-{index}"
            };
        }

        private static MapValue BuildMap(
            IReadOnlyList<KeyValuePair<string, string>> pairs, long index)
        {
            var map = new MapValue();
            foreach (var pair in pairs)
            {
                map[pair.Key] = ParseFieldValue(
                    SubstituteIndex(pair.Value, index));
            }

            return map;
        }

        private static string SubstituteIndex(string value, long index) =>
            value.Replace("{i}", index.ToString(CultureInfo.InvariantCulture));

        private static FieldValue ParseFieldValue(string value)
        {
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return FieldValue.Null;
            }

            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            if (long.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var longValue))
            {
                return ToIntegerField(longValue);
            }

            if (decimal.TryParse(value, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue;
            }

            return value;
        }

        private static FieldValue ToIntegerField(long value)
        {
            return value >= int.MinValue && value <= int.MaxValue
                ? (FieldValue)(int)value
                : value;
        }

        private static void Usage()
        {
            Console.WriteLine(@"
Usage:
  dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.StatsLoadCheck \
    --framework net10.0 -- [options]

Options:
  --config <file>           Config JSON file.
                            Default: Oracle.NoSQL.SDK.Samples/cloudsim.json
  --operation <name>        listTables|get|getTable|tableUsage|table|put|
                            delete|multiDelete|writeMultiple|prepare|query|
                            basicFlow|fullFlow
                            Default: listTables
  --table <name>            Table name. Default: Users
  --total <number>          Total logical operations. Default: 1000000
  --concurrency <number>    Number of concurrent workers. Default: 100
  --profile <name>          NONE|REGULAR|MORE|ALL. Default: MORE
  --interval-sec <number>   Stats interval seconds. Default: 1
  --pretty-print <bool>     Pretty-print Client stats JSON. Default: true
  --enable-log <bool>       Enable Client stats logging. Default: true
  --progress-ms <number>    Progress print interval. Use 0 to disable.
                            Default: 5000
  --key field=value         Repeatable primary key field for get/delete.
  --row field=value         Repeatable row field for put/writeMultiple.
  --query <sql>             SQL for query/prepare.

High-concurrency note:
  A workload may exceed CloudSim or table throughput limits. The SDK retries
  throttled requests, but an operation is reported as an error if its retry or
  timeout budget is exhausted. Reduce concurrency or increase table capacity
  when retry.throttleCount is high; investigate non-throttling errors separately.

Test commands:
  # Start CloudSim first when using the default cloudsim.json config:
  ./runCloudSim -root ./cloudsim-root -httpPort 8080 -storePort 5010

  # Pure Stats unit tests; CloudSim is not required.
  dotnet test Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests/Oracle.NoSQL.SDK.Tests.csproj \
    --filter StatsTests

  # Real HTTP execution-path stats tests; CloudSim must be running at the
  # endpoint in the selected test config.
  dotnet test Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests/Oracle.NoSQL.SDK.Tests.csproj \
    --framework net10.0 --filter StatsExecutionPathTests

Examples:
  # Manual stats check. CloudSim should be running because the default config
  # is Oracle.NoSQL.SDK.Samples/cloudsim.json.
  dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.StatsLoadCheck \
    --framework net10.0 -- --operation basicFlow --table Users \
    --profile ALL --interval-sec 1 --pretty-print true --enable-log true \
    --total 1 --concurrency 1

  dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.StatsLoadCheck \
    --framework net10.0 -- --operation put --table Users \
    --row id={i} --row name=user-{i} --profile ALL \
    --total 1000 --concurrency 10

  dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.StatsLoadCheck \
    --framework net10.0 -- --operation get --table Users \
    --key id={i} --profile MORE \
    --total 1000 --concurrency 10

  dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.StatsLoadCheck \
    --framework net10.0 -- --operation query --table Users \
    --query ""SELECT * FROM Users WHERE id = 458"" --profile ALL \
    --total 1 --concurrency 1
");
        }

        private sealed class Options
        {
            public string ConfigPath { get; private set; } = DefaultConfig;

            public string Operation { get; private set; } = DefaultOperation;

            public string Profile { get; private set; } = DefaultProfile;

            public string Table { get; private set; } = DefaultTable;

            public string Query { get; private set; }

            public long Total { get; private set; } = DefaultTotal;

            public int Concurrency { get; private set; } =
                DefaultConcurrency;

            public int ProgressMs { get; private set; } = DefaultProgressMs;

            public int IntervalSec { get; private set; } =
                DefaultIntervalSec;

            public bool PrettyPrint { get; private set; } = true;

            public bool EnableLog { get; private set; } = true;

            public bool ShowHelp { get; private set; }

            public List<KeyValuePair<string, string>> Keys { get; } = new();

            public List<KeyValuePair<string, string>> Rows { get; } = new();

            public static Options Parse(string[] args)
            {
                var options = new Options();
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg is "--help" or "-h")
                    {
                        options.ShowHelp = true;
                        continue;
                    }

                    switch (arg)
                    {
                        case "--config":
                            options.ConfigPath = RequireValue(args, ref i,
                                arg);
                            break;
                        case "--operation":
                            options.Operation = RequireValue(args, ref i,
                                arg);
                            break;
                        case "--profile":
                            options.Profile = RequireValue(args, ref i, arg);
                            break;
                        case "--table":
                            options.Table = RequireValue(args, ref i, arg);
                            break;
                        case "--query":
                            options.Query = RequireValue(args, ref i, arg);
                            break;
                        case "--total":
                            options.Total = ParseLong(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--concurrency":
                            options.Concurrency = ParseInt(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--progress-ms":
                            options.ProgressMs = ParseInt(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--interval-sec":
                            options.IntervalSec = ParseInt(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--pretty-print":
                            options.PrettyPrint = ParseBool(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--enable-log":
                            options.EnableLog = ParseBool(
                                RequireValue(args, ref i, arg), arg);
                            break;
                        case "--key":
                            options.Keys.Add(ParsePair(
                                RequireValue(args, ref i, arg), arg));
                            break;
                        case "--row":
                            options.Rows.Add(ParsePair(
                                RequireValue(args, ref i, arg), arg));
                            break;
                        default:
                            throw new ArgumentException(
                                "Unknown option: " + arg);
                    }
                }

                if (options.Total < 0)
                {
                    throw new ArgumentException(
                        "--total must be greater than or equal to zero.");
                }

                if (options.Concurrency <= 0)
                {
                    throw new ArgumentException(
                        "--concurrency must be greater than zero.");
                }

                if (options.ProgressMs < 0)
                {
                    throw new ArgumentException(
                        "--progress-ms must be greater than or equal to zero.");
                }

                if (options.IntervalSec <= 0)
                {
                    throw new ArgumentException(
                        "--interval-sec must be greater than zero.");
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index,
                string option)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException(
                        $"{option} requires a value.");
                }

                index++;
                return args[index];
            }

            private static KeyValuePair<string, string> ParsePair(
                string value, string option)
            {
                var separator = value.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException(
                        $"{option} must be in field=value format.");
                }

                return new KeyValuePair<string, string>(
                    value[..separator], value[(separator + 1)..]);
            }

            private static int ParseInt(string value, string option)
            {
                if (!int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var result))
                {
                    throw new ArgumentException(
                        $"{option} must be an integer.");
                }

                return result;
            }

            private static long ParseLong(string value, string option)
            {
                if (!long.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var result))
                {
                    throw new ArgumentException(
                        $"{option} must be an integer.");
                }

                return result;
            }

            private static bool ParseBool(string value, string option)
            {
                if (!bool.TryParse(value, out var result))
                {
                    throw new ArgumentException(
                        $"{option} must be true or false.");
                }

                return result;
            }
        }

        private readonly record struct LoadResult(long Done, long Errors,
            double Rate);

        private sealed class ConsoleStatsLogger : ILogger
        {
            internal static readonly ConsoleStatsLogger Instance = new();

            public IDisposable BeginScope<TState>(TState state) =>
                NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId,
                TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (formatter != null)
                {
                    Console.WriteLine(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                internal static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
