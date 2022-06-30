Applications using the Oracle NoSQL Database use tables to store and access
data.  Oracle NoSQL Database .NET SDK supports table and index creation and
removal, reading, updating and deleting records, as well as queries.  This
guide provides an overview of these capabilities.  For complete description
of the APIs and available options, see the API reference.

## Create a NoSQLClient Instance

To use the SDK in your code, add
[Oracle.NoSQL.SDK](xref:Oracle.NoSQL.SDK) namespace:

```csharp
using Oracle.NoSQL.SDK;
```

Class [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) represents the main
access point to the service.  To create an instance of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) you need to provide
appropriate configuration information.  This information is represented by
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) class which instance can
be provided to the constructor of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient). Alternatively, you may
choose to store the configuration information in a JSON configuration file and
use  the constructor of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
that takes the path (absolute or relative to current directory) to that file.

Required configuration properties are different depending on what
[Service Type](xref:Oracle.NoSQL.SDK.ServiceType) is used by your
application.  Supported service types are Oracle NoSQL Cloud Service
([ServiceType.Cloud](xref:Oracle.NoSQL.SDK.ServiceType.Cloud)), Cloud
Simulator
([ServiceType.CloudSim](xref:Oracle.NoSQL.SDK.ServiceType.CloudSim))
and On-Premise Oracle NoSQL Database
([ServiceType.KVStore](xref:Oracle.NoSQL.SDK.ServiceType.KVStore)). All
service types require service endpoint or region and some require
authentication/authorization information.  Other properties are optional and
default values will be used if not explicitly provided.

See
[Connecting an Application to Oracle NoSQL Database Cloud Service](connect-cloud.md)
tutorial on how to configure and connect to the Oracle NoSQL Database Cloud
Service as well as the Cloud Simulator.

See
[Connecting an Application to On-Premise Oracle NoSQL Database](connect-on-prem.md)
tutorial on how to configure and connect to the On-Premise Oracle NoSQL
Database.

The first example below creates instance of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) for the Cloud Service
using [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig). It also adds a
default compartment and overrides some default timeout values in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig):

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        Timeout = TimeSpan.FromSeconds(10),
        TableDDLTimeout = TimeSpan.FromSeconds(20),
        Compartment = "mycompartment",
        AuthorizationProvider = new IAMAuthorizationProvider(
            "~/myapp/.oci/config", "Jane")
    });
