# .NET SDK for Oracle NoSQL Database

## Overview

This document is for developers of the .NET SDK for the Oracle NoSQL
Database. The target audience are those who want to modify
source code and run and modify tests and examples.

## Getting Started

Read [README](./README.md) on how to use the SDK.

1.  You may develop on Windows, Linux or Mac.  The SDK supports .NET 5.0 and
later as well as .NET Core 3.1. It is recommended to use LTS version. Current
LTS version is .NET 8.0. Download your chosen .NET version
[here](https://dotnet.microsoft.com/en-us/download/dotnet) and follow
installation instructions for your operating system.

After installation, set *DOTNET_ROOT* environment variable to point to the .NET
installation location. 

Currently, the SDK is built for 2 platforms: .NET 5.0 and .NET Core 3.1.
However, both binaries are forward-compatible and will run on any later .NET
runtime. .NET 5.0 binary will be used by applications running on .NET 5.0 or
later, however testing .NET Core 3.1 binary may be necessary to ensure
backward compatibility. See below on how to specify target framework.

Note that .NET SDK for Oracle NoSQL Database requires C# version 8.0 or
higher.

2.  Clone the repository:

```bash
git clone git@github.com:/oracle/nosql-dotnet-sdk
```

3.  Build the solution using *dotnet build* command:

```bash
cd nosql-dotnet-sdk
[dotnet clean]
dotnet build
```

(Note: the above command will build for both .NET 5.0 and .NET 3.1 and will
work only if you installed .NET 5.0 or later).

You may also build for particular .NET version (.NET 5.0 or .NET Core 3.1)
and particular build configuration (*Debug* or *Release*):

```bash
cd nosql-dotnet-sdk
dotnet build -f <target-framework> [-c <build-configuration>]
```

where *target-framework* is target framework moniker (TFM), values are
*net5.0* and *netcoreapp3.1*, and *build-configuration* is *Debug* or
*Release*, default is *Debug*.

The above will build the solution *Oracle.NoSQL.SDK.sln*.  You may also
build particular projects individually by building in the following
directories:

* Driver project in *Oracle.NoSQL.SDK/src*
* Unit test project in *Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests*
* Smoke test in *Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest*

You may also open and build the solution *Oracle.NoSQL.SDK.sln* in
[Visual Studio](https://visualstudio.microsoft.com/).  Visual Studio 2019 or
later is required.

4. The driver may be used to access
[Oracle NoSQL Database Cloud Service](https://docs.oracle.com/en/cloud/paas/nosql-cloud/index.html)
and [On-Premise Oracle NoSQL Database](https://docs.oracle.com/en/database/other-databases/nosql-database/index.html).

If developing for the Cloud Service, during development you may use the
driver locally by running tests and examples against
[Oracle NoSQL Database Cloud Simulator](https://docs.oracle.com/en/cloud/paas/nosql-cloud/donsq/index.html).

### Note on Http Proxy Settings

The SDK will use default HTTP Proxy settings on your system, if present. E.g.
on Linux these are determined by environment variable such as *HTTP_PROXY*,
*HTTPS_PROXY* and *NO_PROXY*. If you are testing against the Cloud Simulator
or On-Premise Oracle NoSQL Database running on *localhost*, you may need to
bypass HTTP proxy for local addresses. E.g. on Linux make sure that *NO_PROXY*
environment variable is set. E.g.:

```bash
export NO_PROXY=localhost,127.0.0.1
```

Otherwise, follow OS-specific instructions for HTTP proxy settings.

## Running Examples

See *Examples* section of [README](./README.md) for information on how to
configure and run examples.

## Running Tests

Tests require configuration information to create
[NoSQLClient](https://oracle.github.io/nosql-dotnet-sdk/api/Oracle.NoSQL.SDK.NoSQLClient.html)
instance.  This configuration is provided as a parameter to tests and is
passed as a path to a JSON configuration file containing the configuration.

For more information on JSON configuration files used to create
[NoSQLClient](https://oracle.github.io/nosql-dotnet-sdk/api/Oracle.NoSQL.SDK.NoSQLClient.html),
see
[Configuring the SDK](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#configure_cloud)
for the cloud service,
[Using the Cloud Simulator](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#cloudsim)
for cloud simulator and
[Configuring the SDK](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html#config)
for on-premise Oracle NoSQL Database.

The configuration defines the service to which the driver will connect to as
well as other optional parameters.  See
[NoSQLConfig](https://oracle.github.io/nosql-dotnet-sdk/api/Oracle.NoSQL.SDK.NoSQLConfig.html)
for more information.

### <a name="unit_tests"></a>Running Unit Tests

**Note:**

**It is recommended to run the unit tests only against the Cloud Simulator or
On-Premise Oracle NoSQL Database and not against the Cloud Service as the
latter will consume cloud resources and may incur significant cost.**

Unit tests are located in
*Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests* directory.

The unit tests use
[MSTest](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
framework (MSTest V2).

If JSON configuration file is not provided when running unit tests, it is
assumed that the they are running against Cloud Simulator using default
endpoint *localhost:8080*.

Use *dotnet test* command to (build and) run unit tests against Cloud
Simulator running on *localhost* port *8080*:

```bash
cd nosql-dotnet-sdk
dotnet test -f <target-framework> [-c <build-configuration>]
```

where *target-framework* is target framework moniker (TFM), values are
*net5.0* and *netcoreapp3.1*, and *build-configuration* is *Debug* or
*Release*, default is *Debug*.

In all other cases, JSON configuration file is required.  It is passed to the
tests as property named *noSQLConfigFile*, as part of
[TestContext.Properties](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.testcontext.properties?view=visualstudiosdk-2019).

You may provide this property in one of two ways:

1.  On the command line when running *dotnet test*:

```bash
dotnet test -f <target-framework> [-c <build-configuration>] -- TestRunParameters.Parameter(name=\"noSQLConfigFile\", value=\"/path/to/config.json\")
```

2. In a *.runsettings* file:

Create file *test.runsettings* (name can be differrent but the extension must
be *.runsettings*) with the following contents:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!-- Parameters used by tests at run time -->
  <TestRunParameters>
    <Parameter name="noSQLConfigFile" value="/path/to/config.json" />
  </TestRunParameters>
</RunSettings>
```

Pass this file to *dotnet test* command:

```bash
dotnet test -f <target-framework> [-c <build-configuration>] -s test.runsettings
```

In addition to *dotnet test* command, using *.runsettings* file will allow you
to pass these settings to unit tests when using
[Test Explorer](https://docs.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer?view=vs-2022)
in Visual Studio.

For more information, see
[Configure unit tests by using a .runsettings file](https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2022).

To run individual unit tests or test cases, use *dotnet test* command with
*--filter* option.  For example:

```bash
dotnet test -f net5.0 --filter ClassName~PutTests
```

For more details, see
[Run selective unit tests](https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=mstest).

#### Running against past server versions

By default, the unit tests assume running against the latest server version.
If you need to run against older server versions for compatibility testing,
you need to set parameter *kvVersion* to the KV version you are running
against. This will allow you to skip testing features not supported in
previous KV versions. Without this option, all features will be tested and
thus some tests will fail when running against past KV version. To run against
past CloudSim version, find the corresponding KV version for given CloudSim
installation (look at the manifest inside *kvstore.jar*).

You can specify *kvVersion* parameter in either *.runsettings* file or on the
command line as described above. E.g. in *.runsettings* file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!-- Parameters used by tests at run time -->
  <TestRunParameters>
	<Parameter name="kvVersion" value="22.3.27" />
    <Parameter name="noSQLConfigFile" value="/path/to/config.json" />
  </TestRunParameters>
</RunSettings>
```

#### Optionally skipping rate limiter tests

Rate limiter tests, which are part of unit tests, may take quite a long time
to run. You can optinally skip them by specifying the parameter
*skipRateLimiterTests*. E.g. in *.runsettings* file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!-- Parameters used by tests at run time -->
  <TestRunParameters>
	<Parameter name="skipRateLimiterTests" value="true" />
    <Parameter name="noSQLConfigFile" value="/path/to/config.json" />
  </TestRunParameters>
</RunSettings>
```

### <a name="smoke_test"></a>Running Smoke Test

Smoke test does basic sanity testing of the SDK.  It creates a table, inserts,
gets and then deletes several rows and then drops the table.

Smoke test is localed under
*Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest* directory.  It can
be run against the cloud service, cloud simulator or on-premise NoSQL
database. It requires JSON configuration file as described above.  To
(build and) run smoke test, use *dotnet run* command:

```bash
cd nosql-dotnet-sdk/Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest
dotnet run -f <target-framework> [-c <build-configuration>] -- /path/to/config.json
```

where *target-framework* is target framework moniker (TFM), values are
*net5.0* and *netcoreapp3.1*, and *build-configuration* is *Debug* or
*Release*, default is *Debug*.

Note: you may omit JSON configuration file if running against the cloud
service using default OCI configuration file (see
[Using an OCI Configuration File](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#config_file)).
This is equivalent to creating
[NoSQLClient](https://oracle.github.io/nosql-dotnet-sdk/api/Oracle.NoSQL.SDK.NoSQLClient.html)
instance using default constructor.
