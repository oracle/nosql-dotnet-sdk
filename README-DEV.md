# Developing the .NET SDK for Oracle NoSQL Database

This guide covers building the SDK and running its tests. For SDK usage and
sample configuration, see the [README](./README.md).

## Prerequisites

- Windows, Linux, or macOS with the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet) or a later supported SDK.
- The SDK targets .NET 8.0, 9.0, and 10.0. Install the corresponding runtimes
  to run tests on each runtime. The test project permits major-version roll
  forward, so tests may otherwise run on a later installed runtime.
- C# 8.0 or later. Running a local KVLite store also requires Java and an Oracle
  NoSQL Database distribution.

Check the installed SDK and runtimes:

```bash
dotnet --version
dotnet --list-runtimes
```

If a system HTTP proxy is configured, local services may require a proxy
bypass. For example, on Linux or macOS:

```bash
export NO_PROXY=localhost,127.0.0.1
```

## Get the source and build

Clone the repository and build the solution:

```bash
git clone https://github.com/oracle/nosql-dotnet-sdk.git
cd nosql-dotnet-sdk
dotnet build Oracle.NoSQL.SDK.sln -c Release
```

The following commands assume the current directory is the repository root.
Use `dotnet clean Oracle.NoSQL.SDK.sln -c Release` before building when a clean
build is needed. `Debug` is also available as a build configuration. The
solution can be opened in Visual Studio.

## Run tests

The test project is `Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests` and uses
MSTest. Tests create and modify tables; use an isolated Cloud Simulator or
on-premises test store. Running against Cloud Service can consume resources and
incur charges.

### <a name="unit_tests"></a>Cloud Simulator

Download and extract [Oracle NoSQL Cloud Simulator](https://www.oracle.com/downloads/cloud/nosql-cloud-sdk-downloads.html).
In a separate terminal, enter the extracted distribution directory when
prompted and start the simulator on `localhost:8080`:

```bash
read -r -p 'Cloud Simulator installation directory: ' CLOUDSIM_HOME
(cd "$CLOUDSIM_HOME" && sh ./runCloudSim -root "${TMPDIR:-/tmp}/nosql-cloudsim-root" -httpPort 8080 -storePort 5010)
```

The simulator runs in the foreground; stop it with Ctrl-C when finished. The
default unit test configuration uses that endpoint and does not require OCI
credentials. From the SDK repository root in another terminal, run all target
frameworks sequentially:

```bash
dotnet test Oracle.NoSQL.SDK.sln -c Release \
  -p:TestTfmsInParallel=false --logger 'trx;LogFilePrefix=cloudsim' \
  --results-directory ./artifacts/test-results
```

For a simulator on another endpoint, provide a NoSQL SDK configuration JSON
file through the `noSQLConfigFile` test parameter. See
[Using the Cloud Simulator](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-cloud.html#cloudsim).

### Local on-premises store

Start KVLite and its HTTP proxy as shown in the [README](./README.md). The
sample `Oracle.NoSQL.SDK.Samples/kvlite.json` connects to an on-premises proxy
on `localhost:8080`. The following commands prompt for the extracted KV
installation directory and report the running server's version. They assume
KVLite is listening on `localhost:5100`:

```bash
read -r -p 'KV installation directory: ' KVHOME
java -jar "$KVHOME/lib/kvstore.jar" runadmin -host localhost -port 5100 show version
read -r -p 'Server version (for example 26.4.2): ' TEST_KV_VERSION
```

Use the reported server version for the test parameter. From the repository
root, run the suite sequentially and save the results:

```bash
dotnet test Oracle.NoSQL.SDK.sln -c Release \
  -p:TestTfmsInParallel=false --logger 'trx;LogFilePrefix=onprem' \
  --results-directory ./artifacts/test-results -- \
  "TestRunParameters.Parameter(name=\"noSQLConfigFile\",value=\"$PWD/Oracle.NoSQL.SDK.Samples/kvlite.json\")" \
  "TestRunParameters.Parameter(name=\"kvVersion\",value=\"$TEST_KV_VERSION\")"
```

For a different endpoint or secure store, use an appropriate
[SDK configuration](https://oracle.github.io/nosql-dotnet-sdk/tutorials/connect-on-prem.html#config)
instead of `kvlite.json`.

### Server versions and V6 queries

Set `kvVersion` to the actual KV version when testing an older server. Tests
that check this parameter can skip features unavailable in that version; other
features may use separate capability checks. If omitted, tests assume the
current version. For Cloud Simulator, the KV version is in the manifest inside
its `kvstore.jar`.

UNION integration tests cover branches within the same store, including
different namespaces. They require on-premises KV 26.1 or later and an
HTTP proxy supporting query protocol V6. The shared test guard checks kvVersion,
then prepares an ordinary query to negotiate the proxy's query protocol before
preparing UNION. An older KV version or a proxy negotiating V3–V5 causes these
tests to be skipped. Supported environments must also have the required store
compatibility version (SCV) enabled; preparation errors are not converted into
skips. If kvVersion is omitted, the KV version is assumed to be current.

### Test options

To run a subset, use a filter. For example:

```bash
dotnet test Oracle.NoSQL.SDK.sln -c Release --filter 'ClassName~PutTests'
```

Rate limiter tests can take a long time. To skip them for a Cloud Simulator
run, set `skipRateLimiterTests`:

```bash
dotnet test Oracle.NoSQL.SDK.sln -c Release -p:TestTfmsInParallel=false -- \
  'TestRunParameters.Parameter(name="skipRateLimiterTests",value="true")'
```

Parameters can also be supplied using a `.runsettings` file, including from
Visual Studio Test Explorer. See
[Configure unit tests with a runsettings file](https://learn.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2022)
and [run selective tests](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=mstest).

### <a name="smoke_test"></a>Smoke test

The smoke test creates a table, writes, reads, and deletes rows, then drops the
table. Start the corresponding service first. For Cloud Simulator:

```bash
dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest/Oracle.NoSQL.SDK.SmokeTest.csproj \
  --framework net10.0 -c Release -- Oracle.NoSQL.SDK.Samples/cloudsim.json
```

For the local on-premises proxy:

```bash
dotnet run --project Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest/Oracle.NoSQL.SDK.SmokeTest.csproj \
  --framework net10.0 -c Release -- Oracle.NoSQL.SDK.Samples/kvlite.json
```

The smoke test can also use a different SDK configuration JSON file. If the
argument is omitted, it uses the default OCI configuration for Cloud Service.

## Run examples

See the [README](./README.md) for service setup and example configuration.
The sample projects reference the released NuGet package. Before the package
version referenced by a sample is published, create a local package and restore
the sample from it and NuGet:

```bash
dotnet pack Oracle.NoSQL.SDK/src/Oracle.NoSQL.SDK.csproj -c Release -o ./artifacts/packages
dotnet restore Oracle.NoSQL.SDK.Samples/BasicExample/BasicExample.csproj \
  --source "$PWD/artifacts/packages" --source https://api.nuget.org/v3/index.json
```

For example, with a local on-premises proxy running on `localhost:8080`, run
the restored sample:

```bash
dotnet run --project Oracle.NoSQL.SDK.Samples/BasicExample/BasicExample.csproj \
  --framework net10.0 -c Release --no-restore -- Oracle.NoSQL.SDK.Samples/kvlite.json
```

If the referenced package is already published, run the sample without
`--no-restore` to restore it from the configured NuGet sources.
BasicExample uses last-write metadata and requires a service that supports it.
Cloud Simulator 5.11.6, which bundles KV 22.3.27, does not support that feature;
the sample reports `NotSupportedException` on that simulator.