```

The second example stores the same configuration in a JSON file *config.json*
and uses it to create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
instance:

*config.json*:

```json
{
    "Region": "us-ashburn-1",
    "Timeout": 20000,
    "TableDDLTimeout": 40000,
    "compartment": "mycompartment",
    "AuthorizationProvider":
    {
        "AuthorizationType": "IAM",
        "ConfigFile": "~/myapp/.oci/config",
        "ProfileName": "Jane"
    }
}
```

Application code:

```csharp
var client = new NoSQLClient("config.json");
```

Note that it may not be possible to store the configuration in a file if
it has property values that cannot be represented as JSON types.  These cases
include custom retry handler, custom authoirzation provider, custom
credentials provider, etc. (see
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) for details). In this
case, use [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) instance as in
the first example above.

### Using a NoSQLClient Instance

Most of the methods of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) are
asynchronous and return a *Task<TResult>* instance representing a result of
a particular operation.  There are different classes for differnet operation
results, such as [GetResult](xref:Oracle.NoSQL.SDK.GetResult`1),
[PutResult](xref:Oracle.NoSQL.SDK.PutResult`1),
[TableResult](xref:Oracle.NoSQL.SDK.TableResult), etc.

Instances of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) are
thread-safe and async-safe and they are expected to be shared among threads.

[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) has memory and network
resources associated with it. It implements
[IDisposable](xref:System.IDisposable) interface and must be disposed when
the application is done using it via calling
[Dispose](xref:Oracle.NoSQL.SDK.NoSQLClient.Dispose) method or via *using*
statement.  Failure to dispose of the instance may cause an application to
hang on exit.

In most cases, you only need once
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance for the lifetime
of your application.  The creation of multiple instances or repeatedly
creating and disposing of the instance incurs additional resource overheads
without providing any performance benefit.

Note that the result objects returned by the methods of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) are not thread-safe and
should only be used by one thread at a time unless synchronized externally.

Most of the methods of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
take options as an optional argument that allows you to customize the behavior
of each operation.  Options for different operations are represented by
different classes, such as [GetOptions](xref:Oracle.NoSQL.SDK.GetOptions),
[PutOptions](xref:Oracle.NoSQL.SDK.PutOptions),
[TableDDLOptions](xref:Oracle.NoSQL.SDK.TableDDLOptions), etc.  Their
properties may override the settings in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) object used to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.

Most of the methods of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
also take [CancellationToken](xref:System.Threading.CancellationToken) as an
optional last argument, which allows you to cancel an operation.  However,
note that this is only a driver-side cancellation and provides no guarantees
as to whether the operation was performed by the service.

## <a name="create_tables"></a>Create Tables and Indexes

Learn how to create tables and indexes in the Oracle NoSQL Database.

Creating a table is the first step of developing your application.  To create
tables and execute other Data Definition Language (DDL) statements, such as
creating, modifying and dropping tables as well as creating and dropping
indexes, use methods
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
and
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*).

Before creating a table, learn about:

* Table design for the Oracle NoSQL Database. See
[Table Design](https://docs.oracle.com/en/cloud/paas/nosql-cloud/csnsd/table-design.html#GUID-7A409201-F240-4DE5-A0C1-545ADFCBFB77).
* Cloud limits. See
[Oracle NoSQL Database Cloud Limits](https://docs.oracle.com/pls/topic/lookup?ctx=en/cloud/paas/nosql-cloud&id=CSNSD-GUID-30129AB3-906B-4E71-8EFB-8E0BBCD67144).

Examples of DDL statements are:

```sql

   /* Create a new table called users */
   CREATE IF NOT EXISTS users (id INTEGER, name STRING, PRIMARY KEY (id));

   /* Create a new table called users and with TTL value to of days */
   CREATE IF NOT EXISTS users (id INTEGER, name STRING, PRIMARY KEY (id))
   USING TTL 4 days;

   /* Create a new index called nameIdx on the name field in the users table */
   CREATE INDEX IF NOT EXISTS nameIdx ON users(name);
```

Methods
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
and
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*)
return
*Task<[TableResult](xref:Oracle.NoSQL.SDK.TableResult)>*.
[TableResult](xref:Oracle.NoSQL.SDK.TableResult) instance contains status
of DDL operation such as
[TableState](xref:Oracle.NoSQL.SDK.TableResult.TableState), table schema
and [TableLimits](xref:Oracle.NoSQL.SDK.TableResult.TableLimits).

Each of these methods comes with several overloads.  In particular, you may
pass options for the DDL operation as
[TableDDLOptions](xref:Oracle.NoSQL.SDK.TableDDLOptions).

When creating a table, you must specify its
[TableLimits](xref:Oracle.NoSQL.SDK.TableLimits).  Table limits specify
maximum throughput and storage capacity for the table as the amount of
read units, write units and Gigabytes of storage.  You may use an overload
that takes *tableLimits* parameter or pass table limits as
[TableLimits](xref:Oracle.NoSQL.SDK.TableDDLOptions.TableLimits)
property of [TableDDLOptions](xref:Oracle.NoSQL.SDK.TableDDLOptions).

Note that these are potentially long running operations.  The method
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
only launches the specified DDL operation by the service and does not
wait for its completion.  The resulting
[TableResult](xref:Oracle.NoSQL.SDK.TableResult) will most likely have one
of intermediate table states such as
[TableState.Creating](xref:Oracle.NoSQL.SDK.TableState.Creating),
[TableState.Dropping](xref:Oracle.NoSQL.SDK.TableState.Dropping)
or
[TableState.Updating](xref:Oracle.NoSQL.SDK.TableState.Updating)
(the latter happens when table is in the process of being altered by
*ALTER TABLE* statement, table limits are being changed or one of the table
indexes is being created or dropped).

When the underlying operation completes, the table state should change to
[TableState.Active](xref:Oracle.NoSQL.SDK.TableState.Active) or
[TableState.Dropped](xref:Oracle.NoSQL.SDK.TableState.Dropped)
(if the DDL operation was *DROP TABLE*).

You may asynchronously wait for table DDL operation completion by calling
[WaitForCompletionAsync](xref:Oracle.NoSQL.SDK.TableResult.WaitForCompletionAsync*)
on the returned [TableResult](xref:Oracle.NoSQL.SDK.TableResult) instance.

You may also get current table status by calling one of overloads of
[GetTableAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*) method and
passing a table name or
[TableResult](xref:Oracle.NoSQL.SDK.TableResult) instance from
the DDL operation (the latter will also provide information on any errors
occured during the DDL operation).

If you are only need to know the DDL operation completion and not any of its
intermediate states, use
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*)
methods.  These methods return only when the DDL operation is fully completed
by the service or throw an exception if the execution of the DDL operation
failed.  The resulting
[TableResult](xref:Oracle.NoSQL.SDK.TableResult) instance will
have table state
[TableState.Active](xref:Oracle.NoSQL.SDK.TableState.Active) or
[TableState.Dropped](xref:Oracle.NoSQL.SDK.TableState.Dropped)
(if the DDL operation was *DROP TABLE*).

```csharp
var client = new NoSQLClient("config.json");
............................................

try
{
    var statement =
        "CREATE TABLE IF NOT EXISTS users(id INTEGER, " +
        "name STRING, PRIMARY KEY(id))";

    var result = await client.ExecuteTableDDLAsync(statement,
        new TableLimits(20, 10, 5));

    await result.WaitForCompletionAsync();
    Console.WriteLine("Table users created.");
}
catch(Exception ex)
{
    // handle exceptions
}
```

Note that
[WaitForCompletionAsync](xref:Oracle.NoSQL.SDK.TableResult.WaitForCompletionAsync*)
will change the calling
[TableResult](xref:Oracle.NoSQL.SDK.TableResult) instance to reflect the
operation completion.

Alternatively you may use
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*).
Substitute the statements in the try-catch block with the following:

```csharp
var statement =
    "CREATE TABLE IF NOT EXISTS users(id INTEGER, " +
    "name STRING, PRIMARY KEY(id))";

await client.ExecuteTableDDLWithCompletionAsync(statement,
    new TableLimits(20, 10, 5));
Console.WriteLine("Table users created.");
```

(Note that above we ignored the returned result from
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*)).

You need not specify
[TableLimits](xref:Oracle.NoSQL.SDK.TableDDLOptions.TableLimits)
for any DDL operation other than *CREATE TABLE*.  You may also change table
limits of an existing table by calling
[SetTableLimitsAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsAsync*)
or
[SetTableLimitsWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsWithCompletionAsync*)
methods.  They have the same operation completion semantics as
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
and
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*).

## Data Representation

To represent data from Oracle NoSQL database tables, the driver uses
[FieldValue](xref:Oracle.NoSQL.SDK.FieldValue) class which serves as a base
class for a collection of classes representing instances of all supported
data types.  Instances of [FieldValue](xref:Oracle.NoSQL.SDK.FieldValue)
are used to represent values of fields in table rows or query results as well
as the rows themselves.  See [FieldValue](xref:Oracle.NoSQL.SDK.FieldValue)
for detailed description of these classes and how they are mapped to data
types of Oracle NoSQL Database.

Here we will note some important features:

* There are subclasses representing atomic data types such as
[IntegerValue](xref:Oracle.NoSQL.SDK.IntegerValue),
[StringValue](xref:Oracle.NoSQL.SDK.StringValue),
[TimestampValue](xref:Oracle.NoSQL.SDK.TimestampValue),
[NullValue](xref:Oracle.NoSQL.SDK.NullValue), etc. as well as complex
types such as [ArrayValue](xref:Oracle.NoSQL.SDK.ArrayValue),
[MapValue](xref:Oracle.NoSQL.SDK.MapValue) and
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue).
* Both [MapValue](xref:Oracle.NoSQL.SDK.MapValue) and
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) represent dictionaries of
keys and values (with string keys and values of type
[FieldValue](xref:Oracle.NoSQL.SDK.FieldValue)).  The keys of
[MapValue](xref:Oracle.NoSQL.SDK.MapValue) are unordered but the keys of
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) preserve their original
insertion order (and thus iteration order over keys).  Thus table rows may be
represented by either [MapValue](xref:Oracle.NoSQL.SDK.MapValue) or
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) depending on whether the
field order needs to be preserved.  Note that every
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) instance is also a
[MapValue](xref:Oracle.NoSQL.SDK.MapValue).
* The field order is not important for values provided to the driver, such as
for row values provided to *Put* operations or primary key values provided
to *Get* operations, because the order of the table fields is already known
on the back end.  Thus these operations take these values as
[MapValue](xref:Oracle.NoSQL.SDK.MapValue) instances. On the other hand,
values returned by the driver, such as rows returned by *Get* or *Query*
operations have well-defined order of fields and thus are returned as
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) instances.
* Each field value supports conversion to and from JSON.  You may create an
instance of field value from a JSON string via
[FieldValue.FromJsonString](xref:Oracle.NoSQL.SDK.FieldValue.FromJsonString*)
or convert an instance of field value to JSON string via
[FieldValue.ToJsonString](xref:Oracle.NoSQL.SDK.FieldValue.ToJsonString*).
Note that [FieldValue.ToString](xref:Oracle.NoSQL.SDK.FieldValue.ToString)
also returns JSON string and it is implicitly used by *Console.WriteLine*
statements in the examples below.

See examples below on how to create and use
[FieldValue](xref:Oracle.NoSQL.SDK.FieldValue) instances.

Note that the classes representing operation results that may contain a row
value such as [PutResult](xref:Oracle.NoSQL.SDK.PutResult`1),
[GetResult](xref:Oracle.NoSQL.SDK.GetResult`1),
[QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1) are designed to
support any type of row value (to be used when the SDK is extended to support
class mapping) and thus are generic.  Currently the only type parameter in use
is [RecordValue](xref:Oracle.NoSQL.SDK.RecordValue). For example, the
result of *Get* operation is
[GetResult](xref:Oracle.NoSQL.SDK.GetResult`1)<[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue)>.
See result descriptions in the sections below.

