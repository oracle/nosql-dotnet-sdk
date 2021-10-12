/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

/*
 * A simple example that
 *   - creates a table
 *   - inserts a row using the put() operation
 *   - reads a row using the get() operation
 *   - drops the table
 *
 * To run against the cloud simulator:
 *     node basic_example.js cloudsim.json
 *
 * To run against the cloud service:
 *     node basic_example.js config.json
 */

namespace Oracle.NoSQL.Driver.SmokeTest
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Driver;

    class Program
    {
        private const string TableName = "SmokeTest";

        private static readonly JsonOutputOptions JsonOutputOptions =
            new JsonOutputOptions
            {
                Indented = true
            };

        // <Main>
        public static async Task Main(string[] args)
        {
            // Console.ReadLine();
            var configFile = args[0];
            try
            {
                using var client = new NoSQLClient(configFile);
                Console.WriteLine("Created NoSQLClient instance");
                await RunSmokeTest(client);
                Console.WriteLine("Success!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetType().Name);
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                for (var inner = ex.InnerException;
                    inner != null;
                    inner = inner.InnerException)
                {
                    Console.WriteLine("Caused by -->");
                    Console.WriteLine(inner.GetType().Name);
                    Console.WriteLine("Error: " + inner.Message);
                    Console.WriteLine(inner.StackTrace);
                }
            }
        }

        private static async Task CreateTable(NoSQLClient client)
        {
            var createDDL = $"CREATE TABLE IF NOT EXISTS {TableName} " +
                "(id LONG, name STRING, price NUMBER, added TIMESTAMP(6), " +
                "details JSON, PRIMARY KEY(id))";
            Console.WriteLine("Create table " + TableName);
            var result = await client.ExecuteTableDDLAsync(createDDL,
                new TableDDLOptions
                {
                    TableLimits = new TableLimits(1, 5, 1)
                });
            Console.WriteLine("Creating table " + TableName);
            Console.WriteLine("Table state: " + result.TableState);

            // Wait for the operation completion
            await result.WaitForCompletionAsync();
            Console.WriteLine("Table {0} is created", result.TableName);
            Console.WriteLine("Table state: " + result.TableState);
        }

        private static async Task DropTable(NoSQLClient client)
        {
            Console.WriteLine("\nDrop table");
            string dropDDL = $"DROP TABLE {TableName}";
            var result = await client.ExecuteTableDDLAsync(dropDDL);
            Console.WriteLine("Dropping table {0}", result.TableName);
            Console.WriteLine("Table state: " + result.TableState);

            // Wait for the table to be removed
            await result.WaitForCompletionAsync();
            Console.WriteLine("Table dropped");
            Console.WriteLine("Table state: " + result.TableState);
        }

        private static async Task PutItem(NoSQLClient client, long id,
            string name, decimal price, DateTime added, string details)
        {
            var value = new MapValue
            {
                ["id"] = id,
                ["name"] = name,
                ["price"] = price,
                ["added"] = added,
                ["details"] = FieldValue.FromJsonString(details)
            };
            Console.WriteLine("\nPut item: " + value.ToJsonString(
                                  JsonOutputOptions));
            var result = await client.PutAsync(TableName, value);
            Console.WriteLine("Success: " + result.Success);
            if (result.ConsumedCapacity != null)
            {
                Console.WriteLine("Put used: " + result.ConsumedCapacity);
            }
        }

        private static async Task GetItem(NoSQLClient client, long id)
        {
            var primaryKey = new MapValue
            {
                ["id"] = id
            };
            Console.WriteLine("\nGet item with primary key: " +
                primaryKey.ToJsonString(JsonOutputOptions));
            var result = await client.GetAsync(TableName, primaryKey);
            var hasRecord = result.Row != null;
            Console.WriteLine("Has item: " + hasRecord);
            if (hasRecord)
            {
                Console.WriteLine("Got item: " + result.Row.ToJsonString(
                                      JsonOutputOptions));
            }
            if (result.ConsumedCapacity != null)
            {
                Console.WriteLine("Get used: " + result.ConsumedCapacity);
            }
        }

        private static async Task DeleteItem(NoSQLClient client, long id)
        {
            var primaryKey = new MapValue
            {
                ["id"] = id
            };
            Console.WriteLine("\nDelete item with primary key: " +
                primaryKey.ToJsonString(JsonOutputOptions));
            var result = await client.DeleteAsync(TableName, primaryKey);

            Console.WriteLine("Success: " + result.Success);
            if (result.ConsumedCapacity != null)
            {
                Console.WriteLine("Delete used: " + result.ConsumedCapacity);
            }
        }

        private static async Task RunSmokeTest(NoSQLClient client)
        {
            await CreateTable(client);

            Console.WriteLine("\nPut new records");
            var idStart = 1000000000000L;
            var now = DateTime.Now;
            await PutItem(client, idStart + 1, "Item1", 1000.12m,
                now - TimeSpan.FromDays(10), null);
            await PutItem(client, idStart + 2, "Item2", .99m,
                now - TimeSpan.FromDays(5), @"
            {
                ""quantity"": 100,
                ""description"": ""building materials"",
                ""categories"": [ ""nails"", ""tape"", ""pliers""]
            }");
            await PutItem(client, idStart + 3, null, 1000000, now, @"
            {
                ""description"": ""factory"",
                ""categories"": null
            }");

            Console.WriteLine("\nRetrieve the records");
            await GetItem(client, idStart + 1);
            await GetItem(client, idStart + 2);
            await GetItem(client, idStart + 3);

            Console.WriteLine("\nUpdate record");
            await PutItem(client, idStart + 2, "Item2", 9.99m, now, @"
            {
                ""quantity"": {
                    ""nails"": 1000000,
                    ""pliers"": 20000,
                    ""tape"": 100000
                },
                ""description"": ""building materials updated"",
                ""categories"": null
            }");
            await GetItem(client, idStart + 2);

            Console.WriteLine("\nDelete record");
            await DeleteItem(client, idStart + 3);
            await GetItem(client, idStart + 3);

            await DropTable(client);
        }

    }
}
