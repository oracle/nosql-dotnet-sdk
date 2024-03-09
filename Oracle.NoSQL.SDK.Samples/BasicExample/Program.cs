/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Samples
{
    using System;
    using System.Threading.Tasks;
    using Oracle.NoSQL.SDK;

    // -----------------------------------------------------------------------
    // A simple example that
    //   - creates a table
    //   - inserts a row using PutAsync
    //   - reads a row using GetAsync
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
        private const string TableName = "BasicExample";

        // <Main>
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
                await RunBasicExample(client);
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

        private static async Task RunBasicExample(NoSQLClient client)
        {
            // Create a table
            var sql =
                $"CREATE TABLE IF NOT EXISTS {TableName}(cookie_id LONG, " +
                "audience_data JSON, PRIMARY KEY(cookie_id))";

            Console.WriteLine("\nCreate table {0}", TableName);
            var tableResult = await client.ExecuteTableDDLAsync(sql,
                new TableDDLOptions
                {
                    TableLimits = new TableLimits(1, 5, 1)
                });

            Console.WriteLine("  Creating table {0}", TableName);
            Console.WriteLine("  Table state: {0}", tableResult.TableState);

            // Wait for the operation completion
            await tableResult.WaitForCompletionAsync();
            Console.WriteLine("  Table {0} is created",
                tableResult.TableName);
            Console.WriteLine("  Table state: {0}", tableResult.TableState);

            // Write a record
            Console.WriteLine("\nWrite a record");
            var putResult = await client.PutAsync(TableName, new MapValue
            {
                ["cookie_id"] = 456,
                ["audience_data"] = new MapValue
                {
                    ["ip_address"] = "10.0.00.yyy",
                    ["audience_segment"] = new MapValue
                    {
                        ["sports_lover"] = "2019-01-05",
                        ["foodie"] = "2018-12-31"
                    }
                }
            });

            if (putResult.ConsumedCapacity != null)
            {
                Console.WriteLine("  Write used:");
                Console.WriteLine("  " + putResult.ConsumedCapacity);
            }

            // Read a record
            Console.WriteLine("\nRead a record");
            var getResult = await client.GetAsync(TableName, new RecordValue
            {
                ["cookie_id"] = 456
            });
            Console.WriteLine("  Got record:\n");
            Console.WriteLine(getResult.Row.ToJsonString(
                new JsonOutputOptions
                {
                    Indented = true
                }));

            if (getResult.ConsumedCapacity != null)
            {
                Console.WriteLine("\n  Read used:");
                Console.WriteLine("  " + getResult.ConsumedCapacity);
            }

            // Drop the table
            Console.WriteLine("\nDrop table");
            sql = $"DROP TABLE {TableName}";

            tableResult = await client.ExecuteTableDDLAsync(sql);
            Console.WriteLine("  Dropping table {0}", tableResult.TableName);

            // Wait for the table to be removed
            await tableResult.WaitForCompletionAsync();

            Console.WriteLine("  Operation completed");
            Console.WriteLine("  Table state is {0}", tableResult.TableState);
        }

    }
}