## Add Data

Add rows to your table.

When you store data in table rows, your application can easily retrieve, add
to or delete information from the table.

Method [PutAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutAsync*) and related
methods
[PutIfAbsentAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfAbsentAsync*),
[PutIfPresentAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfPresentAsync*)
and
[PutIfVersionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfPresentAsync*)
are used to insert a single row into the table or update a single row.

These methods can be used for unconditional and conditional puts:

* Use [PutAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutAsync*) (without
conditional options) to insert a new row or overwrite existing row with the
same primary key if present.  This is unconditional put.
* Use
[PutIfAbsentAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfAbsentAsync*) to
insert a new row only if the row with the same primary key does not exist.
* Use
[PutIfPresentAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfPresentAsync*) to
overwrite existing row only if the row with the same primary key exists.
* Use
[PutIfVersionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutIfPresentAsync*) to
overwrite existing row only if the row with the same primary key exists and
its [RowVersion](xref:Oracle.NoSQL.SDK.RowVersion) matches a
specific version.

These methods take the value of the row as
[MapValue](xref:Oracle.NoSQL.SDK.MapValue), thus you can pass either
[MapValue](xref:Oracle.NoSQL.SDK.MapValue) or
[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue).  The field names should
be the same as the table column names (where a field is omitted, it is
equivalient to inserting SQL NULL in its place).

You may also pass options to each of these methods as
[PutOptions](xref:Oracle.NoSQL.SDK.PutOptions).  One important option is
[PutOptions.TTL](xref:Oracle.NoSQL.SDK.PutOptions.TTL) which represents
time to live and allows you to put an expiration on the table row.  For more
details, see [TimeToLive](xref:Oracle.NoSQL.SDK.TimeToLive).

Among other options, [PutOptions](xref:Oracle.NoSQL.SDK.PutOptions) class
provides properties [IfAbsent](xref:Oracle.NoSQL.SDK.PutOptions.IfAbsent),
[IfPresent](xref:Oracle.NoSQL.SDK.PutOptions.IfPresent) and
[MatchVersion](xref:Oracle.NoSQL.SDK.PutOptions.MatchVersion) that can be
used with [PutAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutAsync*) for
conditional put operations, instead of using one of the *PutIf...* methods
outlined above.  You may choose whichever approach is most convenient.  See
[PutOptions](xref:Oracle.NoSQL.SDK.PutOptions) for details.

Each of the *Put* methods above returns
*Task<[PutResult](xref:Oracle.NoSQL.SDK.PutResult`1)<[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue)>>*.
[PutResult](xref:Oracle.NoSQL.SDK.PutResult`1) instance contains info
about a completed *Put* operation, such as success status (conditional put
operations may fail if the corresponding condition was not met) and the
resulting [RowVersion](xref:Oracle.NoSQL.SDK.RowVersion).

To add rows to your table:

```csharp
var client = new NoSQLClient("config.json");
............................................

var tableName = "users";

try
{
    // Uncondintional put, should succeed.
    var result = await client.PutAsync(tableName,
        new MapValue
        {
            ["id"] = 1,
            ["name"] = "John"
        });

    // This Put will fail because the row with the same primary
    // key already exists.
    result = await client.PutIfAbsentAsync(tableName,
        new MapValue
        {
            ["id"] = 1,
            ["name"] = "Jane"
        });

    // Expected output: PutIfAbsentAsync failed.
    Console.WriteLine("PutIfAbsentAsync {0}.",
        result.Success ? "succeeded" : "failed");

    // This Put will succeed because the row with the same primary
    // key exists.
    result = await client.PutIfPresentAsync(tableName,
        new MapValue
        {
            ["id"] = 1,
            ["name"] = "Jane"
        });

    // Expected output: PutIfPresentAsync succeeded.
    Console.WriteLine("PutIfPresentAsync {0}.",
        result.Success ? "succeeded" : "failed");

    var rowVersion = result.Version;

    // This Put will succeed because the version matches existing
    // row.
    result = await client.PutIfVersionAsync(
        tableName,
        new MapValue
        {
            ["id"] = 1,
            ["name"] = "Kim"
        }),
        rowVersion);

    // Expected output: PutIfVersionAsync succeeded.
    Console.WriteLine("PutIfVersionAsync {0}.",
        result.Success ? "succeeded" : "failed");

    // This Put will fail because the previous Put has changed
    // the row version, so the old version no longer matches.
    result = await client.PutIfVersionAsync(
        tableName,
        new MapValue
        {
            ["id"] = 1,
            ["name"] = "June"
        }),
        rowVersion);

    // Expected output: PutIfVersionAsync failed.
    Console.WriteLine("PutIfVersionAsync {0}.",
        result.Success ? "succeeded" : "failed");

    // Put a new row with TTL indicating expiration in 30 days.
    result = await client.PutAsync(tableName,
        new MapValue
        {
            ["id"] = 2,
            ["name"] = "Jack"
        }),
        new PutOptions
        {
            TTL = TimeToLive.OfDays(30)
        });
}
catch(Exception ex)
{
    // handle exceptions
}
```

