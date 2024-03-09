/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// NoSQLClient class provides access to Oracle NoSQL Database and can be
    /// used both with Oracle NoSQL Database Cloud Service and with On-Premise
    /// Oracle NoSQL Database.  Methods of this class are used to create and
    /// manage tables and indexes, and to read and write data.  When used
    /// on-premise, they also support admin operations such as managing
    /// namespaces, users and roles.  All operations are asynchronous and may
    /// be used with <em>async/await</em>.
    /// </summary>
    /// <example>
    /// This example instantiates <see cref="NoSQLClient"/> and uses it to
    /// create table, put record into that table and then retrieve it.
    /// <code>
    /// using Oracle.NoSQL.SDK;
    ///
    /// public class Example
    /// {
    ///     public static async Task Main()
    ///     {
    ///         // This will ensure client is disposed on exit from Main()
    ///         using var client = new NoSQLClient("config.json");
    ///
    ///         // We can also use ExecuteTableDDLWithCompletionAsync()
    ///         var tableResult = await client.ExecuteTableDDLAsync(
    ///             "CREATE TABLE foo(id INTEGER, name STRING, PRIMARY KEY(id))",
    ///             new TableLimits(100, 100, 50));
    ///
    ///         await tableResult.WaitForCompletionAsync();
    ///         Console.WriteLine("Table name: {0}, table state: {1}",
    ///             tableResult.TableName, tableResult.TableState);
    ///
    ///         var putResult = await client.PutAsync("foo", new MapValue
    ///         {
    ///             ["id"] = 1,
    ///             ["name"] = "John"
    ///         });
    ///         Console.WriteLine("Put succeeded: {0}", putResult.Success);
    ///
    ///         var getResult = await client.GetAsync("foo", new MapValue
    ///         {
    ///             ["id"] = 1
    ///         });
    ///         Console.WriteLine("Get returned record:");
    ///         // Check Row for null before converting to JSON string
    ///         Console.WriteLine(getResult.Row?.ToJsonString());
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>
    /// To instantiate NoSQLClient, provide configuration in the form
    /// of <see cref="NoSQLConfig"/> object containing connection information,
    /// credentials and default values for operation options.  Alternatively,
    /// you may provide a path to a JSON file containing this information.
    /// See <see cref="NoSQLConfig"/> for more details.
    /// </para>
    /// <para>
    /// The same interface is available to both users of Oracle NoSQL
    /// Database Cloud Service and on-premise Oracle NoSQL Database, however
    /// some methods and/or options are specific to each environment.  For
    /// each such method or option, the documentation specifies which
    /// environment it is applicable to.
    /// </para>
    /// <para>
    /// <see cref="NoSQLClient"/> has memory and network resources
    /// associated with it.  It implements <see cref="IDisposable" />
    /// interface and must be disposed when application is done using it via
    /// calling <see cref="Dispose()"/> or via <c>using</c> statement.
    /// In most cases, an application only needs to create
    /// <see cref="NoSQLClient"/> instance once and dispose of it in the end.
    /// To minimize network activity as well as resource allocation and
    /// deallocation overheads, repeated creation and disposal of
    /// <see cref="NoSQLClient"/> instances is not recommended.
    /// </para>
    /// <para>
    /// <see cref="NoSQLClient"/> permits concurrent operations, so a
    /// single instance is sufficient to access tables in a multi-threaded
    /// application. The creation of multiple instances incurs additional
    /// resource overheads without providing any performance benefit.</para>
    /// <para>
    /// With the exception of <see cref="Dispose()"/> the operations on
    /// <see cref="NoSQLClient"/> follow a similar pattern.  They accept a
    /// number of required parameters and and an optional options object
    /// providing optional parameters, with different options classes for
    /// different operations (e.g. <see cref="TableDDLOptions"/>,
    /// <see cref="PutOptions"/>, etc.).  Note that you may reuse the same
    /// options object when performing the same operation multiple times.
    /// </para>
    /// <para>
    /// If an option or any of its values are not specified or set to
    /// <c>null</c>, corresponding values set in
    /// <see cref="NoSQLConfig"/> will be used as described in documentation
    /// for each option.  For values not specified in
    /// <see cref="NoSQLConfig"/> appropriate defaults will be used.
    /// </para>
    /// <para>
    /// All operations are asynchronous and also accept optional
    /// <see cref="CancellationToken"/> parameter to cancel asynchronous
    /// operation.
    /// </para>
    /// <para>
    /// Each operation returns a <see cref="Task{TResult}"/> of
    /// corresponding  operation result, with different result classes
    /// for different operations (e.g. <see cref="TableResult"/>,
    /// <see cref="PutResult{TRow}"/> etc.)
    /// Result objects returned are not thread safe and should be used by
    /// only one thread at a time unless synchronized externally.</para>
    /// <para>Operation failures throw exceptions.  <see cref="NoSQLClient"/>
    /// may throw some standard exceptions such as
    /// <see cref="ArgumentException"/>,
    /// <see cref="InvalidOperationException"/>,
    /// <see cref="TimeoutException"/>, etc).  All other exceptions
    /// are instances of <see cref="NoSQLException"/> which serves as a base
    /// class for NoSQL Database exceptions.
    /// </para>
    /// <para>
    /// Some exceptions allow an operation to be retried, with the expectation
    /// that it may succeed on retry, because they are caused by conditions
    /// that are temporary in nature.  Examples of retryable exceptions are
    /// those which indicate resource consumption violation such as
    /// <see cref="ReadThrottlingException"/>,
    /// <see cref="WriteThrottlingException"/> and others.  In addition, any
    /// network or IO errors are always retryable as the usually indicate
    /// temporary conditions.  Other exceptions such as
    /// <see cref="ArgumentException"/>, <see cref="TableNotFoundException"/>,
    /// etc. are not retryable as they indicate a syntactic or semantic errors
    /// that require additional actions to be resolved.
    /// Exceptions that may be retried are those among the subclasses of
    /// <see cref="NoSQLException"/> and are indicated in the documentation
    /// for corresponding exception classes.  For these subclasses the
    /// property <see cref="NoSQLException.IsRetryable"/> is <c>true</c>.
    /// </para>
    /// <para>
    /// If a retryable exception is thrown, the driver will automatically
    /// retry the operation.  The retry semantics is determined by retry
    /// handler provided as <see cref="NoSQLConfig.RetryHandler"/> in the
    /// initial configuration.  If not set, a default instance of
    /// <see cref="NoSQLRetryHandler"/> will be used.  See
    /// <see cref="IRetryHandler"/> and <see cref="NoSQLRetryHandler"/> for
    /// details on retry behavior.  This means that a result returned by an
    /// operation may be a result obtained after one or more retries (in case
    /// the initial attempt and previous retries have failed with retryable
    /// exceptions).
    /// </para>
    /// <para>
    /// When operation is retried by the driver, the operation timeout is
    /// considered cumulative over all retries (and not just for a single
    /// retry).  The driver will keep retrying as long as retries are
    /// allowed by the retry handler and are within the specified timeout.
    /// <see cref="TimeoutException"/> is thrown once the timeout expires.
    /// <see cref="TimeoutException"/> may also be thrown even on a
    /// non-retryable operation (a single request) if the operation times out
    /// for other reasons such as network connectivity.
    /// If you are getting <see cref="TimeoutException"/> often and the
    /// service is operating properly, you may wish to adjust corresponding
    /// timeout values passed in the operation options or specified in
    /// <see cref="NoSQLConfig"/>.
    /// </para>
    /// <para><see cref="TimeoutException"/> itself is not retryable
    /// automatically by the driver, although an application may choose to
    /// retry the operation.  Note however that in general,
    /// <see cref="TimeoutException"/> only indicates inability for the driver
    /// to receive operation result within the specified timeout and thus it
    /// does not determine whether the operation itself was already
    /// started/completed by the service.  Thus for non-idempotent operations,
    /// additional actions may need to be taken before retrying in this
    /// situation.
    /// </para>
    /// <para>
    /// Likewise, you may cancel operations on <see cref="NoSQLClient"/>
    /// instance for which you provided <see cref="CancellationToken"/>
    /// argument.  In this case the operation may throw
    /// <see cref="OperationCanceledException"/> (or its subclass
    /// <see cref="TaskCanceledException"/>).  As with
    /// <see cref="TimeoutException"/>, catching this exception does not
    /// provide any guarantees as to whether the operation was performed
    /// by the service.
    ///
    /// </para>
    /// <para>
    /// Instances of <see cref="NoSQLClient"/> are thread-safe and
    /// expected to be shared among threads.
    /// </para>
    /// <para>
    /// <em>For cloud service only:</em> Each of the options classes
    /// contains <em>Compartment</em> property which specifies the compartment
    /// of given table (or compartment used to perform given operation).  A
    /// default value to use for all operations may also be set as
    /// <see cref="NoSQLConfig.Compartment"/>.  If neither is set, the root
    /// compartment of the tenancy is assumed.  The compartment is a string
    /// that may be either the id (OCID) of the compartment or a compartment
    /// name.  Both are acceptable.  If a name is used it can be either
    /// the name of a top-level compartment, or for nested compartments, it
    /// should be a compartment path to the nested compartment that does not
    /// include the root compartment name (tenant), e.g.
    /// <em>compartmentA.compartmentB.compartmentC</em>.  Alternatively,
    /// instead of using <em>Compartment</em> property, you may prefix the
    /// table name with its compartment name (for top-level compartments) or
    /// compartment path (for nested compartments), e.g.
    /// <em>compartmentA.compartmentB.compartmentC:myTable</em>.  Note that
    /// the table name cannot be prefixed with compartment id.  Prefixing
    /// the table with compartment name/path takes precedence over other
    /// methods of specifying the compartment.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLConfig"/>
    /// <seealso cref="NoSQLException"/>
    /// <seealso cref="NoSQLRetryHandler"/>
    public partial class NoSQLClient: IDisposable
    {
        private void Init(NoSQLConfig config)
        {
            Config = config;
            Config.Init();
            ProtocolHandler = new ProtocolHandler();
            client = new Http.Client(Config, ProtocolHandler);
            if (RateLimitingHandler.IsRateLimitingEnabled(config))
            {
                RateLimitingHandler = new RateLimitingHandler(this);
            }
        }

        /// <summary>
        /// Initializes new instance of <see cref="NoSQLClient"/> using
        /// provided configuration object.  You may omit
        /// </summary>
        /// <remarks>
        /// <para>
        /// You may omit <paramref name="config"/> parameter or pass
        /// <c>null</c> if using the cloud service with the default OCI
        /// configuration file that contains credentials and region
        /// identifier.  In this case, a default <see cref="NoSQLConfig"/>
        /// instance will be created.  In all other cases, a valid
        /// <see cref="NoSQLConfig"/> object must be provided.
        /// See <see cref="NoSQLConfig()"/> for more details.
        /// </para>
        /// <para>
        /// Note that <see cref="NoSQLConfig"/> will be copied when
        /// creating <see cref="NoSQLClient"/> instance, so that the
        /// modifications to <see cref="NoSQLConfig"/> will have no effect on
        /// existing <see cref="NoSQLClient"/> instances which are immutable.
        /// </para>
        /// </remarks>
        /// <param name="config">(Optional) Client configuration object.
        /// </param>
        /// <exception cref="ArgumentException">If <paramref name="config"/>
        /// is invalid or inconsistent, or <paramref name="config"/> is not
        /// provided and default <see cref="NoSQLConfig"/> instance cannot be
        /// used as described above.</exception>
        /// <seealso cref="NoSQLConfig"/>
        public NoSQLClient(NoSQLConfig config = null)
        {
            Init(config != null ? config.Clone() : new NoSQLConfig
            {
                // Case of default OCI config file with default profile.
                ServiceType = ServiceType.Cloud
            });
        }

        /// <summary>
        /// Initializes new instance of <see cref="NoSQLClient"/> using
        /// provided path to JSON configuration file.
        /// </summary>
        /// <remarks>This is equivalent to
        /// using <see cref="NoSQLClient(NoSQLConfig)"/> constructor and
        /// passing the result of
        /// <see cref="NoSQLConfig.FromJsonFile(string)"/> as parameter.
        /// </remarks>
        /// <param name="configFile">Path to JSON configuration file as either
        /// absolute path or relative to the current directory of the
        /// application.</param>
        /// <exception cref="ArgumentException">If failed to parse provided
        /// <paramref name="configFile"/> (file is not found, cannot be read,
        /// does not contain valid JSON or valid data to instantiate
        /// <see cref="NoSQLConfig"/>) or if resulting
        /// <see cref="NoSQLConfig"/> is invalid or inconsistent.</exception>
        /// <seealso cref="NoSQLConfig"/>
        public NoSQLClient(string configFile)
        {
            Init(NoSQLConfig.FromJsonFile(configFile));
        }

        /// <summary>
        /// Releases the unmanaged resources used by this instance and
        /// optionally releases the managed resources.
        /// </summary>
        /// <remarks>
        /// This method is called by the public <see cref="Dispose()"/> method
        /// and the <see cref="Object.Finalize"/> method.
        /// </remarks>
        /// <param name="disposing"><c>true</c> to release both managed and
        /// unmanaged resources, <c>false</c> to release only unmanaged
        /// resources.</param>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">
        /// Implementing a Dispose Method
        /// </seealso>
        protected void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                client.Dispose();
                Config.ReleaseResources();
                RateLimitingHandler?.Dispose();
                disposed = true;
            }
        }

        /// <summary>
        /// Releases resources used by this <see cref="NoSQLClient"/>
        /// instance.
        /// </summary>
        /// <remarks>
        /// After this method is called, <see cref="NoSQLClient"/>
        /// can no longer be used.  Applications must dispose of
        /// <see cref="NoSQLClient"/> instance when done with it by calling
        /// this method or via <c>using</c> statement.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the service type used by this <see cref="NoSQLClient"/>
        /// instance.
        /// </summary>
        /// <remarks>
        /// This is the service type that was specified (or
        /// implicitly derived) in <see cref="NoSQLConfig"/> object used to
        /// create this instance.
        /// </remarks>
        /// <value>Service type.</value>
        public ServiceType ServiceType => Config.ServiceType;

    }

}
