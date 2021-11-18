This guide describes how to install, configure, and use the Oracle NoSQL
Database .NET SDK with the Oracle NoSQL Database Cloud Service.

## Prerequisites

The SDK requires:

* An Oracle Cloud Infrastructure account
* A user created in that account, in a group with a policy that grants the
desired permissions.
* [.NET Core](https://dotnet.microsoft.com/download) 3.1 or later, including
[.NET 5.0](https://dotnet.microsoft.com/download/dotnet/5.0) or later version
running on Windows, Linux, or Mac.
* [Nuget Package Manager](https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-nuget-cli)
if you wish to install the SDK independently of your project.

## Downloading and Installing the SDK

You can install the SDK from [NuGet Package Manager](https://www.nuget.org/)
either by adding it as a reference to your project or independently.

### Add the SDK as a Project Reference

You may add the SDK [NuGet Package](https://www.nuget.org/packages/Oracle.NoSQL.SDK/)
as a reference to your project by using .Net CLI:

```bash
cd <your-project-directory>
dotnet add package Oracle.NoSQL.SDK
```

Alternatively, you may perform the same using
[NuGet Package Manager](https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio)
in [Visual Studio](https://visualstudio.microsoft.com/).

### Independent Install

You may install the SDK independently into a directory of your choice by using
[nuget.exe CLI](https://docs.microsoft.com/en-us/nuget/install-nuget-client-tools#cli-tools):

```bash
nuget.exe install Oracle.NoSQL.SDK -OutputDirectory <your-packages-directory>
```

## <a name="configure_cloud"></a>Configuring the SDK

The SDK requires an Oracle Cloud account and a subscription to the Oracle
NoSQL Database Cloud Service. If you do not already have an Oracle Cloud
account you can start [here](https://docs.cloud.oracle.com/en-us/iaas/Content/GSG/Concepts/baremetalintro.htm).

The SDK is using Oracle Cloud Infrastructure Identity and Access Management
(IAM) to authorize database operations.  For more information on IAM see
[Overview of Oracle Cloud Infrastructure Identity and Access Management](https://docs.cloud.oracle.com/iaas/Content/Identity/Concepts/overview.htm)

To use the SDK, you need to configure it for use with IAM.  The best way to
get started with the service is to use your Oracle Cloud user's identity to
obtain required credentials and provide them to the application.  This is
applicable in most use cases and described below in section
[Authorize with Oracle Cloud User's Identity](#user).

A different configuration that does not require user's credentials may be used
in a couple of special use cases:

* To access Oracle NoSQL Database Cloud Service from a compute instance in the
Oracle Cloud Infrastructure (OCI), use Instance Principal.  See
[Authorizing with Instance Principal](#instance_principal).

* To access Oracle NoSQL Database Cloud Service from other Oracle Cloud
service resource such as
[Functions](https://docs.cloud.oracle.com/en-us/iaas/Content/Functions/Concepts/functionsoverview.htm),
use Resource Principal.
See [Authorizing with Resource Principal](#resource_principal).

### <a name="user"></a>Authorize with Oracle Cloud User's Identity

See sections below on how to [acquire credentials](#creds) and
[configure the application](#supply) to connect to Oracle NoSQL Database Cloud
Service.  You may also need to perform [additional configuration](#connecting)
such as choosing your region, specifying compartment and other configuration
options.

See [Example Quick Start](#quickstart) for the quickest way to get an
example running.

#### <a name="creds"></a>Acquire Credentials for the Oracle NoSQL Database Cloud Service

See
[Acquiring Credentials](https://www.oracle.com/pls/topic/lookup?ctx=en/cloud/paas/nosql-cloud/csnsd&id=acquire-creds)
for details of credentials you will need to configure an application.

These steps only need to be performed one time for a user. If they have already
been done they can be skipped. You need to obtain the following credentials:

* Tenancy ID
* User ID
* API signing key (private key file in PEM format)
* Fingerprint for the public key uploaded to the user's account
* Private key pass phrase, needed only if the private key is encrypted

The private key may be either in PKCS#8 format (starts with
<em>-----BEGIN PRIVATE KEY-----</em> or
<em>-----BEGIN ENCRYPTED PRIVATE KEY-----</em>) or PKCS#1 format (starts with
<em>-----BEGIN RSA PRIVATE KEY-----</em>).  PKCS#8
format is preferred.  There is a limitation for encrypted private keys in
PKCS#1 format in that it must use AES encryption (with key sizes of 128, 192
or 256 bits).  Otherwise, if you have an encrypted private key in PKCS#1
format, you can convert it to PKCS#8 using openssl:

```bash
openssl pkcs8 -topk8 -inform PEM -outform PEM -in encrypted_pkcs1_key.pem -out encrypted_pkcs8_key.pem
```

#### <a name="supply"></a>Supply Credentials to the Application

Credentials are used to authorize your application to use the service.
There are 3 ways to supply credentials:

1. Store credentials in an [OCI configuration file](#config_file).
2. [Supply credentials directly](#config_api) as
[IAMCredentials](xref:Oracle.NoSQL.SDK.IAMCredentials).
3. [Create your own credentials provider](#config_obj) to load credentials on
demand from the location of your choice (e.g. keystore, keychain, encrypted
file, etc.).

You supply the credentials to the SDK when you create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance that is used to
perform the database operations.  The IAM configuration is represented by
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider)
instance and it indicates how the credentials are supplied.
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider)
is in turn a part of [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig)
configuration that is passed to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) (see
[Connecting an Application](#connecting)).

[Creating your own credentials provider](#config_obj) is the most secure
option, because you are in control of how credentials are stored and loaded
by the driver.  Otherwise, the recommended option is to use an
[Oracle Cloud Infrastructure configuration file](#config_file).  Supplying
credentials directly is the least secure option because sensitive information
such as private key will be kept in memory for the lifetime of
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.

##### <a name="config_file"></a>Using an OCI Configuration File

You can store the credentials in an
[Oracle Cloud Infrastructure Configuration File](https://docs.oracle.com/en-us/iaas/Content/API/Concepts/sdkconfig.htm).

The default path for the configuration file is *~/.oci/config*, where *~*
stands for user's home directory.  On Windows, *~* is a value of *USERPROFILE*
environment variable.

The file may contain multiple profiles.  By default, the SDK uses profile
named **DEFAULT** to store the credentials.

To use these default values, create file named *config* in *~/.oci* directory
with the following contents:

```ini
[DEFAULT]
tenancy=<your-tenancy-ocid>
user=<your-user-ocid>
fingerprint=<fingerprint-of-your-public-key>
key_file=<path-to-your-private-key-file>
pass_phrase=<your-private-key-passphrase>
region=<your-region-identifier>
```

Note that you may also specify your region identifier together with
credentials in the OCI configuration file.  By default, the driver will look
for credentials and a region in the OCI configuration file at the default path
and in the default profile.  Thus, if you provide region together with
credentials as shown above, you can create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance without passing
any configuration:

```csharp
var client = new NoSQLClient();
```

Alternatively, you may specify the region (as well as other properties) in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig):

```csharp
var client = new NoSQLClient(
    new NoSQLConfig(
    {
        Region = Region.US_ASHBURN_1,
        Timeout = TimeSpan.FromSeconds(10),
        ..........
    });
```

As in the previous example, default OCI configuration file with default
profile will be used unless specified otherwise.  The region in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) will take
precendence over the region in OCI configuration file if both are set.

You may choose to use different path for OCI configuration file as well as
different profile within the configuration file.  In this case, specify these
when creating
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider).
For example, if your OCI configuration file path is *~/myapp/.oci/config* and
you store your credentials under profile **Jane**:

```ini
...............
...............
[Jane]
tenancy=.......
user=..........
...............
[John]
tenancy=.......
user=..........
...............
```

Then create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance as
follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        AuthorizationProvider = new IAMAuthorizationProvider(
            "~/myapp/.oci/config", "Jane")
    });
```

(Note that you don't have to specify the service type if you set
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider),
see section [Specifying Service Type](#service_type))

##### <a name="config_api"></a>Specifying Credentials Directly

You may specify credentials directly as
[IAMCredentials](xref:Oracle.NoSQL.SDK.IAMCredentials) when creating
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider).
Create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) as follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = <your-service-region>,
        AuthorizationProvider = new IAMAuthorizationProvider(
            new IAMCredentials
            {
                TenantId = myTenancyOCID,
                UserId = myUserOCID,
                Fingerprint = myPublicKeyFingerprint,
                PrivateKeyFile = myPrivateKeyFile
            })
    });
```

##### <a name="config_obj"></a>Creating Your Own Credentials Provider

You may specify your custom credentials provider when creating
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider).
The credentials provider is a delegate function that returns
Task<[IAMCredentials](xref:Oracle.NoSQL.SDK.IAMCredentials)> and thus may
be implemented asynchronously:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = <your-service-region>,
        AuthorizationProvider = new IAMAuthorizationProvider(
           async (CancellationToken) => {
                // Retrieve the credentials in a preferred manner.
                await..........
                return new IAMCredentials
                {
                    TenantId = myTenancyOCID,
                    UserId = myUserOCID,
                    Fingerprint = myPublicKeyFingerprint,
                    PrivateKey = myPrivateKeyFile,
                    Passphrase = myPassphrase
                }
           })
    });
```

### <a name="instance_principal"></a>Authorizing with Instance Principal

*Instance Principal* is an IAM service feature that enables instances to be
authorized actors (or principals) to perform actions on service resources.
Each compute instance has its own identity, and it authenticates using the
certificates that are added to it.  See
[Calling Services from an Instance](https://docs.cloud.oracle.com/en-us/iaas/Content/Identity/Tasks/callingservicesfrominstances.htm) for prerequisite steps to set up Instance
Principal.

Once set up, create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
instance as follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = <your-service-region>,
        Compartment =
            "ocid1.compartment.oc1.............................",
        AuthorizationProvider =
            IAMAuthorizationProvider.CreateWithInstancePrincipal()
    });
```

You may also represent the same configuration in JSON as follows:

```json
{
    "Region": "<your-service-region>",
    "AuthorizationProvider":
    {
        "AuthorizationType": "IAM",
        "UseInstancePrincipal": true
    },
    "Compartment": "ocid1.compartment.oc1.............................",
}
```

For more details, see [Connecting an Application](#connecting).

Note that when using Instance Principal you must specify compartment id
(OCID) as *compartment* property (see
[Specifying a Compartment](#compartment)).  This is required even if you wish
to use default compartment.  Note that you must use compartment id and not
compartment name or path.  In addition, when using Instance Principal, you may
not prefix table name with compartment name or path when calling
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) APIs.

### <a name="resource_principal"></a>Authorizing with Resource Principal

*Resource Principal* is an IAM service feature that enables the resources to
be authorized actors (or principals) to perform actions on service resources.
You may use Resource Principal when calling Oracle NoSQL Database Cloud
Service from other Oracle Cloud service resource such as
[Functions](https://docs.cloud.oracle.com/en-us/iaas/Content/Functions/Concepts/functionsoverview.htm).
See [Accessing Other Oracle Cloud Infrastructure Resources from Running Functions](https://docs.cloud.oracle.com/en-us/iaas/Content/Functions/Tasks/functionsaccessingociresources.htm)
for how to set up Resource Principal.

Once set up, create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
instance as follows:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = <your-service-region>,
        Compartment =
            "ocid1.compartment.oc1.............................",
        AuthorizationProvider =
            IAMAuthorizationProvider.CreateWithResourcePrincipal()
    });
```

You may also represent the same configuration in JSON as follows:

```json
{
    "Region": "<your-service-region>",
    "AuthorizationProvider":
    {
        "AuthorizationType": "IAM",
        "UseResourcePrincipal": true
    },
    "Compartment": "ocid1.compartment.oc1.............................",
}
```

For more details, see [Connecting an Application](#connecting).

Note that when using Resource Principal you must specify compartment id
(OCID) as *compartment* property (see
[Specifying a Compartment](#compartment)).  This is required even if you wish
to use default compartment.  Note that you must use compartment id and not
compartment name or path.  In addition, when using Resource Principal, you may
not prefix table name with compartment name or path when calling
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) APIs.

## <a name="connecting"></a>Connecting an Application

To use the SDK in your code, add
[Oracle.NoSQL.SDK](xref:Oracle.NoSQL.SDK) namespace:

```csharp
using Oracle.NoSQL.SDK;
```

The first step in your Oracle NoSQL Database Cloud Service application is to
create an instance of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
class which is the main point of access to the service.  To create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance, you need to
supply an instance of [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig)
class containing the information needed to access the service.  Alternatively,
you may choose to supply a path (absolute or relative to current directory) to
a JSON file that contains the same configuration information as in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).

The required information consists of the communication region or endpoint and
authorization information described in section
[Acquire Credentials for the Oracle NoSQL Database Cloud Service](#creds)
(also see
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider)).

It is possible to specify a
[Region](xref:Oracle.NoSQL.SDK.NoSQLConfig.Region) or an
[Endpoint](xref:Oracle.NoSQL.SDK.NoSQLConfig.Endpoint), but not both. If
you use a region the endpoint of that region is inferred. If an endpoint is
used, it needs to be either the endpoint of a Region or a reference to a host
and port.  For example when using the Cloud Simulator you would use an
endpoint string like **http://localhost:8080**.

Other, optional parameters may also be specified in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig). See API documentation for
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) for more information.

For example:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        AuthorizationProvider = new IAMAuthorizationProvider(
            "~/myapp/.oci/config", "Jane"),
        Compartment =
            "ocid1.compartment.oc1.............................",
        Timeout = TimeSpan.FromSeconds(10)
    });
```

In addition to providing an instance of
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig), you may store the initial
configuration in a JSON file and provide a path to that file when creating
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.

For example, you may provide the same configuration as in above example using
the JSON file.  Create file **config.json** with following contents:

```json
{
    "Region": "us-ashburn-1",
    "AuthorizationProvider":
    {
        "AuthorizationType": "IAM",
        "ConfigFile": "~/myapp/.oci/config",
        "ProfileName": "Jane"
    },
    "Compartment": "ocid1.compartment.oc1.............................",
    "Timeout": 10000
}
```

Then you may create {@link NoSQLClient} instance as follows:

```csharp
var client = new NoSQLClient("/path/to/config.json");
```

In general, the JSON representation is very similar to the
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) instance, together with
certain rules for values that cannot be directly represented in JSON.  For
example, you may see the following from the above representation:

* [Region](xref:Oracle.NoSQL.SDK.Region) values are represented as
corresponding region identifiers.
* Authorization provider is represented as JSON object with the properties
* for a given provider class and an additional *AuthorizationType* property
indicating the type of the authorization provider.
* Timeout values are represented as their number of milliseconds.

These rules are described in detail in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).

In the *Oracle.NoSQL.SDK.Samples* directory, you will see JSON
configuration files that are used to create a
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance as shown above:
* Use *cloud_template.json* for the cloud service to create a configuration of
your choice as described in [Supply Credentials to the Application](#supply).
Fill in appropriate values for properties needed and remove the rest.
* Use *cloudsim.json* for the Cloud Simulator.

As metioned in section [Using a Configuration File](#config_file), you may
also create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance for
the cloud service with no-argument constructor (without any configuration
provided) if you are using a default configuration file with default profile
containing both the credentials and the service region.

### <a name="service_type"></a>Specifying Service Type

Because this SDK is used both for the Oracle NoSQL Cloud Service and the
On-Premise Oracle NoSQL Database,
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) instance can specify
that we are connecting to the cloud service by setting its
[ServiceType](xref:Oracle.NoSQL.SDK.NoSQLConfig.ServiceType) property to
[ServiceType.Cloud](xref:Oracle.NoSQL.SDK.ServiceType.Cloud).

You can always explicitly specify the
[ServiceType](xref:Oracle.NoSQL.SDK.NoSQLConfig.ServiceType) property in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig), but in many cases such as
in the examples above, it may be determined automatically.  In particular, the
driver will assume the cloud service if any of the following is true:

* [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) has
[Region](xref:Oracle.NoSQL.SDK.NoSQLConfig.Region) property set (as opposed
to [Endpoint](xref:Oracle.NoSQL.SDK.NoSQLConfig.Endpoint) property).  It is
recommended to use region instead of endpoint for the cloud service.
* The value of
[AuthorizationProvider](xref:Oracle.NoSQL.SDK.NoSQLConfig.AuthorizationProvider)
is an instance of
[IAMAuthorizationProvider](xref:Oracle.NoSQL.SDK.IAMAuthorizationProvider))
* No configuration is provided, with both the region and the credentials
stored in OCI configuration file in default location as described in section
[Using a Configuration File](#config_file).

On the other hand, for the configuration that specifies neither service type
nor authorization provider but that specifies the endpoint (and not region),
the service type will default to
[ServiceType.CloudSim](xref:Oracle.NoSQL.SDK.ServiceType.CloudSim) (see
[Using the Cloud Simulator](#cloudsim)).

For more details, see [ServiceType](xref:Oracle.NoSQL.SDK.ServiceType)
enumeration.

You may also specify the service type in a JSON configuration file as
string value of the [ServiceType](xref:Oracle.NoSQL.SDK.ServiceType)
enumeration constant.  For example:

```json
{
    "ServiceType": "Cloud",
    "Region": "us-ashburn-1"
}
```

###  <a name="compartment"></a>Specifying a Compartment

In the Oracle NoSQL Cloud Service environment tables are always created in an
Oracle Cloud Infrastructure *compartment* (see
[Managing Compartments](https://docs.cloud.oracle.com/en-us/iaas/Content/Identity/Tasks/managingcompartments.htm)).
It is recommended that compartments be created for tables to better organize
them and control security, which is a feature of compartments. The default
compartment for tables is the root compartment of the user's tenancy. A
default compartment for all operations can be specified by setting the
[Compartment](xref:Oracle.NoSQL.SDK.NoSQLConfig.Compartment) property of
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).  For example:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Region = Region.US_ASHBURN_1,
        Compartment = "<compartment_ocid_or_name>"
    });
```

The string value may be either a compartment OCID or a compartment name or
path.  If it is a simple name it must specify a top-level compartment. If it
is a path to a nested compartment, the top-level compartment must be excluded
as it is inferred from the tenancy.

In addition, all operation options classes have *Comparment* property, such as
[TableDDLOptions.Compartment](xref:Oracle.NoSQL.SDK.TableDDLOptions.Compartment),
[GetOptions.Compartment](xref:Oracle.NoSQL.SDK.GetOptions.Compartment),
[PutOptions.Compartment](xref:Oracle.NoSQL.SDK.PutOptions.Compartment),
etc.  Thus you may also specify comparment separately for any operation.
This value, if set, will override the compartment value in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig), if any.

If compartment is not supplied, the tenancy OCID will be used as default. Note
this only applies if you are [authorizing with user's identity](#user). When
using [instance principal](#instance_principal) or
[resouce principal](#resource_principal), compartment id must be specified.

## <a name="quickstart"></a>Example Quick Start

The examples in the SDK are configured to make it simple to connect and run
against the Oracle NoSQL Database Cloud Service. Follow these steps:

1. Acquire credentials. See [Acquire Credentials](#creds). You will need these:

* Tenancy ID
* User ID
* API signing key (private key file in PEM format
* Fingerprint for the public key uploaded to the user's account
* Private key pass phrase, needed only if the private key is encrypted

2. Put the information in a configuration file, ~/.oci/config, based on
the format described in [Using a Configuration File](#config_file). It should
look like this:

```ini
[DEFAULT]
tenancy=<your-tenancy-ocid>
user=<your-user-ocid>
fingerprint=<fingerprint-of-your-public-key>
key_file=<path-to-your-private-key-file>
pass_phrase=<your-private-key-passphrase>
region=<your-region-identifier>
```

Instead of using a configuration file it is possible to modify the example code
to directly provide your credentials as described in
[Specifying Credentials Directly](#config_api).

3.  Use git to clone the Oracle NoSQL Database .NET SDK:

```bash
git clone https://github.com/oracle/nosql-dotnet-sdk
```

Alternatively you may download the zip file or a tarball containing the SDK
source from the
[GitHub Releases](https://github.com/oracle/nosql-dotnet-sdk/releases) page.

4.  The examples are located under *Oracle.NoSQL.SDK.Samples* directory.
Go to the *BasicExample*:

```bash
cd  Oracle.NoSQL.SDK.Samples/BasicExample
```

Under this directory, you will see the example source code *Program.cs* and
the project *BasicExample.csproj*.

5.  Build and run the project:

```bash
dotnet run -f <your_target_framework>
```

The project supports multiple
[target frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
which currently are .NET Core 3.1 and .NET 5.0, so you must specify the target
framework to use.

For .NET 5.0, specify *net5.0*:

```bash
dotnet run -f net5.0
```

For .NET Core 3.1, specify *netcoreapp3.1*:

```bash
dotnet run -f netcoreapp3.1
```

Note that the commands above will automatically download and install Oracle
NoSQL Database SDK package as a dependency of the example project.

Alternatively, you may build and run the example project in
[Visual Studio](https://visualstudio.microsoft.com/).  In Visual Studio, open
the Samples solution located at
*Oracle.NoSQL.SDK.Samples/Oracle.NoSQL.SDK.Samples.sln*.

## <a name="cloudsim"></a>Using the Cloud Simulator

The configuration instructions above are for getting connected to the actual
Oracle NoSQL Database Cloud Service.

You may first get familiar with Oracle NoSQL Database .NET SDK and its
interfaces by using the
[Oracle NoSQL Cloud Simulator](https://docs.oracle.com/en/cloud/paas/nosql-cloud/csnsd/develop-oracle-nosql-cloud-simulator.html).

The Cloud Simulator simulates the cloud service and lets you write and test
applications locally without accessing Oracle NoSQL Database Cloud Service.

You can start developing your application with the Oracle NoSQL Cloud
Simulator, using and understanding the basic examples, before you get
started with the Oracle NoSQL Database Cloud Service. After building,
debugging and testing your application with the Oracle NoSQL Cloud
Simulator, move your application to the Oracle NoSQL Database Cloud Service.

Note that the Cloud Simulator does not require authorization information and
credentials described above that are required by Oracle NoSQL Database
Cloud Service.  Only the endpoint is required and is by default *localhost:8080*.

Follow these instructions to run an example program against the Cloud
Simulator:

1. [Download](https://docs.oracle.com/pls/topic/lookup?ctx=en/cloud/paas/nosql-cloud&id=CSNSD-GUID-3E11C056-B144-4EEA-8224-37F4C3CB83F6)
and start the Cloud Simulator.

2. Follow step 3. of [Example Quick Start](#quickstart) to obtain the SDK
source.

3. In *Oracle.NoSQL.SDK.Samples* directory you will find the file
*cloudsim.json* containging default configuration for the Cloud Simulator.
It should look like this:

```json
{
    "Endpoint": "http://localhost:8080"
}
```

Copy/edit this file to modify the endpoint if you are running the Cloud
Simulator on a different port or another machine.  You may also add other
configuration properties described in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) if needed.  Note that
[AuthorizationProvider](xref:Oracle.NoSQL.SDK.NoSQLConfig.AuthorizationProvider)
property should not be set for the Cloud Simulator.

4.  Build and run examples as described in steps 4. and 5. of the
[Example Quick Start](#quickstart).  Each example takes the path to the
JSON configuration file as an optional command line parameter, which you can
provide to the *dotnet run* command.  For example:

```bash
cd Oracle.NoSQL.SDK.Samples/BasicExample
dotnet run -f net5.0 -- ../cloudsim.json
```

As described in section [Specifying Service Type](#service_type), for the
configuration above, you do not need to specify
[ServiceType](xref:Oracle.NoSQL.SDK.NoSQLConfig.ServiceType) property which
will default to
[ServiceType.CloudSim](xref:Oracle.NoSQL.SDK.ServiceType.CloudSim).
