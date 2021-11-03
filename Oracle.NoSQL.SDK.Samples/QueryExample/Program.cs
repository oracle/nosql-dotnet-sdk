/*-
 * Copyright (c) 2018, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Oracle.NoSQL.SDK;

    // -----------------------------------------------------------------------
    // A simple example that
    //   - creates a table
    //   - create an index
    //   - insert several rows
    //   - prepare and use queries
    //   - drops the table
    // ------------------------------------------------------------------------
    // Prerequisites -
    //
    // For Cloud Service and Cloud Simulator, see tutorial
    // "Connecting an Application to Oracle NoSQL Database Cloud Service".
    //
    // For on-premise NoSQL Database, see tutorial
    // "Connecting an Application to On-Premise Oracle NoSQL Database".
    // ------------------------------------------------------------------------
    // The example takes one command line parameter which is a path to the
    // JSON configuration file.  If not specified, a default configuration is
    // used to connect to the cloud service using default OCI configuration
    // file and default profile name as described in the tutorial.
    // ------------------------------------------------------------------------
    // To run the example, prepare the config file.
    //
    // See template config files in the parent directory:
    //
    // For Cloud Simulator, use "cloudsim.json".  Change the Endpoint if
    // required.
    //
    // For Cloud Service, copy template "cloud_template.json", fill in
    // appropriate property values and remove unused properties.
    //
    // For on-premise NoSQL Database, copy template "kvstore_template.json",
    // fill in appropriate property values and remove unused properties.
    //
    // Run the example as:
    //
    // dotnet run -f <target framework> -- <config file>
    //
    // where:
    //   - <target framework> is target framework moniker, supported values
    //     are netcoreapp3.1 and net5.0
    //   - <config file> is the JSON config file created as described above
    // ------------------------------------------------------------------------

    public class Program
    {
        private const string Usage =
            "Usage: dotnet run -f <target framework> [-- <config file>]";
        private const string TableName = "users";

        private static readonly JsonOutputOptions JsonOptions =
            new JsonOutputOptions
            {
                Indented = true
            };

        public static async Task Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine(Usage);
                return;
            }

            var configFile = args.Length == 1 ? args[0] : null;

            try
            {
                using var client = configFile != null
                    ? new NoSQLClient(configFile)
                    : new NoSQLClient();

                Console.WriteLine("Created NoSQLClient instance");
                await RunQueryExample(client);
                Console.WriteLine("\nSuccess!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception has occurred:\n{0}: {1}",
                    ex.GetType().FullName, ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nCaused by:\n{0}: {1}",
                        ex.InnerException.GetType().FullName,
                        ex.InnerException.Message);
                }
            }
        }

        private static async Task RunQueryExample(NoSQLClient client)
        {
            // Create a table
            var sql =
                $"CREATE TABLE IF NOT EXISTS {TableName}(id INTEGER, " +
                "name STRING, userInfo JSON, PRIMARY KEY(id))";

            Console.WriteLine("\nCreate table {0}", TableName);
            var tableResult = await client.ExecuteTableDDLWithCompletionAsync(
                sql, new TableLimits(1, 5, 1));
            Console.WriteLine("  Table {0} is created",
                tableResult.TableName);
            Console.WriteLine("  Table state is {0}", tableResult.TableState);

            // Create an index
            sql =
                $"CREATE INDEX IF NOT EXISTS city_idx ON {TableName} " +
                "(userInfo.city AS STRING)";

            Console.WriteLine("\nCreate index city_idx on ${0}", TableName);
            tableResult = await client.ExecuteTableDDLWithCompletionAsync(
                sql);
            Console.WriteLine("  Index city_idx created");
            Console.WriteLine("  Table state is {0}", tableResult.TableState);
            
            // Write some records
            Console.WriteLine("\nInserting records");

            await client.PutAsync(TableName,
                new MapValue
                {
                    ["id"] = 10,
                    ["name"] = "Taylor",
                    ["userInfo"] = new MapValue
                    {
                        ["age"] = 79,
                        ["city"] = "Seattle"
                    }
                });

            await client.PutAsync(TableName,
                new MapValue
                {
                    ["id"] = 33,
                    ["name"] = "Xiao",
                    ["userInfo"] = new MapValue
                    {
                        ["age"] = 5,
                        ["city"] = "Shanghai"
                    }
                });
            
            await client.PutAsync(TableName,
                new MapValue
                {
                    ["id"] = 49,
                    ["name"] = "Supriya",
                    ["userInfo"] = new MapValue
                    {
                        ["age"] = 16,
                        ["city"] = "Bangalore"
                    }
                });

            await client.PutAsync(TableName,
                new MapValue
                {
                    ["id"] = 55,
                    ["name"] = "Rosa",
                    ["userInfo"] = new MapValue
                    {
                        ["age"] = 39,
                        ["city"] = "Seattle"
                    }
                });

            Console.WriteLine("  Inserted 4 records");

            // Find user with name Supriya with a simple query.
            var statement =
                $"SELECT * FROM {TableName} WHERE name = 'Supriya'";
            
            Console.WriteLine("\nUse a simple query: {0}", statement);
            var queryEnumerable = client.GetQueryAsyncEnumerable(statement);
            
            await DoQuery(queryEnumerable);

            // Find all the Seattle dwellers with a prepared statement.
            statement = $"DECLARE $city STRING; SELECT * FROM {TableName} " +
                "u WHERE u.userInfo.city = $city";
            
            Console.WriteLine("\nUse a prepared statement: {0}", statement);
            var preparedStatement = await client.PrepareAsync(statement);
            
            const string city = "Seattle";
            Console.WriteLine(
                "  Set variable $city to \"{0}\" in prepared statement",
                city);
            preparedStatement.Variables["$city"] = city;

            // We limit number of rows to 1 in each query result to
            // demonstrate paging of query results.
            queryEnumerable = client.GetQueryAsyncEnumerable(preparedStatement,
                new QueryOptions
                {
                    Limit = 1
                });

            await DoQuery(queryEnumerable);

            // Drop the table
            Console.WriteLine("\nDrop table");
            sql = $"DROP TABLE {TableName}";
            tableResult = await client.ExecuteTableDDLWithCompletionAsync(
                sql);

            Console.WriteLine("  Operation completed");
            Console.WriteLine("  Table state is {0}", tableResult.TableState);
        }

        // Iterate over query results.
        // Each query result may have multiple records.
        private static async Task DoQuery(
            IAsyncEnumerable<QueryResult<RecordValue>> queryEnumerable)
        {
            Console.WriteLine("  Query results:");

            await foreach (var result in queryEnumerable)
            {
                foreach (var row in result.Rows)
                {
                    Console.WriteLine();
                    Console.WriteLine(row.ToJsonString(JsonOptions));
                }
            }
        }

    }
}
