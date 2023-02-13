This guide describes how to install, configure, and use the Oracle NoSQL
Database Node.js SDK with On-Premise Oracle NoSQL Database.

## <a name="prereq"></a>Prerequisites

The SDK requires:

* [Oracle NoSQL Database](https://www.oracle.com/database/technologies/related/nosql.html).
See
[Oracle NoSQL Database Downloads](https://www.oracle.com/database/technologies/nosql-database-server-downloads.html)
to download Oracle NoSQL Database. See
[Oracle NoSQL Database Documentation](https://docs.oracle.com/en/database/other-databases/nosql-database/index.html)
to get started with Oracle NoSQL Database.
In particular, see the
[Administrator Guide](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/index.html)
on how to install, configure and run Oracle NoSQL Database Service.
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

## <a name="config"></a>Configuring the SDK

To use the SDK with the On-Premise Oracle NoSQL Database you need the following
components:

1. Running instance of Oracle NoSQL Database.  See [Prerequisites](#prereq).
2. Oracle NoSQL Database Proxy.  The proxy is the middle tier that lets Oracle
NoSQL Database drivers communicate with the database.  See
[Oracle NoSQL Database Proxy](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/proxy.html) for information on how to configure and run the proxy.

A Oracle NoSQL Database instance may be configured and run in secure or
non-secure mode.  Secure mode is recommended.  See
[Oracle NoSQL Database Security Guide](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/security/index.html)
on security concepts and configuration.  Correspondingly, the proxy can be
configured and used with [secure kvstore](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/secure-proxy.html) or
[non-secure kvstore](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/non-secure-proxy.html).

Your application  will connect and use a running NoSQL database via
the proxy service.  The following sections describe information required in non-secure
and secure modes.

### <a name="non_secure"></a>Configuring the SDK for non-secure kvstore

See
[Using the Proxy in a Non-Secure kvstore](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/non-secure-proxy.html)
on how to configure and start the proxy in a non-secure mode.

In non-secure mode, the driver communicates with the proxy via the HTTP protocol.
The only information required is the communication *endpoint*.  For on-premise
NoSQL Database, the endpoint specifies the url of the proxy, in the form
*http://proxy_host:proxy_http_port*.  For example:

```csharp
    var endpoint = "http://localhost:8080";
```

You may also omit the protocol portion:

```csharp
    var endpoint = "myhost:8080";
```

See [NoSQLConfig.Endpoint](xref:Oracle.NoSQL.SDK.NoSQLConfig.Endpoint) for
more information on the endpoint.

Also, see
[Connecting to a Non-Secure Store](#connect_non_secure) on how to connect to
non-secure store.

### <a name="secure"></a>Configuring the SDK for a Secure Store

See
[Using the Proxy in a Secure kvstore](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/admin/secure-proxy.html)
on how to configure and start the proxy in a secure mode.

In secure mode, the driver communicates with the proxy via HTTPS protocol.
The following information is required:

1. Communication endpoint which is the https url of the proxy in the form
*https://proxy_host:proxy_https_port*.  For example:

```csharp
    var endpoint = "https://localhost:8181";
```

Note that unless using port 443, the protocol portion of the url is required.

See [NoSQLConfig.Endpoint](xref:Oracle.NoSQL.SDK.NoSQLConfig.Endpoint) for
details.

2. User for the driver which is used by the application to access the kvstore
through the proxy.  Use the following SQL to create the driver user:

```sql
sql-> CREATE USER <driver_user> IDENTIFIED BY "<driver_password>"
sql-> GRANT READWRITE TO USER <driver_user>
```

where, the *driver_user* is the username and *driver_password* is the password
for the *driver_user* user. In this example, the user *driver_user* is granted
*READWRITE* role, which allows the application to perform only read and
write operations.
See
[Configuring Authentication](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/security/configuring-authentication.html)
on how to create and modify users.
See
[Configuring Authorization](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/security/configuring-authorization.html)
on how to assign roles and privileges to users.

You can use
[Oracle NoSQL Database Shell](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/sqlfornosql/introduction-sql-shell.html)
to connect to secure kvstore in order to create the user.  For example:

```bash
java -jar lib/sql.jar -helper-hosts localhost:5000 -store kvstore -security kvroot/security/user.security
```

```sql
sql-> CREATE USER John IDENTIFIED BY "JohnDriver@@123"
sql-> GRANT READWRITE TO USER John
```

(The password shown above is for example purpose only.  All user passwords
should follow the password security policies.  See
[Password Complexity Policies](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/security/password-complexity-policies.html))

The driver requires user name and password created above to authenticate with
a secure store via the proxy.

3. In secure mode the proxy requires SSL
[Certificate and Private key](https://docs.oracle.com/en/database/other-databases/nosql-database/22.3/security/generating-certificate-and-private-key-proxy.html).
If the root certificate authority (CA) for your proxy certificate is not one
of the trusted root CAs, e.g. if you are using a self-signed certificate or a
custom CA, the driver needs to trust that CA/certificate in order to connect
to the proxy.  You can provide trusted root certificates to the driver by
specifying
[TrustedRootCertificateFile](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificateFile)
property.

See [Specifying Trusted Root Certificates](#connect_cert) for details.

The trusted root certificate file must be in PEM format and may
contain one or more trusted root certificates.  The certificate(s) in this
file may be either custom root CA certificate that issued the proxy
certificate or a self-signed proxy certificate used for development.

Note that this step is not required if the certificate chain
for your proxy certificate has one of well-known CAs at its root (these are
CAs that you will find in your operating system trust store).

Also, see [Connecting to a Secure Store](#connect_secure) on how to connect to
a secure store.

## <a name="connecting"></a>Connecting an Application

To use the SDK in your code, add
[Oracle.NoSQL.SDK](xref:Oracle.NoSQL.SDK) namespace:

```csharp
using Oracle.NoSQL.SDK;
```

The first step in your Oracle NoSQL Database application is to
create an instance of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
class which is the main point of access to the service.  To create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance, you need to
supply an instance of [NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig)
class containing information needed to access the service.  Alternatively, you
may choose to supply a path (absolute or relative to current directory) to a
JSON file that contains the same configuration information as in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).

### <a name="service_type"></a>Specifying Service Type

Since Oracle Database NoSQL .NET SDK is used both for Oracle NoSQL Cloud
Service and On-Premise Oracle NoSQL Database,
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) object may need to specify
that we are connecting to on-premise NoSQL Database by setting its
[ServiceType](xref:Oracle.NoSQL.SDK.NoSQLConfig.ServiceType) property to
[ServiceType.KVStore](xref:Oracle.NoSQL.SDK.ServiceType.KVStore). You must
specify [ServiceType](xref:Oracle.NoSQL.SDK.NoSQLConfig.ServiceType) to
connect to non-secure store.  For secure store, because the
[AuthorizationProvider](xref:Oracle.NoSQL.SDK.NoSQLConfig.AuthorizationProvider)
property will be set to an instance of
[KVStoreAuthorizationProvider](xref:Oracle.NoSQL.SDK.KVStoreAuthorizationProvider)
the service type will default to
[ServiceType.KVStore](xref:Oracle.NoSQL.SDK.ServiceType.KVStore), thus you
do not need to set the service type explicitly.  See
[ServiceType](xref:Oracle.NoSQL.SDK.ServiceType) for details.

Other required information has been described in section
[Configuring the SDK](#config) and is different for connections to non-secure
and secure stores.

### <a name="connect_non_secure"></a>Connecting to a Non-Secure Store

To connect to the proxy in non-secure mode, you need to specify communication
endpoint and the service type as
[ServiceType.KVStore](xref:Oracle.NoSQL.SDK.ServiceType.KVStore).

You can provide an instance of
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig) either directly or in a
JSON configuration file.

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        ServiceType = ServiceType.KVStore,
        Endpoint = "myhost:8080"
    });
```

You may also choose to provide the same configuration in JSON configuration
file.  Create file *config.json* with following contents:

```json
{
    "ServiceType": "KVStore",
    "Endpoint": "myhost:8080"
}
```

Then you may use this file to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance:

```csharp
var client = new NoSQLClient("/path/to/config.json");
```

As shown above, you specify the service type in a JSON configuration file as
a string value of the [ServiceType](xref:Oracle.NoSQL.SDK.ServiceType)
enumeration constant.  See [ServiceType](xref:Oracle.NoSQL.SDK.ServiceType)
for more details.

### <a name="connect_secure"></a>Connecting to a Secure Store

To connect to the proxy in secure mode, in addition to communication endpoint,
you need to specify user name and password of the driver user.  This
information is passed in the instance of
[KVStoreAuthorizationProvider](xref:Oracle.NoSQL.SDK.KVStoreAuthorizationProvider)
and can be specified in one of 3 ways as described below.

As described in section [Specifying Service Type](#service_type), we can omit
the service type from the configuration for secure store.

#### Passing user name and password directly

You may choose to specify user name and password directly:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Endpoint = "https://myhost:8081",
        AuthorizationProvider = new KVStoreAuthorizationProvider(
            userName, // user name as string
            password) // password as char[]
    });
```

This option is less secure because the password is stored in plain text in
memory for the lifetime of [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient)
instance.  Note that the password is specified as *char[]* which allows you to
erase it after you are finished using
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient).

#### <a name="credentials_file"></a>Storing credentials in a file

You may choose to store credentials in a separate file which is protected
by file system permissions, thus making it more secure than the previous
option, because the credentials will not be stored in memory, but will be
accessed from this file only when the login to the store is required.

Credentials file should have the following format:

```json
{
    "UserName": "<Driver user name>",
    "Password": "<Driver user password>"
}
```

Then you may use this credentials file as following:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Endpoint: 'https://myhost:8081',
        AuthorizationProvider = new KVStoreAuthorizationProvider(
            "path/to/credentials.json")
    });
```

You may also reference *credentials.json* in the JSON configuration file used
to create [NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance:

*config.json*

```json
{
    "Endpoint": "https://myhost:8081",
    "AuthorizationProvider": {
        "AuthorizationType": "KVStore",
        "CredentialsFile": "path/to/credentials.json"
    }
}
```

```csharp
var client = new NoSQLClient("/path/to/config.json");
```

Note that in *config.json* the authorization provider is represented as a JSON
object with the properties for
[KVStoreAuthorizationProvider](xref:Oracle.NoSQL.SDK.KVStoreAuthorizationProvider)
and an additional *AuthorizationType* property indicating the type of the
authorization provider, which is *KVStore* for the secure on-premise store.

For more details on the JSON representation, see
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig).

As an aside, it is also possible to specify credentials directly in the JSON
configuration file using *Credentials* property:

*config.json*

```json
{
    "Endpoint": "https://myhost:8081",
    "AuthorizationProvider": {
        "AuthorizationType": "KVStore",
        "Credentials": {
            "UserName": "<user_name>",
            "Password": "<password>"
        }
    }
}
```

Note that the above is not secure and should only be used during testing.

#### Creating Your Own Credentials Provider

You may implement your own credentials provider for secure storage and
retrieval of driver credentials.  This is the most secure option because
you are in control of how the credentials are stored and loaded by the driver.
The credentials provider is a delegate function that returns
Task<[KVStoreCredentials](xref:Oracle.NoSQL.SDK.KVStoreCredentials)> and
thus may be implemented asynchronously.

(Note that [KVStoreCredentials](xref:Oracle.NoSQL.SDK.KVStoreCredentials)
is a class that encapsulates the user name and password).

For example:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        "Endpoint": "https://myhost:8081",
        AuthorizationProvider = new KVStoreAuthorizationProvider(
           async (CancellationToken) => {
                // Retrieve the credentials in a preferred manner.
                await..........
                return new KVStoreCredentials(myUserName, myPassword);
           })
    });
```

#### <a name="connect_cert"></a>Specifying Trusted Root Certificates

As described in section
[Configuring the SDK for a Secure Store](#secure), you may need to
provide trusted root certificate to the driver if the certificate chain
for your proxy certificate is not rooted in one of the well known CAs.  The
provided certificate may be either your custom CA or self-signed proxy
certificate.  It may be specified using
[TrustedRootCertificateFile](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificateFile)
property, which sets a file path (absolute or relative) to a PEM file
containing one or more trusted root certificates (multiple roots are allowed
in this file).  This property is specified as part of
[ConnectionOptions](xref:Oracle.NoSQL.SDK.NoSQLConfig.ConnectionOptions) in
[NoSQLConfig](xref:Oracle.NoSQL.SDK.NoSQLConfig):

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Endpoint =  "https://myhost:8081",
        AuthorizationProvider = new KVStoreAuthorizationProvider(...),
        ConnectionOptions = new ConnectionOptions
        {
            TrustedRootCertificateFile = "path/to/certificates.pem"
        }
    });
