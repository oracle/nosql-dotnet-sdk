/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    /// <summary>
    /// On-premise only.  Represents the status of an admin DDL operation and
    /// related information.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the result of APIs
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
    /// and <see cref="NoSQLClient.GetAdminStatusAsync"/>.  It encapsulates
    /// the state of the admin DDL operation.
    /// </para>
    /// <para>
    /// Admin DDL operations performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
    /// can be potentially long running and not necessarily completed when
    /// this method returns result. You may call
    /// <see cref="AdminResult.WaitForCompletionAsync"/> to be notified when
    /// the operation completes.
    /// </para>
    /// <para>
    /// Alternatively you may call
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
    /// which will return the result only when the DDL operation is fully
    /// completed. You may also call
    /// <see cref="NoSQLClient.GetAdminStatusAsync"/> to
    /// receive the current status of the admin DDL operation.
    /// </para>
    /// </remarks>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
    /// <seealso cref="NoSQLClient.GetAdminStatusAsync"/>
    /// <seealso cref="AdminState"/>
    public class AdminResult
    {
        internal const AdminState UnknownAdminState = (AdminState)(-1);

        private readonly NoSQLClient client;

        internal AdminResult(NoSQLClient client)
        {
            this.client = client;
        }

        internal string OperationId { get; set; }

        // Tests should check that this value is set to correct enum value
        // upon being returned.

        /// <summary>
        /// Gets the current state of the operation.
        /// </summary>
        /// <value>
        /// The current state of the operation which is either complete or in
        /// progress.
        /// </value>
        /// <seealso cref="AdminState"/>
        public AdminState State { get; internal set; } = UnknownAdminState;

        /// <summary>
        /// Gets the statement for the operation.
        /// </summary>
        /// <value>
        /// The statement for the operation.
        /// </value>
        public string Statement { get; internal set; }

        /// <summary>
        /// Gets the output of the operation as a string.
        /// </summary>
        /// <remarks>
        /// The output is not <c>null</c> for read-only immediate operations
        /// such as <em>SHOW</em> operations and is <c>null</c> for operations
        /// that modify system state such as <em>CREATE</em>, <em>DROP</em>,
        /// <em>GRANT</em>, etc.
        /// </remarks>
        /// <value>
        /// The output of the operation for read-only operations, otherwise
        /// <c>null</c>.
        /// </value>
        public string Output { get; internal set; }

        /// <summary>
        /// Asynchronously waits for completion of admin DDL operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The wait is accomplished by polling the operation state at
        /// specified interval.  When the operation completes, the state of
        /// the operation should be <see cref="AdminState.Complete"/>.
        /// </para>
        /// <para>
        /// The result of this method is an <see cref="AdminResult"/> that
        /// represents the final state of the operation (which is the result
        /// of the last poll).  If the operation fails, this method will
        /// throw exception with the information about the operation failure.
        /// </para>
        /// <para>
        /// Note that in addition to the result returned, this calling
        /// instance is also modified to reflect the operation completion.
        /// </para>
        /// <para>
        /// You need not call this method on the result returned by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
        /// since it will already reflect the operation completion.
        /// </para>
        /// </remarks>
        /// <param name="timeout">(Optional) Timeout reflecting how long to
        /// keep polling for the operation completion.  Must be positive
        /// value. Defaults to <see cref="NoSQLConfig.AdminPollTimeout"/>
        /// if the latter is set or to no timeout if the latter is not set.
        /// </param>
        /// <param name="pollDelay">(Optional) Delay between successive polls,
        /// determines how often the polls are performed.  Must be positive
        /// value and not greater then the timeout.  Defaults to
        /// <see cref="NoSQLConfig.AdminPollDelay"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="timeout"/> or <paramref name="pollDelay"/> are
        /// invalid or <paramref name="pollDelay"/> is greater than the
        /// timeout.</exception>
        /// <exception cref="TimeoutException">Admin DDL operation has not
        /// completed within the timeout period.  May also be thrown if
        /// the service becomes unreachable due to network connectivity.
        /// </exception>
        /// <exception cref="InvalidOperationException">If the service is not
        /// in a valid state to perform the operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses may reflect the failure of the operation
        /// being polled for. See documentation for corresponding subclass of
        /// <see cref="NoSQLException"/>.</exception>
        public async Task<AdminResult> WaitForCompletionAsync(
            TimeSpan? timeout = null,
            TimeSpan? pollDelay = null,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(client != null);
            CheckPollParameters(timeout, pollDelay, nameof(timeout),
                nameof(pollDelay));

            await client.WaitForAdminCompletionAsync(this, timeout,
                pollDelay, cancellationToken);
            return this;
        }

    }

    /// <summary>
    /// On-premise only.  Represents the state of the operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
    /// or
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>.
    /// </summary>
    public enum AdminState
    {
        /// <summary>
        /// Operation is complete and successful.
        /// </summary>
        Complete,

        /// <summary>
        /// Operation is in progress.
        /// </summary>
        InProgress
    }

    /// <summary>
    /// On-premise only. Represents information associated with a user
    /// including the id and user name in the system.
    /// </summary>
    /// <seealso cref="NoSQLClient.ListUsersAsync"/>
    public readonly struct UserInfo
    {
        internal UserInfo(string id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Gets the user id.
        /// </summary>
        /// <value>
        /// The user id.
        /// </value>
        public string Id { get; }

        /// <summary>
        /// Gets the user name.
        /// </summary>
        /// <value>
        /// The user name.
        /// </value>
        public string Name { get; }
    }

}
