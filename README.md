# .NET SDK for Oracle NoSQL Database

## Overview

This is the .NET SDK for
the [Oracle NoSQL Database](https://www.oracle.com/database/technologies/related/nosql.html).
The SDK provides interfaces, documentation, and examples to develop .NET
applications that use the
[Oracle NoSQL Database Cloud Service](https://cloud.oracle.com/nosqldatabase)
and the [On-Premise Oracle NoSQL Database](https://docs.oracle.com/en/database/other-databases/nosql-database/index.html).

## Prerequisites

* [.NET Core](https://dotnet.microsoft.com/download) 3.1 or later, including
.NET 5.0 and later running on Windows, Linux, or Mac.
* Optionally,
[Nuget Package Manager](https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-nuget-cli)
if you wish to install the SDK independently of your project.
* For use with the Oracle NoSQL Database Cloud Service:
  * An Oracle Cloud Infrastructure account
  * A user created in that account, in a group with a policy that grants the
  desired permissions.
  See
  [Oracle NoSQL Database Cloud Service](https://docs.oracle.com/en/cloud/paas/nosql-cloud/index.html)
  for more information.
* For use with the Oracle NoSQL Database On Premise:
  * [Oracle NoSQL Database](https://www.oracle.com/database/technologies/related/nosql.html).
See
[Oracle NoSQL Database Downloads](https://www.oracle.com/database/technologies/nosql-database-server-downloads.html)
to download Oracle NoSQL Database.  See
[Oracle NoSQL Database Documentation](https://docs.oracle.com/en/database/other-databases/nosql-database/index.html)
to get started with Oracle NoSQL Database.
In particular, see the
[Administrator Guide](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/index.html)
on how to install, configure and run Oracle NoSQL Database Service.
* Optionally, [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/)
or later, or [Visual Studio Code](https://code.visualstudio.com/).

## Installation

You may install this SDK either as a dependency of your project or in a
separate directory.  To install as a dependency of your project, cd to the
directory containing your .Net project file and run:

```bash
dotnet add package Oracle.NoSQL.SDK
```

Alternatively, you may install the SDK independently into a directory of your
choice by using
[nuget.exe CLI](https://docs.microsoft.com/en-us/nuget/install-nuget-client-tools#cli-tools):

```bash
nuget.exe install Oracle.NoSQL.SDK -OutputDirectory <your-packages-directory>
```

## Documentation

See the
[API and user guide documentation](https://oracle.github.io/nosql-dotnet-sdk/).

## Changes
See [Changelog](https://github.com/oracle/nosql-dotnet-sdk/blob/main/CHANGELOG.md)
for changes in each release.

## Getting Started

The SDK API classes are in *Oracle.NoSQL.SDK* namespace.

```csharp
using Oracle.NoSQL.SDK;
```

### Connecting to Oracle NoSQL Database

To perform database operations, you need to create an instance of
[NoSQLClient](https://oracle.github.io/nosql-dotnet-sdk/api/Oracle.NoSQL.SDK.NoSQLClient.html).

#### Connecting to Oracle NoSQL Database Cloud Service

Running against the Cloud Service requires an Oracle Cloud account. See
[Configuring for the Cloud Service](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#configure_cloud)
for information on getting an account and acquiring required credentials.

1. Collect the following information:

* Tenancy ID
* User ID
* API signing key (private key file in PEM format)
* Fingerprint for the public key uploaded to the user's account
* Private key passphrase, needed only if the private key is encrypted

2. Decide on the region you want to use.

3. Create *NoSQLClient* as follows, by substituting the values as indicated
below:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.<your-region-here>,
        AuthorizationProvider = new IAMAuthorizationProvider(
            new IAMCredentials
            {
                TenantId = "your tenancy OCID",
                UserId = "your user OCID",
                Fingerprint = "your public key fingerprint",
                PrivateKeyFile = "path to private key file",
                Passphrase = "passphrase if set for your private key"
            })
    });
```

Alternatively, you may put the credentials into an OCI configuration file.
See
[Using a Configuration File](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#config_file).
Put your credentials in a file *~/.oci/config* (on Windows ~ stands for
%USERPROFILE%) under profile named *DEFAULT*.  Include your region together
with the credentials.  Then create *NoSQLClient* as follows:

```csharp
var client = new NoSQLClient();
```

#### Connecting to Oracle NoSQL Cloud Simulator

Running against the Oracle NoSQL Cloud Simulator requires a running Cloud
Simulator instance. See
[Using the Cloud Simulator](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#cloudsim)
for information on how to download and start the Cloud Simulator.

By default, Cloud Simulator runs on *localhost* and uses HTTP port *8080*.

Create *NoSQLClient* as follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        ServiceType = ServiceType.CloudSim,
        Endpoint = "localhost:8080"
    });
```

#### Connecting to On-Premise Oracle NoSQL Database

Running against the Oracle NoSQL Database on-premise requires a running
Oracle NoSQL Database instance as well as a running NoSQL Proxy server
instance. Your application will connect to the proxy server.

See
[Connecting to an On-Premise Oracle NoSQL Database](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html)
for information on how to download and start the database instance and proxy
server.

To get started, start the proxy in non-secure mode.  By default, it runs on
*localhost* and uses HTTP port *80*.

Create *NoSQLClient* as follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        ServiceType = ServiceType.KVStore,
        Endpoint = "localhost:80"
    });
```

If the proxy was started on a different host or port, change *Endpoint*
accordingly.

A more complex setup is required to use the proxy in secure mode.  See
[Configuring the SDK for a Secure Store](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html#secure).

### Using Oracle NoSQL Database

The following example creates a table, inserts a row, reads a row, deletes
a row and drops the table:

```csharp

var tableName = "orders";

// Create a table
var tableResult = await client.ExecuteTableDDLWithCompletionAsync(
    $"CREATE TABLE IF NOT EXISTS {tableName}(id LONG, description STRING, " +
    "details JSON, PRIMARY KEY(id))", new TableLimits(1, 5, 1));
Console.WriteLine("Table {0} created, table state is {1}",
    tableName, tableResult.TableState);

// Put a row
var putResult = await client.PutAsync(TableName, new MapValue
{
    ["id"] = 1000,
    ["description"] = "building materials",
    ["details"] = new MapValue
    {
        ["total"] = 1000.00,
        ["quantity"] = new MapValue
        {
            ["nails"] = 500,
            ["pliers"] = 100,
            ["tape"] = 50
        }
    }
});

if (putResult.ConsumedCapacity != null)
{
    Console.WriteLine("Put used: {0}", putResult.ConsumedCapacity);
}

var primaryKey = new MapValue
{
    ["id"] = 1000
};

// Get a row
var getResult = await client.GetAsync(TableName, primaryKey);
if (getResult.Row != null)
{
    Console.WriteLine("Got row: {0}\n", getResult.Row.ToJsonString());
}
else
{
    Console.WriteLine("Row with primaryKey {0} doesn't exist",
    primaryKey.ToJsonString());
}

// Delete a row
var deleteResult = await client.DeleteAsync(tableName, primaryKey);
Console.WriteLine("Delete {0}.",
    deleteResult.Success ? "succeeded", "failed");

// Drop a table
tableResult = await client.ExecuteTableDDLWithCompletionAsync(
    $"DROP TABLE {tableName}");
Console.WriteLine("Table {0} dropped, table state is {1}",
    tableName, tableResult.TableState);
```

## <a name="examples"></a>Examples

Examples are located under *Oracle.NoSQL.SDK.Samples* directory.  In Visual
Studio, you can open the examples solution *Oracle.NoSQL.SDK.Samples.sln*.
Each example is in its own sub-directory and has its project file and the main
program *Program.cs*.

You can run the examples

* Against the Oracle NoSQL Database Cloud Service using your Oracle Cloud
account and credentials.
* Locally using the
[Oracle NoSQL Database Cloud Simulator](https://www.oracle.com/downloads/cloud/nosql-cloud-sdk-downloads.html).
* Against On-Premise Oracle NoSQL Database via the proxy.

Each example takes one command line argument which is the path to the
JSON configuration file used to create *NoSQLClient* instance.

Note: you may omit this command line argument if running against the cloud
service and using default OCI configuration file.  See
[Example Quick Start](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#quickstart).

Use configuration file templates provided in *Oracle.NoSQL.SDK.Samples*
directory.  Make a copy of the template, fill in the appropriate values and
remove unused properties.  The following templates are provided:

* **cloud\_template.json** is used to access a cloud service instance and
allows you to customize configuration. Copy that file and fill in appropriate
values as described in
[Supply Credentials to the Application](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#supply).
* **cloudsim.json** is used if you are running against the cloud simulator.
You may use this file directly as config file if you are running the cloud
simulator on localhost on port 8080. If the cloud simulator has been started on
a different host or port, change the endpoint. See
[Using the Cloud Simulator](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#cloudsim).
* **kvstore\_template.json** is used to access on-premise NoSQL Database via
the proxy.  Copy that file and fill in appropriate values as described in
[Configuring the SDK](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html#config).
Also see
[Example Quick Start](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html#quickstart).

To run an example, go to the example's directory and issue *dotnet run*
command, providing JSON config file as the command line argument:

```bash
cd Oracle.NoSQL.SDK.Samples/<example>
dotnet run -f <target-framework> [-- /path/to/config.json]
```

where *<example>* is the example directory and *<target-framework>* is the
target framework moniker, supported values are *net5.0* and *netcoreapp3.1*.
The command above will build and run the example.

For example:

```bash
cd Oracle.NoSQL.SDK.Samples/BasicExample
dotnet run -f net5.0 -- my_config.json
```

## Building Documentation

The documentation is build with [DocFX](https://dotnet.github.io/docfx/).  It
encompasses the API documentation and the tutorials.  The doc build also
requires
[memberpage plugin](https://dotnet.github.io/docfx/templates-and-plugins/plugins-dashboard.html)
in order to have separate pages describing each method and property.

*docfx.json* is located under *Documentation* directory.  The tutorials
are located under *Documentation/tutorials*.

Before building documentation, make sure you have built the driver in Debug
mode for .NET Core 3.1.  DocFX build uses this dll:

*Oracle.NoSQL.SDK/src/bin/Debug/netcoreapp3.1/Oracle.NoSQL.SDK.dll*

Afterwards, follow these steps:

1. Install [docfx.console](https://www.nuget.org/packages/docfx.console/)
package. You may use
[nuget.exe CLI](https://docs.microsoft.com/en-us/nuget/install-nuget-client-tools#cli-tools)
for this.  Note that to run on Linux and Mac,
[Mono](https://www.mono-project.com/) is required.  For more information, see
[Getting Started with DocFX](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html).
Alternatively to *docfx.console*, *docfx* can be installed as indicated in the
link above.

2. Install [memberpage](https://www.nuget.org/packages/memberpage/) package.

3. Run DocFX as follows:

```bash
cd nosql-dotnet-sdk/Documentation
/path/to/docfx.console/<docfx-version>/tools/docfx.exe -t statictoc,template,/path/to/memberpage/<memberpage-version>/content
```

In the above:
* */path/to/docfx.console/<docfx-version>/tools/docfx.exe* is the location of
*docfx.exe*, may be different if using *docfx* instead of *docfx.console*
* */path/to/memberpage* is the installation location of the *memberpage*
plugin.  You must provide the path to its *content* directory.

An example for Windows:

```bash
cd nosql-dotnet-sdk\Documentation
%USERPROFILE%\.nuget\packages\docfx.console\2.58.0\tools\docfx.exe -t statictoc,template,%USERPROFILE%\.nuget\packages\memberpage\2.58.0\content
```

Other templates used are *statictoc* and *template* (the later is custom
template included in the build).
[statictoc](https://dotnet.github.io/docfx/templates-and-plugins/templates-dashboard.html)
is used to build a static website browsable from the file system.  It is
required to publish the documentation.

The generated documentation is located in *Documentation/_site* directory.
(There are also intermediate build files in *Documentation/obj* directory.)

For faster builds, you may use *default* template instead of *statictoc* but
the website would only be viewable when using *docfx serve* command:

```bash
/path/to/docfx.console/<docfx-version>/tools/docfx.exe -t default,template,/path/to/memberpage/<memberpage-version>/content
/path/to/docfx.console/<docfx-version>/tools/docfx.exe serve -p <port>
```

Use *docfx.exe help* for more details.

## Build Nuget Package

To build the nuget package, use *dotnet pack* command:

```bash
cd Oracle.NoSQL.SDK/src
dotnet clean -c Release
dotnet build -c Release
dotnet pack -c Release
```

This builds nuget package *Oracle.NoSQL.SDK.<version>.nupkg* in
*Oracle.NoSQL.SDK/src/bin/Release* directory.

## Help

* Open an issue in the [Issues](https://github.com/oracle/nosql-dotnet-sdk/issues) page.
* Email to <nosql_sdk_help_grp@oracle.com>.
* [Oracle NoSQL Developer Forum](https://community.oracle.com/community/groundbreakers/database/nosql_database)

When requesting help please be sure to include as much detail as possible,
including version of the SDK and **simple**, standalone example code as needed.

## License

Please see the
[LICENSE](https://github.com/oracle/nosql-dotnet-sdk/blob/main/LICENSE.txt)
file included in the top-level directory of the package for a copy of the
license and additional information.

The
[THIRD\_PARTY\_LICENSES](https://github.com/oracle/nosql-dotnet-sdk/blob/main/THIRD_PARTY_LICENSES.txt)
file contains third party notices and licenses.

The
[THIRD\_PARTY\_LICENSES\_DEV](https://github.com/oracle/nosql-dotnet-sdk/blob/main/THIRD_PARTY_LICENSES_DEV.txt)
file contains third party notices and licenses for running the test programs.

The *Documentation* directory contains the license files for the documentation.
If you build and distribute a documentation bundle, these should be included in
that bundle.

## Contributing

See
[CONTRIBUTING](https://github.com/oracle/nosql-dotnet-sdk/blob/main/CONTRIBUTING.md)
for details.

## Security

See
[SECURITY](https://github.com/oracle/nosql-dotnet-sdk/blob/main/SECURITY.md)
for details.