Note that [Success](xref:Oracle.NoSQL.SDK.PutResult`1.Success) property
of the result only indicates successful completion as related to conditional
*Put* operations (i.e. whether the condition was satisfied and thus the
operation completed) and is always *true* for unconditional Puts.  If the
*Put* operation fails for any other reason, an exception will be thrown.
See [Handle Exceptions](#exceptions).

You can perform a sequence of put operations on a table that share the same
shard key using
[PutManyAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutManyAsync*) method.
This sequence will be executed within the scope of single transaction, thus
making this operation atomic.  You can also call
[WriteManyAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*)
to perform a sequence that includes both *Put* and *Delete* operations.

Using fields of data type *JSON* allows more flexibility in the use of data
as the data in JSON field does not have a predefined schema.  To put value
into a JSON field, supply a [MapValue](xref:Oracle.NoSQL.SDK.MapValue)
instance as its field value as part of the row value. You may also create its
value from a JSON string via
[FieldValue.FromJsonString](xref:Oracle.NoSQL.SDK.FieldValue.FromJsonString*).

## Read Data

Learn how to read data from your table.

You can read a single row using the
[GetAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.GetAsync*) method. This method
allows you to retrieve a row based on its primary key value. In order to read
multiple rows in a single operation, see [Use Queries](#queries).

This method takes the primary key value as
[MapValue](xref:Oracle.NoSQL.SDK.MapValue).  The field names should
be the same as the table primary key column names. You may also pass options
as [GetOptions](xref:Oracle.NoSQL.SDK.GetOptions).

You can set consistency of a read operation using
[Consistency](xref:Oracle.NoSQL.SDK.Consistency) enumeration. By default
all read operations are eventually consistent (see
[Consistency.Eventual]([Consistency](xref:Oracle.NoSQL.SDK.Consistency.Eventual)).
This type of read is less costly than those using absolute consistency (see
[Consistency.Absolute]([Consistency](xref:Oracle.NoSQL.SDK.Consistency.Absolute)).
The default consistency for read operations may be set as
[Consistency](xref:Oracle.NoSQL.SDK.NoSQLConfig.Consistency) property of
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).  You may also change
the consistency for a single *Get* operation by using
[Consistency](xref:Oracle.NoSQL.SDK.GetOptions.Consistency) property of
[GetOptions](xref:Oracle.NoSQL.SDK.GetOptions).

[GetAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.GetAsync*) method returns
*Task<[GetResult](xref:Oracle.NoSQL.SDK.GetResult`1)<[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue)>>*.
[GetResult](xref:Oracle.NoSQL.SDK.GetResult`1) instance contains the
returned [Row](xref:Oracle.NoSQL.SDK.GetResult`1.Row), the row
[Version](xref:Oracle.NoSQL.SDK.GetResult`1.Version) and other
information. If the row with the provided primary key does not exist in the
table, the values of both [Row](xref:Oracle.NoSQL.SDK.GetResult`1.Row)
and [Version](xref:Oracle.NoSQL.SDK.GetResult`1.Version) properties
will be *null*.

```csharp
var client = new NoSQLClient("config.json");
..................................................

var tableName = "users";

try
{
    var result = await client.GetAsync(tableName,
        new MapValue
        {
            ["id"] = 1
        });

    // Continuing from the Put example, the expected output will be:
    // { "id": 1, "name": "Kim" }
    Console.WriteLine("Got row: {0}", result.row);

    // Use absolute consistency.
    result = await client.GetAsync(tableName,
        new MapValue
        {
            ["id"] = 2
        }),
         new GetOptions
         {
            Consistency = Consistency.Absolute
         });

    // The expected output will be:
    // { "id": 2, "name": "Jack" }
    Console.WriteLine("Got row with absolute consistency: {0}",
        result.row);

    // Continuing from the Put example, the expiration time should be
    // 30 days from now.
    Console.WriteLine("Expiration time: {0}", result.ExpirationTime)
}
catch(Exception ex)
{
    // handle exceptions
}
```

## <a name="queries"></a>Use Queries

Learn about  using queries in your application.

The Oracle NoSQL Database provides a rich query language to read and
update data. See
[SQL For NoSQL Specification](http://www.oracle.com/pls/topic/lookup?ctx=en/cloud/paas/nosql-cloud&id=sql_nosql)
for a full description of the query language.

To execute a query, you may call
[QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*) method or
call
[GetQueryAsyncEnumerable]((xref:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*))
method and iterate over the resulting async enumerable.

You may pass options to each of these methods as
[QueryOptions](xref:Oracle.NoSQL.SDK.QueryOptions).
[QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*) method (as well as
each iteration step over
[GetQueryAsyncEnumerable]((xref:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*))
return
*Task<[QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1)<[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue)>>*.
[QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1) contains query results
as a list of [RecordValue](xref:Oracle.NoSQL.SDK.RecordValue) instances,
as well as other information.

When your query specifies a complete primary key (or you are executing an
*INSERT* statement, see below), it is sufficient to call
[QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*) once:

```csharp
var client = new NoSQLClient("config.json");
..................................................

try {
    var result = await client.QueryAsync(
    "SELECT * FROM users WHERE id = 1");

    // Because we select by primary key, there can be at most one record.
    if (result.Rows.Count > 0)
    {
        Console.WriteLine("Got record: {0}.", result.Rows[0]);
    }
    else
    {
        Console.WriteLine("Got no records.");
    }
}
catch(Exception ex)
{
    // handle exceptions
}
```

For other queries, this is not the case.

The amount of data returned by the query is limited by the system.  It could
also be further limited by setting
[MaxReadKB](xref:Oracle.NoSQL.SDK.QueryOptions.MaxReadKB) property of
[QueryOptions](xref:Oracle.NoSQL.SDK.QueryOptions).  This means that one
invocation of [QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*)
may not return all available results.  This situation is dealt with by using
continuation key.  Non-null
[ContinuationKey](xref:Oracle.NoSQL.SDK.QueryResult`1.ContinuationKey)
in [QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1) means that
more more query results may be available.  This means that queries should run
in a loop, looping until the continuation key becomes *null*.

