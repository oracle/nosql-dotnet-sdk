/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Represents configuration required to instantiate
    /// <see cref="NoSQLClient" /> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class contains properties that are needed to create
    /// and configure <see cref="NoSQLClient"/> instance.  Some properties are
    /// needed in order to establish the connection with the NoSQL service and
    /// their values are required to be provided.  Most of the other
    /// properties serve as defaults for various options passed to
    /// <see cref="NoSQLClient"/> APIs.   They are optional and have their own
    /// default values.
    /// </para>
    /// <para>
    /// The configuration may be stored in JSON format as a string or in a
    /// file and you may construct <see cref="NoSQLClient"/> instance by
    /// providing a path to a JSON file storing the configuration (see
    /// <see cref="NoSQLClient(string)"/>).  <see cref="NoSQLConfig"/>
    /// instance is represented as JSON object using the following rules:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// In general, every property is represented as JSON field with the
    /// field name being the same as the property name and the field value
    /// being the property value, unless otherwise specified in a
    /// documentation for a particular property.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The field names are matched in a case-insensitive manner.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Enumeration values are represented as string values of the
    /// corresponding enumeration constants, the match being case-insensitive,
    /// unless otherwise specified.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Each <see cref="TimeSpan"/> value is represented as the number of
    /// milliseconds.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Complex property values of properties such as
    /// <see cref="RetryHandler"/> and <see cref="AuthorizationProvider"/> are
    /// represented as JSON objects with the same rules as above applying
    /// within each such object.
    /// </description>
    /// </item>
    /// </list>
    /// The examples show how the same configuration would be represented in
    /// C# code and in JSON.
    /// </para>
    /// </remarks>
    /// <example>
    /// Initializing <see cref="NoSQLConfig"/> instance.
    /// <code>
    /// var config = new NoSQLConfig
    /// {
    ///     ServiceType = ServiceType.Cloud,
    ///     Region = Region.AP_MUMBAI_1,
    ///     Timeout = TimeSpan.FromSeconds(15),
    ///     RetryHandler = new NoSQLRetryHandler
    ///     {
    ///         MaxRetryAttempts = 20,
    ///         BaseDelay = TimeSpan.FromSeconds(2),
    ///         ControlOperationBaseDelay = TimeSpan.FromMinutes(2)
    ///     },
    ///     AuthorizationProvider = new IAMAuthorizationProvider(
    ///         "~/my_app/.oci/config", "Jane")
    /// };
    /// </code>
    /// </example>
    /// <example>
    /// Representing <see cref="NoSQLConfig"/> instance in JSON.  This is the
    /// same configuration as in the first example.
    /// <code>
    /// {
    ///     "ServiceType": "Cloud",
    ///     "Region": "AP_MUMBAI_1",
    ///     "Timeout": 15000,
    ///     "RetryHandler": {
    ///         "MaxRetryAttempts": 20,
    ///         "BaseDelay": 2000,
    ///         "ControlOperationBaseDelay": 120000
    ///     },
    ///     "AuthorizationType": "IAM",
    ///     "AuthorizationProvider": {
    ///         "ConfigFile": "~/my_app/.oci/config",
    ///         "ProfileName": "Jane"
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <see cref="NoSQLClient(NoSQLConfig)"/>
    /// <see cref="NoSQLClient(string)"/>
    public partial class NoSQLConfig
    {
        /// <summary>
        /// Retry handler instance that disables all operation retries.
        /// </summary>
        /// <remarks>
        /// You can set <see cref="NoSQLConfig.RetryHandler"/> to this value
        /// to disable all operation retries.  In this case the application
        /// may retry the operations manually.  This value is a read-only
        /// singleton instance.
        /// </remarks>
        public static readonly IRetryHandler NoRetries =
            new NoRetriesHandler();

        /// <summary>
        /// Gets or sets the type of Oracle NoSQL service to use.
        /// </summary>
        /// <value>
        /// The service type.  The default is
        /// <see cref="SDK.ServiceType.Unspecified"/>, in which case the
        /// driver will try to determine the service type from an
        /// authorization provider settings, see <see cref="ServiceType"/>.
        /// </value>
        /// <seealso cref="ServiceType"/>
        public ServiceType ServiceType { get; set; }

        /// <summary>
        /// Cloud Service Only.  Gets or sets a region to use to connect to
        /// the Oracle NoSQL Database Cloud Service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is an alternative to <see cref="Endpoint"/>. The
        /// service endpoint will be inferred from the region.  This value
        /// must be one of the region constants defined in the
        /// <see cref="Region"/> class.
        /// </para>
        /// <para>
        /// When using JSON configuration file, specify this property as
        /// a string equal to the name of the region constant, such as
        /// <em>AP_MUMBAI_1</em>, <em>US_ASHBURN_1</em>, etc. or value of the
        /// region identifier such as <em>ap-mumbai-1</em>,
        /// <em>us-ashburn-1</em>, etc. (see
        /// <see cref="SDK.Region.RegionId"/>).
        /// </para>
        /// <para>
        /// You must specify either <see cref="NoSQLConfig.Region"/> or
        /// <see cref="NoSQLConfig.Endpoint"/> but not both.  One possible
        /// exception is when the region is stored in the OCI configuration
        /// file together with the credentials, in which case you need not
        /// specify either property in <see cref="NoSQLConfig"/>.  In this
        /// case you must either set
        /// <see cref="NoSQLConfig.AuthorizationProvider"/> to an instance of
        /// <see cref="IAMAuthorizationProvider"/> configured to use that OCI
        /// configuration file or use the default OCI configuration file and
        /// the default instance of <see cref="IAMAuthorizationProvider"/>.
        /// </para>
        /// <para>
        /// Note that setting <see cref="NoSQLConfig.Region"/> takes
        /// precedence over region identifier in the OCI configuration file if
        /// one is used.
        /// </para>
        /// </remarks>
        /// <value>
        /// The region identifier.  If not set, the
        /// <see cref="NoSQLConfig.Endpoint"/> or the region identifier in the
        /// OCI configuration file will be used, see remarks section.
        /// </value>
        /// <seealso cref="SDK.Region"/>
        public Region Region { get; set; }

        /// <summary>
        /// Gets or sets the endpoint to use to connect to Oracle NoSQL
        /// Database Service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When using the cloud service, you may choose to set
        /// <see cref="NoSQLConfig.Region"/> instead.  Each region has its own
        /// endpoint.  E.g. <see cref="SDK.Region.US_PHOENIX_1"/> has
        /// endpoint <em>https://nosql.us-phoenix-1.oci.oraclecloud.com</em>.
        /// </para>
        /// <para>
        /// The endpoint specifies the host to connect to but may optionally
        /// specify port and/or protocol.  The protocol is usually not needed
        /// and is inferred from the host and port information.  If no port is
        /// specified, port 443 and protocol <em>https</em> are assumed.  If
        /// port 443 is specified, <em>https</em> is assumed.  If the port is
        /// specified and it is not 443, <em>http</em> is assumed.  If
        /// the protocol is specified, it must be either <em>http</em> or
        /// <em>https</em>.  For example,
        /// <em>https://nosql.ap-seoul-1.oci.oraclecloud.com</em> or
        /// <em>http://localhost:8080</em>.  If the protocol is specified but
        /// not the port, 443 is assumed for <em>https</em> and 8080 for
        /// <em>https</em> (8080 is also the default port for the Cloud
        /// Simulator).
        /// </para>
        /// <para>
        /// Per described above, the examples of allowable endpoints would be:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <em>nosqlhost</em> is equivalent to
        /// <em>https://nosqlhost:443</em>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <em>nosqlhost:443</em> is also equivalent to
        /// <em>https://nosqlhost:443</em>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <em>nosqlhost:8888</em> is equivalent to
        /// <em>http://nosqlhost:8888</em>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <em>http://localhost</em> is equivalent to
        /// <em>http://localhost:8080</em>.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// You may specify either <see cref="NoSQLConfig.Endpoint"/> or
        /// <see cref="NoSQLConfig.Region"/> but not both.
        /// </para>
        /// </remarks>
        /// <value>
        /// The region identifier.  If not set, the
        /// <see cref="NoSQLConfig.Region"/> or the region identifier in the
        /// OCI configuration file will be used, see
        /// <see cref="NoSQLConfig.Region"/>.
        /// </value>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets a timeout for non-DDL operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Specifies timeout for all operations with the exception of DDL
        /// operations such as
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>.
        /// This value serves as the default if the timeout is not specified
        /// in the options for a non-DDL operation.  For DDL operations, see
        /// <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="AdminTimeout"/>.
        /// </para>
        /// <para>
        /// Note that for operations that are automatically retried, the
        /// timeout is cumulative over all retries and not just a timeout for
        /// a single retry.  This means that all retries and waiting periods
        /// between the retries are counted towards the timeout.
        /// </para>
        /// </remarks>
        /// <value>
        /// The timeout for non-DDL operations.  The default is 5 seconds.
        /// </value>
        /// <seealso cref="TableDDLTimeout"/>
        /// <seealso cref="AdminTimeout"/>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets a timeout for table DDL operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Table DDL operations are operations performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
        /// <see cref="NoSQLClient.SetTableLimitsAsync"/>,
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// and
        /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>.
        /// This value serves as the default if
        /// <see cref="TableDDLOptions.Timeout"/> is not set.
        /// </para>
        /// <para>
        /// Note that for operations performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// and
        /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>, the
        /// default timeout is the sum of <see cref="TableDDLTimeout"/> and
        /// <see cref="TablePollTimeout"/> if <see cref="TablePollTimeout"/>
        /// is specified, and no timeout otherwise.
        /// </para>
        /// </remarks>
        /// <value>
        /// The timeout for table DDL operations.  The default is 10 seconds.
        /// </value>
        public TimeSpan TableDDLTimeout { get; set; } =
            TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets a timeout for admin DDL operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Admin DDL operations are operations performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
        /// and
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>.
        /// This value serves as the default if
        /// <see cref="AdminOptions.Timeout"/> is not set.
        /// </para>
        /// <para>
        /// Note that for operations performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>,
        /// the default timeout is the sum of <see cref="AdminTimeout"/>
        /// and <see cref="AdminPollTimeout"/> if
        /// <see cref="AdminPollTimeout"/> is specified, and no timeout
        /// otherwise.
        /// </para>
        /// </remarks>
        /// <value>
        /// The timeout for admin DDL operations.  The default is 10 seconds.
        /// </value>
        public TimeSpan AdminTimeout { get; set; } =
            TimeSpan.FromSeconds(10);

        // Made security info related settings invisible to the user as this
        // handling will be moved out of the driver.

        /// <summary>
        /// Cloud Service only.  Gets or sets a timeout to wait for security
        /// information to be available in the system.
        /// </summary>
        /// <remarks>
        /// By default, if an operation fails with
        /// <see cref="SecurityInfoNotReadyException"/>, it will be
        /// automatically retried.  Because it may take some time for the
        /// security information to become available, this timeout value will
        /// be used while retrying the operation if it exceeds the original
        /// operation timeout (whichever value is greater will be used).
        /// </remarks>
        /// <value>
        /// Timeout to wait for security information to be available in the
        /// system.  The default is 10 seconds.
        /// </value>
        internal TimeSpan SecurityInfoNotReadyTimeout { get; set; } =
            TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the timeout to wait for completion of a table DDL
        /// operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Methods <see cref="TableResult.WaitForCompletionAsync"/> and
        /// <see cref="NoSQLClient.WaitForTableStateAsync"/> wait for
        /// completion of a table DDL operation by polling the table for
        /// status at regular intervals.  This property sets a timeout on the
        /// duration of the calls to
        /// <see cref="TableResult.WaitForCompletionAsync"/> and
        /// <see cref="NoSQLClient.WaitForTableStateAsync"/>.  Set this
        /// property to limit the duration of these polling operations.
        /// Because table DDL operations may be potentially long running,
        /// there is no default timeout.
        /// </para>
        /// <para>
        /// This property serves as a default for
        /// <see cref="TableResult.WaitForCompletionAsync"/> if the timeout
        /// parameter is not provided as well as the default for
        /// <see cref="NoSQLClient.WaitForTableStateAsync"/> if the timeout is
        /// not set in <see cref="TableCompletionOptions"/>.
        /// </para>
        /// <para>
        /// If <see cref="TableDDLOptions.Timeout"/> is not set, the value of
        /// this property is added to the the default timeout for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// and <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
        /// APIs because these operations already include the polling for the
        /// table status.  If this property is not set, these operations will
        /// likewise have no timeout.
        /// </para>
        /// </remarks>
        /// <value>
        /// Timeout to wait for completion of a table DDL operation.  If not
        /// set, the default is no timeout (infinity).
        /// </value>
        /// <seealso cref="NoSQLClient.ExecuteTableDDLAsync"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        /// <seealso cref="NoSQLClient.WaitForTableStateAsync"/>
        public TimeSpan? TablePollTimeout { get; set; }

        /// <summary>
        /// Gets or sets the delay between successive polls when waiting for
        /// completion of a table DDL operation.
        /// </summary>
        /// <remarks>
        /// Methods <see cref="TableResult.WaitForCompletionAsync"/> and
        /// <see cref="NoSQLClient.WaitForTableStateAsync"/> wait for
        /// completion of a table DDL operation by polling the table for
        /// status at regular intervals.  This property sets a delay between
        /// the successive polls and serves as a default value if the poll
        /// delay parameter is not passed to
        /// <see cref="TableResult.WaitForCompletionAsync"/> as well as the
        /// default for <see cref="TableDDLOptions.PollDelay"/> and
        /// <see cref="TableCompletionOptions.PollDelay"/>.
        /// </remarks>
        /// <value>
        /// Delay between successive polls when waiting for completion of
        /// a table DDL operation.  The default is 1 second.
        /// </value>
        public TimeSpan TablePollDelay { get; set; } =
            TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the timeout to wait for completion of an admin DDL
        /// operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Method <see cref="AdminResult.WaitForCompletionAsync"/> waits for
        /// completion of an admin DDL operation by polling for the operation
        /// status at regular intervals.  This property sets a timeout on the
        /// duration of the call to
        /// <see cref="AdminResult.WaitForCompletionAsync"/>.  Set this
        /// property to limit the duration of the polling operation.
        /// Because admin DDL operations may be potentially long running,
        /// there is no default timeout.
        /// </para>
        /// <para>
        /// This property serves as a default for
        /// <see cref="AdminResult.WaitForCompletionAsync"/> if the timeout
        /// parameter is not provided.
        /// </para>
        /// <para>
        /// If <see cref="TableDDLOptions.Timeout"/> is not set, the value of
        /// this property is added to the the default timeout for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
        /// API because this operation already includes the polling for the
        /// operation status.  If this property is not set, this operation
        /// will likewise have no timeout.
        /// </para>
        /// </remarks>
        /// <value>
        /// Timeout to wait for completion of an admin DDL operation.  If not
        /// set, the default is no timeout (infinity).
        /// </value>
        /// <seealso cref="NoSQLClient.ExecuteAdminAsync"/>
        /// <seealso cref="AdminResult.WaitForCompletionAsync"/>
        public TimeSpan? AdminPollTimeout { get; set; }

        /// <summary>
        /// Gets or sets the delay between successive polls when waiting for
        /// completion of an DDL operation.
        /// </summary>
        /// <remarks>
        /// Method <see cref="AdminResult.WaitForCompletionAsync"/> waits for
        /// completion of an admin DDL operation by polling for the operation
        /// status at regular intervals.  This property sets a delay between
        /// the successive polls and serves as a default value if the poll
        /// delay parameter is not passed to
        /// <see cref="AdminResult.WaitForCompletionAsync"/> as well as the
        /// default for <see cref="AdminOptions.PollDelay"/>.
        /// </remarks>
        /// <value>
        /// Delay between successive polls when waiting for completion of
        /// a an admin DDL operation.  The default is 1 second.
        /// </value>
        public TimeSpan AdminPollDelay { get; set; } =
            TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets a <see cref="Consistency"/> used for read operations.
        /// </summary>
        /// <remarks>
        /// This property serves as a default value for
        /// <see cref="GetOptions.Consistency"/> and
        /// <see cref="QueryOptions.Consistency"/>.
        /// </remarks>
        /// <value>
        /// Consistency used for read operations.  The default is
        /// <see cref="SDK.Consistency.Eventual"/>.
        /// </value>
        /// <seealso cref="SDK.Consistency"/>
        public Consistency Consistency { get; set; } = Consistency.Eventual;

        /// <summary>
        /// On-Premise only.
        /// Gets or sets a <see cref="Durability"/> used for write operations.
        /// </summary>
        /// <remarks>
        /// This property serves as a default value for
        /// <see cref="PutOptions.Durability"/>,
        /// <see cref="DeleteOptions.Durability"/>,
        /// <see cref="DeleteRangeOptions.Durability"/> and
        /// <see cref="WriteManyOptions.Durability"/>.  
        /// </remarks>
        /// <value>
        /// Durability used for write operations.  If not set, the
        /// default server-side durability settings are used.
        /// </value>
        /// <seealso cref="SDK.Durability"/>
        public Durability? Durability { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount of memory in megabytes that can be
        /// used by the driver-side portion of a query.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property serves as a default for
        /// <see cref="QueryOptions.MaxMemoryMB"/>.
        /// </para>
        /// <para>
        /// The memory at the driver is needed for query operations such as
        /// duplicate elimination (which may be required if using an index
        /// on an array or a map) and sorting.  Such operations may require
        /// significant amount of memory as they need to cache full result set
        /// or a large subset of it locally. If memory consumption exceeds
        /// this value, an exception will be thrown.
        /// </para>
        /// </remarks>
        /// <value>
        /// Maximum amount of memory in MB for the execution of the
        /// driver-side portion of a query.  The default is 1024 (1 GB).
        /// </value>
        public int MaxMemoryMB { get; set; } = 1024;

        /// <summary>
        /// Cloud service only.  Gets or sets a compartment id or a
        /// compartment name for operations with this
        /// <see cref="NoSQLClient"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property serves a a default value of <c>Compartment</c>
        /// property of all operation options classes.
        /// </para>
        /// <para>
        /// If compartment name is used it can be either the name of a
        /// top-level compartment or a path to a nested compartment, e.g.
        /// <em>compartmentA.compartmentB.compartmentC</em>.  The path should
        /// not include the root compartment name (tenant).  If this property
        /// is not specified either here or for an individual operation, the
        /// tenant OCID is used as the compartment id for the operation, which
        /// is the id of the root compartment for the tenancy.  See
        /// </para>
        /// </remarks>
        /// <value>
        /// Compartment id or compartment name.  The default is the OCID of
        /// the root compartment of the tenancy.
        /// </value>
        /// <seealso href="https://docs.cloud.oracle.com/iaas/Content/GSG/Concepts/settinguptenancy.htm">
        /// Setting Up Your Tenancy
        /// </seealso>
        public string Compartment { get; set; }

        /// <summary>
        /// Gets or sets the handler for operation retries.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You may either instantiate <see cref="NoSQLRetryHandler"/> or
        /// provide your own implementation of <see cref="IRetryHandler"/>
        /// interface.  The instance of <see cref="NoSQLRetryHandler"/> may be
        /// customized with various parameters.  If this property is not set,
        /// the driver will use an instance of <see cref="NoSQLRetryHandler"/>
        /// with default parameters.
        /// </para>
        /// <para>
        /// When using JSON configuration file, you may specify an instance of
        /// <see cref="NoSQLRetryHandler"/> by providing a JSON object
        /// containing its parameters, as the example shows.
        /// </para>
        /// </remarks>
        /// <example>
        /// Specifying <see cref="NoSQLConfig.RetryHandler"/> in a JSON
        /// configuration.
        /// <code>
        /// {
        ///     "Region": "AP_HYDERABAD_1",
        ///     ...
        ///     "RetryHandler": {
        ///         "MaxRetryAttempts": 20,
        ///         "BaseDelay": 500
        ///     }
        ///     ...
        /// }
        /// </code>
        /// </example>
        /// <value>
        /// The handler for operation retries.  If not set, the default
        /// instance of <see cref="NoSQLRetryHandler"/> is used.
        /// </value>
        /// <seealso cref="IRetryHandler"/>
        /// <seealso cref="NoSQLRetryHandler"/>
        public IRetryHandler RetryHandler { get; set; }

        /// <summary>
        /// Gets or sets the authorization provider.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The type of the authorization depends on the
        /// <see cref="ServiceType"/> that you are using.  In particular:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// For Oracle NoSQL Cloud Service, set this property to an instance
        /// of <see cref="IAMAuthorizationProvider"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// For on-premise Oracle NoSQL Database in secure mode, set this
        /// property to an instance of
        /// <see cref="KVStoreAuthorizationProvider"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// For the Cloud Simulator or non-secure on-premise NoSQL Database,
        /// there is no authorization and this property should not be set.
        /// </description>
        /// </item>
        /// </list>
        /// In addition, for the cloud service and secure on-premise NoSQL
        /// database you may create and use your own implementation of
        /// <see cref="IAuthorizationProvider"/> and possibly subclass one of
        /// <see cref="IAMAuthorizationProvider"/> or
        /// <see cref="KVStoreAuthorizationProvider"/>.
        /// </para>
        /// <para>
        /// If this property is not set, the driver will use default
        /// authorization provider depending on the value of
        /// <see cref="NoSQLConfig.ServiceType"/>:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// For <see cref="SDK.ServiceType.Cloud"/>, the driver will use an
        /// instance of <see cref="IAMAuthorizationProvider"/> with default
        /// parameters.  This instance will obtain credentials stored in the
        /// default OCI configuration file with the default profile.  See
        /// <see cref="IAuthorizationProvider"/> for more information.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// For <see cref="SDK.ServiceType.CloudSim"/> no authorization is
        /// required.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// For <see cref="SDK.ServiceType.KVStore"/>, the driver will
        /// assume non-secure kvstore and thus no authorization required.  For
        /// the secure kvstore there is no default and you must provide the
        /// authorization provider.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// When using JSON configuration file, you can specify an instance of
        /// <see cref="IAMAuthorizationProvider"/> or
        /// <see cref="KVStoreAuthorizationProvider"/> by specifying a JSON
        /// object that includes the following fields:
        /// <list type="number">
        /// <item>
        /// <description>
        /// Set the field <em>AuthorizationType</em> to either <em>IAM</em> or
        /// <em>KVStore</em> respectively (the value is case-insensitive).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Set any other fields describing properties of
        /// <see cref="IAMAuthorizationProvider"/> or
        /// <see cref="KVStoreAuthorizationProvider"/>.
        /// </description>
        /// </item>
        /// </list>
        /// The example shows this for <see cref="IAMAuthorizationProvider"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// Specifying <see cref="NoSQLConfig.AuthorizationProvider"/> in JSON
        /// configuration.
        /// <code>
        /// {
        ///     "Region": "US_ASHBURN_1",
        ///     ...
        ///     "AuthorizationProvider": {
        ///         "AuthorizationType": "IAM",
        ///         "Credentials": {
        ///             "TenantId":  "ocid1.tenancy.oc...................",
        ///             "UserId": "ocid1.user.oc.....................",
        ///             ...
        ///         }
        ///     }
        ///     ...
        /// }
        /// </code>
        /// </example>
        /// <value>
        /// The authorization provider.  The default depends on the value of
        /// <see cref="NoSQLConfig.ServiceType"/> as described in the remarks
        /// section.
        /// </value>
        /// <seealso cref="IAuthorizationProvider"/>
        /// <seealso cref="IAMAuthorizationProvider"/>
        /// <seealso cref="KVStoreAuthorizationProvider"/>
        public IAuthorizationProvider AuthorizationProvider { get; set; }

        /// <summary>
        /// Gets or sets network connection options.
        /// </summary>
        /// <value>
        /// Options for network connections to Oracle NoSQL Database.
        /// </value>
        /// <seealso cref="SDK.ConnectionOptions"/>
        public ConnectionOptions ConnectionOptions { get; set; }

        /// <summary>
        /// Creates an instance of <see cref="NoSQLConfig"/> from a JSON
        /// string.
        /// </summary>
        /// <remarks>
        /// See the remarks section of <see cref="NoSQLConfig"/> for the
        /// description of the JSON format of the configuration.
        /// </remarks>
        /// <param name="jsonString">JSON representation of the configuration.
        /// </param>
        /// <returns>An instance of <see cref="NoSQLConfig"/> representing the
        /// configuration provided in <paramref name="jsonString"/>.</returns>
        /// <exception cref="ArgumentException">If failed to parse
        /// <paramref name="jsonString"/>.  The parser exception will be
        /// included as <see cref="Exception.InnerException"/> property.
        /// </exception>
        public static NoSQLConfig FromJsonString(string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<NoSQLConfig>(jsonString,
                    JsonSerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"Error parsing JSON: {ex.Message}, path: {ex.Path}",
                    ex);
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="NoSQLConfig"/> from a JSON
        /// file.
        /// </summary>
        /// <remarks>
        /// See the remarks section of <see cref="NoSQLConfig"/> for the
        /// description of the JSON format of the configuration.
        /// </remarks>
        /// <param name="jsonFilePath">The path to the file containing the
        /// JSON representation of the configuration, either as absolute path
        /// or relative to the current directory of the application.</param>
        /// <returns>An instance of <see cref="NoSQLConfig"/> representing the
        /// configuration provided by <paramref name="jsonFilePath"/>.
        /// </returns>
        /// <exception cref="ArgumentException">If failed to read the file
        /// specified by <paramref name="jsonFilePath"/> or failed to parse
        /// the JSON text.  The <see cref="Exception.InnerException"/>
        /// property will specify the cause.
        /// </exception>
        public static NoSQLConfig FromJsonFile(string jsonFilePath)
        {
            try
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                return FromJsonString(jsonString);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Error creating NoSQLConfig from file {jsonFilePath}: " +
                    ex.Message,
                    ex);
            }
        }

    }

}