```
You may also specify the same in JSON configuration file.  For example:

```json
{
    "Endpoint": "https://myhost:8081",
    "AuthorizationProvider": {
        "AuthorizationType": "KVStore",
        "CredentialsFile": "path/to/credentials.json"
    },
    "ConnectionOptions": {
        "TrustedRootCertificateFile": "path/to/certificates.pem"
    }
}
```

Alternatively you may use (in code only)
[TrustedRootCertificates](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificates)
property to explicitly specify
[X509Certificate2Collection](xref:System.Security.Cryptography.X509Certificates.X509Certificate2Collection)
instance containing trusted root certificates:

```csharp
var client = new NoSQLClient(
    new NoSQLConfig
    {
        Endpoint = "...",
        AuthorizationProvider = new KVStoreAuthorizationProvider(...),
        ConnectionOptions = new ConnectionOptions
        {
            TrustedRootCertificates = new X509Certificate2Collection(...)
        }
    });
```

Note that in this case the application is responsible for
[disposing](xref:System.Security.Cryptography.X509Certificates.X509Certificate.Dispose)
of each certificate in the collection after you have finished using
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance.

Also note that you may use only one of
[TrustedRootCertificates](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificates)
or
[TrustedRootCertificateFile](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificateFile)
properties.

Alternatively to specifying trusted root certificates in the initial
configuration, you may put your root certificate(s) to the
trusted root certificate store on your machine for the operating system
user which will run the application.  This is less secure option than using
[TrustedRootCertificateFile](xref:Oracle.NoSQL.SDK.ConnectionOptions.TrustedRootCertificateFile)
because it will make your certificate trusted for other applications running
on behalf of your operating system user.  Here are some pointers on this
procedure, which depends on the operating system:

* On Windows, use Certificate Manager Tool *certmgr.msc* which is part of
Microsoft Management Console.  You can launch *certmgr.msc* from the command
line or the Start menu.  For more details, see
[How to: View certificates with the MMC snap-in](https://docs.microsoft.com/en-us/dotnet/framework/wcf/feature-details/how-to-view-certificates-with-the-mmc-snap-in).
* On Linux, place the certificate in the appropriate directory and run command
*update-ca-certificates* or *update-ca-trust*.  The details depend on the
distribution.  Refer to your Linux distribution documentation and manual pages
for the above commands for more details.  Note that you may need a root or
sudo access.
* On Mac, use *Keychain Access* to import trusted root certificate.

## <a name="quickstart"></a>Example Quick Start

The examples in the SDK are configured to make it simple to connect and run
against the On-Premise Oracle NoSQL Database. Follow these steps:

1. Perform the steps outlined in section [Configuring the SDK](#config) to run
Oracle NoSQL Database instance and Oracle NoSQL Database Proxy.  You can
configure for [secure](#secure) or [non-secure](#non_secure) store.

2.  Use git to clone the Oracle NoSQL Database .NET SDK:

```bash
git clone https://github.com/oracle/nosql-dotnet-sdk
```

Alternatively you may download the zip file or a tarball containing the SDK
source from the
[GitHub Releases](https://github.com/oracle/nosql-dotnet-sdk/releases) page.

3.  The examples are located under *Oracle.NoSQL.SDK.Samples* directory.
In this directory you will find the file *kvstore_template.json*.  It is used
as a JSON configuration file to create
[NoSQLClient](xref:Oracle.NoSQL.SDK.NoSQLClient) instance. Make a copy of
this file and fill in appropriate values depending on whether you are
connecting to secure or non-secure store.

Fill in the service endpoint as *Endpoint* property as described for
[secure](#secure) or [non-secure](#non_secure) store.

For a secure store, either supply a path to credentials file in a
*CredentialsFile* property (see
[Storing credentials in a file](#credentials_file)) or supply credentials
directly inside *Credentials* property (only to be used for development).
Remove the other unused property.  If applicable (see section
[Specifying Trusted Root Certificates](#connect_cert)), you may also specify
the trusted root certificate file.  Otherwise, remove the entire
"ConnectionOptions" property.

For example:

```json
{
    "ServiceType": "KVStore",
    "Endpoint": "https://somehost:443",
    "AuthorizationProvider": {
        "AuthorizationType": "KVStore",
        "Credentials":
        {
            "UserName": "<your-driver-user>",
            "Password": "<your-driver-password>"
        }
    },
    "ConnectionOptions": {
        "TrustedRootCertificateFile": "<trusted-root-certificate.pem>"
    }
}
```

For a non-secure store, remove the entire "AuthorizationProvider" and
"ConnectionOptions" properties. For example:

```json
{
    "ServiceType": "KVStore",
    "Endpoint": "http://localhost:8080"
}
```

4. Go to the *BasicExample*:

```bash
cd  Oracle.NoSQL.SDK.Samples/BasicExample
```

Under this directory, you will see the example source code *Program.cs* and
the project *BasicExample.csproj*.

5.  Build and run the project, providing the path to the JSON configuration
file you created as a command line argument to the example program:

```bash
dotnet run -f <your_target_framework> -- /path/to/config.json
```

The project supports multiple
[target frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
which currently are .NET Core 3.1 and .NET 5.0, so you must specify the target
framework to use.

For .NET 5.0, specify *net5.0*:

```bash
dotnet run -f net5.0 -- /path/to/config.json
```

For .NET Core 3.1, specify *netcoreapp3.1*:

```bash
dotnet run -f netcoreapp3.1 -- /path/to/config.json
```

Note that the commands above will automatically download and install Oracle
NoSQL Database SDK package as a dependency of the example project.

Alternatively, you may build and run the example project in
[Visual Studio](https://visualstudio.microsoft.com/).  In Visual Studio, open
the Samples solution located at
*Oracle.NoSQL.SDK.Samples/Oracle.NoSQL.SDK.Samples.sln*.