Note that it is possible for query to return now rows
([QueryResult.Rows](xref:Oracle.NoSQL.SDK.QueryResult`1.Rows) is
empty) yet have not-null continuation key, which means that the query should
continue looping.  See
[QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*) for more
details.

To continue the query, set
[ContinuationKey](xref:Oracle.NoSQL.SDK.QueryOptions.ContinuationKey)
in the [QueryOptions](xref:Oracle.NoSQL.SDK.QueryOptions) for
the next call to [QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*)
and loop until the continuation key becomes *null*:

The following example executes the query and prints query results:

```csharp
var client = new NoSQLClient("config.json");
..................................................

var options = new QueryOptions();

try {
    do {
        var result = await client.QueryAsync(
            "SELECT id, name FROM users ORDER BY name",
            options);
        foreach(var row of result.Rows) {
            Console.WriteLine(row);
        }
        options.ContinuationKey = result.ContinuationKey;
    }
    while(options.ContinuationKey != null);
}
catch(Exception ex)
{
    // handle exceptions
}
```

Another way to execute the query in a loop is to use
[GetQueryAsyncEnumerable](xref:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*).
It returns an instance of
[IAsyncEnumerable](xref:System.Collections.Generic.IAsyncEnumerable`1)<[QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1)>
that can be iterated over.  Each iteration step returns a portion of the query
results as [QueryResult](xref:Oracle.NoSQL.SDK.QueryResult`1).  For more
information on async enumerables, see
[Iterating With Async Enumerables in C# 8](https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8).

The following example executes the same query as in previous example:

```csharp
var client = new NoSQLClient("config.json");
..................................................

try {
    await foreach(var result in client.GetQueryAsyncEnumerable(
        "SELECT id, name FROM users ORDER BY name"))
    {
        foreach(var row of result.Rows) {
            Console.WriteLine(row);
        }
    }
}
catch(Exception ex)
{
    // handle exceptions
}
```

Note that in addition to *SELECT* queries, Oracle NoSQL Database query
language allows you to insert, update and delete records by executing
*INSERT*, *UPDATE* and *DELETE* statements.  For example, to insert a record:

```csharp
var client = new NoSQLClient("config.json");
..................................................

try {
    var result = await client.QueryAsync(
        "INSERT INTO items VALUES(3, 'Ravi')");

    // Because the insert specifies complete primary key,
    // we don't need to loop.
    if (result.Rows.Count > 0)
    {
        // Expected result: { "NumRowsInserted": 1 }
        Console.WriteLine(result.Rows[0]);
    }
    else
    {
        throw new InvalidOperationException(
            "Insert query returned no results");
    }
}
catch(Exception ex)
{
    // handle exceptions
}
```

As with *SELECT* queries, updates and deletes that don't specify a complete
primary key must be executed in a loop.  For example:

```csharp
var client = new NoSQLClient("config.json");
..................................................

try {
    await foreach(var result in client.GetQueryAsyncEnumerable(
        "DELETE FROM MyLargeTable"))
    {
        foreach(var row of result.Rows) {
            Console.WriteLine(row);
        }
    }
}
catch(Exception ex)
{
    // handle exceptions
}
```

The above example will print a result like *{ "NumRowsDeleted": 100000 }*,
indicating the number of deleted rows.  It is possible that the above query
may loop several times before returning the result record, especially for
large tables.

As with the amount of data read by the query, the amount of data written is
also limited by the system and can be further limited by setting
[MaxWriteKB](xref:Oracle.NoSQL.SDK.QueryOptions.MaxWriteKB) of
[QueryOptions](xref:Oracle.NoSQL.SDK.QueryOptions). In addition, the read
limit may also apply to *UPDATE* and *DELETE* queries because of the amount of
data that need to be read before finding the records that match the query
predicate.

Oracle NoSQL Database provides the ability to prepare queries for execution
and reuse. It is recommended that you use prepared queries when you run the
same query for multiple times. When you use prepared queries, the execution is
much more efficient than starting with a SQL statement every time. The query
language and API support query variables to assist with query reuse.

Use [PrepareAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PrepareAsync*) to
prepare the query.  This method returns
*Task<[PreparedStatement](xref:Oracle.NoSQL.SDK.PreparedStatement)*.
[PreparedStatement](xref:Oracle.NoSQL.SDK.PreparedStatement) allows you to
set query variables.  The query methods
[QueryAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*) and
[GetQueryAsyncEnumerable](xref:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*)
have overloads that execute prepared queries by taking
[PreparedStatement](xref:Oracle.NoSQL.SDK.PreparedStatement) as a parameter
instead of the SQL statement.  For example:

```csharp
var client = new NoSQLClient("config.json");
..................................................

try {
    var sql = "DECLARE $name STRING; SELECT * FROM users WHERE " +
        "name = $name";
    var preparedStatement = await client.PrepareAsync(sql);

    // Set value for $name variable and execute the query
    preparedStatement.SetVariable("$name", "Taylor");
    await foreach(var result in client.GetQueryAsyncEnumerable(
        preparedStatement))
    {
        foreach(var row of result.Rows) {
            Console.WriteLine(row);
        }
    }

    // Set different value for $name and re-execute the query.
    preparedStatement.SetVariable("$name", "Jane");
    await foreach(var result in client.GetQueryAsyncEnumerable(
        preparedStatement))
    {
        foreach(var row of result.Rows) {
            Console.WriteLine(row);
        }
    }
}
catch(Exception ex)
{
    // handle exceptions
}
```

## Delete Data

Learn how to delete rows from your table.

To delete a row, use
[DeleteAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteAsync*) method. Pass
to it the table name and primary key of the row to delete.  This method takes
the primary key value as [MapValue](xref:Oracle.NoSQL.SDK.MapValue).  The
field names should be the same as the table primary key column names. You may
also pass options as [DeleteOptions](xref:Oracle.NoSQL.SDK.DeleteOptions).

In addition, you can make delete operation conditional by specifying on a
[RowVersion](xref:Oracle.NoSQL.SDK.RowVersion) of the row that was
previously returned by
[GetAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.GetAsync*) or
[PutAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.PutAsync*).  Use
[DeleteIfVersionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteIfVersionAsync*)
method that takes the row version to match.  Alternatively, you may use
[DeleteAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteAsync*) method and
pass the version as
[MatchVersion](xref:Oracle.NoSQL.SDK.DeleteOptions.MatchVersion)
property of [DeleteOptions](xref:Oracle.NoSQL.SDK.DeleteOptions).

[DeleteAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteAsync*) and
[DeleteIfVersionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteIfVersionAsync*)
methods return
*Task<[DeleteResult](xref:Oracle.NoSQL.SDK.DeleteResult`1)<[RecordValue](xref:Oracle.NoSQL.SDK.RecordValue)>>*.
[DeleteResult](xref:Oracle.NoSQL.SDK.DeleteResult`1) instance contains
success status of the *Delete* operation. *Delete* operation may fail if the
row with given primary key does not exist or this is a conditional *Delete*
and provided row version did not match the existing row version.  In addition,
for conditional *Delete* operation, the result may also contain
[ExistingRow](xref:Oracle.NoSQL.SDK.DeleteResult`1.ExistingRow) and
[ExistingVersion](xref:Oracle.NoSQL.SDK.DeleteResult`1.ExistingVersion) if
the operation failed due to the version mismatch and
[ReturnExisting](xref:Oracle.NoSQL.SDK.DeleteOptions.ReturnExisting)
property was set to *true* in
[DeleteOptions](xref:Oracle.NoSQL.SDK.DeleteOptions).

```csharp
var client = new NoSQLClient("config.json");
..................................................

var tableName = "users";

try
{
    var row = new MapValue
    {
        ["id"] = 1,
        ["name"] = "John"
    };

    var putResult = await client.PutAsync(tableName, row);

    Console.WriteLine("Put {0}.",
        putResult.Success ? "succeeded" : "failed");

    var primaryKey = new MapValue
    {
        ["id"] = 1
    };

    // Unconditional delete, should succeed.
    var deleteResult = await client.DeleteAsync(tableName, primaryKey);

    // Expected output: Delete succeeded.
    Console.WriteLine("Delete {0}.",
        deleteResult.Success ? "succeeded" : "failed");

    // Delete with non-existent primary key, should fail.
    var deleteResult = await client.DeleteAsync(tableName,
        new MapValue
        {
            ["id"] = 200
        });

    // Expected output: Delete failed.
    Console.WriteLine("Delete {0}.",
        deleteResult.Success ? "succeeded" : "failed");

    // Re-insert the row and get the new row version.
    putResult = await client.PutAsync(tableName, row);
    var version = putResult.Version;

    // Delete should succeed because the version matches existing
    // row.
    deleteResult = await client.DeleteIfVersionAsync(tableName,
        primaryKey, version);

    // Expected output: DeleteIfVersion succeeded.
    Console.WriteLine("DeleteIfVersion {0}.",
        deleteResult.Success ? "succeeded" : "failed");

    // Re-insert the row
    putResult = await client.PutAsync(tableName, row);

    // This delete should fail because the last put operation has
    // changed the row version, so the old version no longer matches.
    // The result will also contain existing row and its version because
    // we specified ReturnExisting in DeleteOptions.

    deleteResult = await client.DeleteIfVersionAsync(tableName,
        primaryKey, version);

    // Expected output: DeleteIfVersion failed.
    Console.WriteLine("DeleteIfVersion {0}.",
        deleteResult.Success ? "succeeded" : "failed");

    // Expected output: { "id": 1, "name": "John" }
    Console.WriteLine(result.existingRow);
}
catch(Exception ex)
{
    // handle exceptions
}
```

Note that [Success](xref:Oracle.NoSQL.SDK.DeleteResult`1.Success) property of
the result only indicates whether the row to delete was found and for
conditional *Delete*, whether the provided version was matched.  If the
*Delete* operation fails for any other reason, an exception will be thrown.
See [Handle Exceptions](#exceptions).

You can delete multiple rows having the same shard key in a single
atomic operation using
[DeleteRangeAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*)
method.  This method deletes set of rows based on partial primary key (which
must include a shard key) and optional
[FieldRange](xref:Oracle.NoSQL.SDK.FieldRange) which specifies a range of
values of one of the other (not included into the partial key) primary key
fields.

Similar to queries, the amount of data that can be deleted by this operation
in one call is limited by the system and you may loop over multiple calls to
[DeleteRangeAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*)
using [ContinuationKey](xref:Oracle.NoSQL.SDK.DeleteRangeOptions.ContinuationKey).
Alternatively, you may use
[GetDeleteRangeAsyncEnumerable](xref:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*)
and loop over the result.

Note that in either of this cases, when *DeleteRange* operation is split over
multiple calls, the operation is no longer atomic. See
[DeleteRangeAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*)
and
[GetDeleteRangeAsyncEnumerable](xref:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*)
for more information.

## Modify Tables

Learn how to modify tables. You modify a table to:

* Add new fields to an existing table
* Delete currently existing fields from a table
* To change the default time-to-live (TTL) value
* Modify table limits

Other than modifying table limits, use
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
or
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*)
to modify a table by issuing a DDL statement against this table.

Examples of DDL statements to modify a table are:
```sql
   /* Add a new field to the table */
   ALTER TABLE users (ADD age INTEGER);

   /* Drop an existing field from the table */
   ALTER TABLE users (DROP age);

   /* Modify the default TTL value*/
   ALTER TABLE users USING TTL 4 days;
```

Table limits can be modified using
[SetTableLimitsAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsAsync*)
or
[SetTableLimitsWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsWithCompletionAsync*)
methods.  They take table name and new
[TableLimits](xref:Oracle.NoSQL.SDK.TableLimits) as parameters and return
*Task<[TableResult](xref:Oracle.NoSQL.SDK.TableResult)>*.

Note that as with
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*),
[SetTableLimitsAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsAsync*)
only launches the operation by the service and does not wait for its
completion.  Similarly, you may use
[TableResult.WaitForCompletionAsync](xref:Oracle.NoSQL.SDK.TableResult.WaitForCompletionAsync*)
to asynchronously wait for the operation completion.  Alternatively, you may
call
[SetTableLimitsWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.SetTableLimitsWithCompletionAsync*)
that will return result only when the operation is completed by the service:

```csharp
var client = new NoSQLClient("config.json");
............................................

var tableName = "users";

try
{
    var result = await client.SetTableLimitsWithCompletionAsync(
        tableName, new TableLimits(40, 10, 5));

    // Expected output: Table state is Active.
    Console.WriteLine("Table state is {0}.", result.TableState);

    Console.WriteLine("Table limits have been changed");
}
catch(Exception ex)
{
    // handle exceptions
}
```

## Drop Tables and Indexes

Learn how to drop a table or index that you have created in the Oracle NoSQL
Database.

To drop a table or index, use the *DROP TABLE* or *DROP INDEX* DDL statement,
for example:
```sql

   /* Drop the table named users (implicitly drops any indexes on that table) */
   DROP TABLE users;

   /*
    * Drop the index called nameIndex on the table users. Don't fail if the
    * index doesn't exist
    */
   DROP INDEX IF EXISTS nameIndex ON users;
```

To execute these statements, use
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*)
and
[ExecuteTableDDLWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*).
See [Create Tables and Indexes](#create_tables) for more information.

```csharp
var client = new NoSQLClient("config.json");
............................................

try
{
    // Drop index "nameIndex" on table "users".
    var result = await client.ExecuteTableDDLAsync(
        "DROP INDEX nameIndex ON users");

    // The following may print: Table state is Updating.
    Console.WriteLine("Table state is {0}", result.TableState);

    await result.WaitForCompletionAsync();    

    // Expected output: Table state is Active.
    Console.WriteLine("Table state is {0}.", result.TableState);

    // Drop table "TestTable".
    result = await client.ExecuteTableDDLWithCompletionAsync(
        "DROP TABLE TestTable");

    // Expected output: Table state is Dropped.
    Console.WriteLine("Table state is {0}.", result.TableState);    
}
catch(Exception ex)
{
    // handle exceptions
}
```

## <a name="exceptions"></a>Handle Exceptions

Learn how to handle exceptions.

Your application may need to handle exceptions thrown by the driver.  In most
cases, exceptions may be thrown when calling methods of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient).  It is also possible for
exceptions to be thrown when creating
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance, when using
methods such as
[TableResult.WaitForCompletionAsync](xref:Oracle.NoSQL.SDK.TableResult.WaitForCompletionAsync*)
or when using methods and conversion operations of the
[FieldValue](xref:Oracle.NoSQL.SDK.FieldValue) class hierarchy.

[NoSQLException](xref:Oracle.NoSQL.SDK.NoSQLException) serves as a base
class for many exceptions thrown by the driver.  However, in certain cases
the driver uses standard exception types such as:

* [ArgumentException](xref:System.ArgumentException) and its subclasses such
as [ArgumentNullException](xref:System.ArgumentNullException).  They are
thrown when an invalid argument is passed to a method or when an invalid
configuration (in code or in JSON) is passed to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.
* [TimeoutException](xref:System.TimeoutException) is thrown when an operation
issued by [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) has timed out.
If you are getting many timeout exceptions, you may try to increase the
timeout values in [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) or in
*options* argument passed to the
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) method.
* [InvalidOperationException](xref:System.InvalidOperationException) is thrown
when the service is an invalid state to perform an operation.  It may also
be thrown if the query has failed be cause its processing exceeded the memory
limit specifed in
[QueryOptions.MaxMemoryMB](xref:Oracle.NoSQL.SDK.QueryOptions.MaxMemoryMB)
or
[NoSQLConfig.MaxMemoryMB](xref:Oracle.NoSQL.SDK.NoSQLConfig.MaxMemoryMB).
In this case, you may increase the corresponding memory limit.   Otherwise,
you may retry the operation.  If it still fails, contact Oracle Support for
assistance.
* [InvalidCastException](xref:System.InvalidCastException) and
[OverflowException](xref:System.OverflowException) may occur when working
with sublcasses of [FieldValue](xref:Oracle.NoSQL.SDK.FieldValue) and
trying to cast a value to a type it doesn't support or cast a numeric value
to a smaller type causing arithmetic overflow.  Check the validity of the
conversion in question.  See [FieldValue](xref:Oracle.NoSQL.SDK.FieldValue)
for details.
* [OperationCanceledException](xref:System.OperationCanceledException) and
[TaskCanceledException](xref:System.Threading.Tasks.TaskCanceledException)
if you issued a cancellation of the operation started by a method of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) using the provided
[CancellationToken](xref:System.Threading.CancellationToken).

Other exceptions thrown by the driver are subclasses of
[NoSQLException](xref:Oracle.NoSQL.SDK.NoSQLException).  See API
documentation for more details.

Note: for exceptions indicating that the operation has timed out or was
canceled, such as [TimeoutException](xref:System.TimeoutException),
[OperationCanceledException](xref:System.OperationCanceledException) or
[TaskCanceledException](xref:System.Threading.Tasks.TaskCanceledException),
the driver cannot provide any guarantee whether the operation has or has not
been executed by the service.  E.g. when using *Put* operation to insert a
new row into a table, it is possible that
[TimeoutException](xref:System.TimeoutException) has been thrown because the
service response could not reach the driver due to a network issue, even
though the row has been already inserted into the table.

In addition, exceptions may be split into two broad categories:
* Exceptions that may be retried with the expectation that the operation may
succeed on retry.  In general these are subclasses of
[RetryableException](xref:Oracle.NoSQL.SDK.RetryableException).  These
include throttling exceptions as well as other exceptions where a resource is
temporarily anavailable.  Some other subclasses of
[NoSQLException](xref:Oracle.NoSQL.SDK.NoSQLException) may also be
retryable depending on the conditions under which the exception occurred.  See
API documentation for details.  In addition, network-related errors are
retryable because most network conditions are temporary.
* Exceptions that should not be retried because they will still fail after
retry.  They include exceptions such as
[TableNotFoundException](xref:Oracle.NoSQL.SDK.TableNotFoundException),
[TableExistsException](xref:Oracle.NoSQL.SDK.TableExistsException) and
others as well as standard exceptions discussed above such as
[ArgumentException](xref:System.ArgumentException).

You can determine if a given instance of
[NoSQLException](xref:Oracle.NoSQL.SDK.NoSQLException) is retryable by
checking its
[IsRetryable](xref:Oracle.NoSQL.SDK.NoSQLException.IsRetryable) property.

### Retry Handler

By default, the driver will automatically retry operations that threw a
retryable exception (see above).  The driver uses retry handler to control
operation retries.  The retry hanlder determines:

* Whether and how many times the operation will be retried.
* How long to wait before each retry.

All retry handlers implement
[IRetryHandler](xref:Oracle.NoSQL.SDK.IRetryHandler) interface.  This
interface provides two methods, one to determine if the operation in its
current state should be retried and another to determine a retry delay befor
the next retry.  You have a choice to use default retry handler or set your
own retry handler as
[RetryHandler](xref:Oracle.NoSQL.SDK.NoSQLConfig.RetryHandler)
property of [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) when
creating [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.

Note: retries are only performed within the timeout period alloted to the
operation and configured as one of timeout properties in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) or in *options*
passed to the [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
method.  If the operation or its retries have not succeded before the timeout
is reached, [TimeoutException](xref:System.TimeoutException) is thrown.

By default, the driver uses
[NoSQLRetryHandler](xref:Oracle.NoSQL.SDK.NoSQLRetryHandler) class
which controls retires based on operation type, exception type and whether
the number of retries performed has reached a preconfigured maximum.  It also
uses exponential backoff delay to wait between retries starting with a
preconfigured base delay.  You may customize the properties such as maximum
number of retries, base delay and others by creating your own instance of
[NoSQLRetryHandler](xref:Oracle.NoSQL.SDK.NoSQLRetryHandler) and
setting it as a
[RetryHandler](xref:Oracle.NoSQL.SDK.NoSQLConfig.RetryHandler)
property in [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).  For
example:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = .....,
        ...............
        RetryHandler = new NoSQLRetryHandler
        {
            MaxRetryAttempts = 20,
            BaseDelay = TimeSpan.FromSeconds(2)
        }
    });
```

You may also specify the same in the JSON configuration file used to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance:

```json
{
    "Region": .....,
    ................
    "RetryHandler": {
        "MaxRetryAttempts": 20,
        "BaseDelay": 2000
    }
}
```

If you don't specify the retry handler, the driver will use an instance of
[NoSQLRetryHandler](xref:Oracle.NoSQL.SDK.NoSQLRetryHandler) with
default values for all parameters.

Alternatively, you may choose to create your own retry handler class by
implementing [IRetryHandler](xref:Oracle.NoSQL.SDK.IRetryHandler)
interface.

The last option is to disable retries alltogether.  You may do this if you
plan to retry the operations within your application instead.  To disable
retries, set
[RetryHandler](xref:Oracle.NoSQL.SDK.NoSQLConfig.RetryHandler)
property of [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) to
[NoRetries](xref:Oracle.NoSQL.SDK.NoSQLConfig.NoRetries):

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = .....,
        ...............
        RetryHandler = NoSQLConfig.NoRetries
    });
```

See [IRetryHandler](xref:Oracle.NoSQL.SDK.IRetryHandler) and
[NoSQLRetryHandler](xref:Oracle.NoSQL.SDK.NoSQLRetryHandler) for
details.

## Handle Resource Limits

This section is relevant only for the Oracle NoSQL Database Cloud Service
(including Cloud Simulator) and not for the on-premise NoSQL Database.

Programming in a resource-limited environment can be challenging. Tables have
user-specified throughput limits and if an application exceeds those limits
it may be throttled, which means an operation may fail with one of the
throttling exceptions such as
[ReadThrottlingException](xref:Oracle.NoSQL.SDK.ReadThrottlingException) or
[WriteThrottlingException](xref:Oracle.NoSQL.SDK.WriteThrottlingException).
This is most common when using queries, which can read a lot of data, using up
capacity very quickly. It can also happen for get and put operations that run
in a tight loop.

Even though throttling errors will be retried and using custom
[RetryHandler](xref:Oracle.NoSQL.SDK.NoSQLConfig.RetryHandler) may allow
more direct control over retries, an application should not rely on retries
to handle throttling as this will result in poor performance and inability to
use all of the throughput available for the table.

The better approach would be to avoid throttling entirely by rate-limiting
your application. In this context *rate-limiting* means keeping operation
rates under the limits for the table.

The SDK provides support for rate limiting.  To use rate limiting, you must
set the property
[NoSQLConfig.RateLimitingEnabled](xref:Oracle.NoSQL.SDK.NoSQLConfig.RateLimitingEnabled)
of the initial configuration used to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance, e.g.:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        AuthorizationProvider = new IAMAuthorizationProvider(
            "~/myapp/.oci/config", "Jane"),
        RateLimitingEnabled = true
    });
```

Internally, a pair of rate limiters is created for each table, one for read
operations and another for write operations.  All rate limiters implement
[IRateLimiter](xref:Oracle.NoSQL.SDK.IRateLimiter) interface.

By default, the SDK uses rate-limiting algorithm implemented by
[NoSQLRateLimiter](xref:Oracle.NoSQL.SDK.NoSQLRateLimiter) class.  You may
choose instead to implement custom rate-limiting logic by creating your own
implementation of [IRateLimiter](xref:Oracle.NoSQL.SDK.IRateLimiter)
interface and providing a factory delegate to create your own rate limiters
by setting
[NoSQLConfig.RateLimiterCreator](xref:Oracle.NoSQL.SDK.NoSQLConfig.RateLimiterCreator)
property of the initial configuration.

Note that by default rate limiting in the SDK assumes that read and write
operations are issued from only one
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance. This might not work
as expected when using multiple clients against the same table.  You may
improve this by allocating only a percentage of each table's limits to a given
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance by setting
[NoSQLConfig.RateLimiterPercent](xref:Oracle.NoSQL.SDK.NoSQLConfig.RateLimiterPercent)
property of the initial configuration.

Here is an example of a more custom configuration:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        AuthorizationProvider = new IAMAuthorizationProvider(
            "~/myapp/.oci/config", "Jane"),
        RateLimitingEnabled = true,
        RateLimiterPercent = 20,
        RateLimiterCreator = () => new MyRateLimiter()
    });
```

For more information, see API documentation for
[IRateLimiter](xref:Oracle.NoSQL.SDK.IRateLimiter),
[NoSQLRateLimiter](xref:Oracle.NoSQL.SDK.NoSQLRateLimiter) and
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).

Note that you may further reduce the amount of data read and/or
written (for update queries) in a single iteration step call by setting
[MaxReadKB](xref:Oracle.NoSQL.SDK.QueryOptions.MaxReadKB) and
[MaxWriteKB](xref:Oracle.NoSQL.SDK.QueryOptions.MaxWriteKB) options. You
can also limit the amount of data deleted by *DeleteRange* operation by
setting [MaxWriteKB](xref:Oracle.NoSQL.SDK.DeleteRangeOptions.MaxWriteKB)
option.

## Administrative Operations (On-Premise only)

If you are using Node.js SDK with On-Premise Oracle NoSQL Database,  you may
perform administrative operations on the store using
[ExecuteAdminAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*)
and
[ExecuteAdminWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*)
methods.  These are operations that don't affect a specific table.  Examples
of such statements include:

* CREATE NAMESPACE mynamespace
* CREATE USER some_user IDENTIFIED BY password
* CREATE ROLE some_role
* GRANT ROLE some_role TO USER some_user

Note that management of users and roles as in the last 3 examples above is
available only when using
[secure kvstore](https://docs.oracle.com/en/database/other-databases/nosql-database/22.1/security/index.html).

These methods optional options object as
[AdminOptions](xref:Oracle.NoSQL.SDK.AdminOptions) and return
*Task<[AdminResult](xref:Oracle.NoSQL.SDK.AdminResult)>*.
[AdminResult](xref:Oracle.NoSQL.SDK.AdminResult) instance contains the
status of the operation (as to whether it is completed or still in progress)
as [State](xref:Oracle.NoSQL.SDK.AdminResult.State) as well as the
operaion's [Output](xref:Oracle.NoSQL.SDK.AdminResult.Output) if any.

Similar to
[ExecuteTableDDLAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*),
some of these operations can be long running and the returned result does not
imply the operation completion.  You may call
[AdminResult.WaitForCompletionAsync](xref:Oracle.NoSQL.SDK.AdminResult.WaitForCompletionAsync*)
to asynchronously wait for the operation completion.  Alternatively, use
[ExecuteAdminWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*)
method that will only procude result once the operation is completed by the
service.  You may also check the status of the currently running operation by
calling
[GetAdminStatusAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.GetAdminStatusAsync*)

For example, to create a namespace:

```csharp
var client = new NoSQLClient("config.json");
............................................

try
{
    var statement = "CREATE NAMESPACE TestNamespace1";
    var result = await client.ExecuteAdminAsync(statement);
    await result.WaitForCompletionAsync();
    Console.WriteLine("Namespace created.");
}
catch(Exception ex)
{
    // handle exceptions
}
```

Some other operations are immediate and are completed when
[ExecuteAdminAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*)
produces the result.  These are readonly operations that don't modify system
state but only return back information, such as *SHOW* commands (see
[Shell Utility Commands](https://docs.oracle.com/en/database/other-databases/nosql-database/22.1/sqlfornosql/shell-utility-commands.html#GUID-70FA12B5-6AD3-4965-9163-FA9549078EC7)).

Because some statements, such as *CREATE USER*, may include passwords, both
[ExecuteAdminAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*)
and
[ExecuteAdminWithCompletionAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*)
have overloads that take *statement* parameter as *char[]* instead of *string*
so that you can erase it afterwards and avoid keeping sensitive information
in memory.

For example:

```csharp
var client = new NoSQLClient("config.json");
............................................

var userName = "John";
var beginStatement = $"CREATE USER {userName} IDENTIFIED BY ".ToCharArray();
char[] password = null;
char[] statement = null;

try
{
    // Here we assume that RetrievePasswordAsCharArray() may throw Exception.
    password = RetrievePasswordAsCharArray();
    statement = new char[beginStatement.Length + password.Length];
    beginStatement.CopyTo(statement, 0);
    password.CopyTo(statement, beginStatement.Length);

    var result = await client.ExecuteAdminWithCompletion(statement);
    // Expected output: Status is Complete.
    Console.WriteLine("Status is {0}.", result.State);
    Console.WriteLine("User created.");
}
catch(Exception ex)
{
    // handle exceptions
}
finally {
    if (password != null)
    {
        Array.Clear(password);
        Debug.Assert(statement != null);
        Array.Clear(statement);
    }
}
```

In addition, there are methods such as
[ListNamespacesAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ListNamespacesAsync*),
[ListUsersAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ListUsersAsync*) and
[ListRolesAsync](xref:Oracle.NoSQL.SDK.NoSQLClient.ListRolesAsync*) that
return namespaces, users and roles, respectively, present in the store.  These
methods retrive this information by executing *SHOW* commands (such as
*SHOW AS JSON NAMESPACES*) and parsing the JSON output of the command which is
returned as [Output](xref:Oracle.NoSQL.SDK.AdminResult.Output) property of
[AdminResult](xref:Oracle.NoSQL.SDK.AdminResult).
