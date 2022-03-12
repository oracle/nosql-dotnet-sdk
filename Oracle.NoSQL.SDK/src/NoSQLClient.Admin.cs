/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class NoSQLClient
    {
        /// <summary>
        /// On-premise only.  Executes an administrative operation on the
        /// system.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The operations allowed are defined by Data Definition Language
        /// (DDL) portion of the query language that do not affect a specific
        /// table.For table-specific DLL operations use
        /// <see cref="NoSQLClient.ExecuteTableDDLAsync"/>.
        /// </para>
        /// <para>
        /// Examples of statements passed to this method include:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// CREATE NAMESPACE my_namespace
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// CREATE USER some_user IDENTIFIED BY password
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// CREATE ROLE some_role
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// GRANT ROLE some_role TO USER some_user
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// Some operations initiated by this API are performed by the service
        /// asynchronously and can be potentially long-running.  For these
        /// operations, getting the result returned by this API does not imply
        /// operation completion. Call
        /// <see cref="AdminResult.WaitForCompletionAsync"/> on returned
        /// <see cref="AdminResult"/> to asynchronously wait for the operation
        /// completion.  Alternatively, you may check the status of the
        /// running DDL operation by calling
        /// <see cref="GetAdminStatusAsync"/>.
        /// </para>
        /// <para>
        /// Other operations are immediate and are completed when the result
        /// from <see crer="ExecuteAdminAsync"/> is returned.  The readonly
        /// operations that don't modify system state but only return back
        /// information are immediate and will have
        /// <see cref="AdminResult.State"/> as
        /// <see cref="AdminState.Complete"/> and non-null
        /// <see cref="AdminResult.Output"/>.
        /// </para>
        /// <para>
        /// When an admin DDL operation is completed, the status of the
        /// operation should be <see cref="AdminState.Complete"/>.  To get
        /// only the final result, instead of this API, call
        /// <see cref="ExecuteAdminWithCompletionAsync"/>, which is
        /// equivalent to calling <see cref="ExecuteAdminAsync"/> and then
        /// <see cref="AdminResult.WaitForCompletionAsync"/> (you can do this
        /// regardless of the type of the admin operation because
        /// <see cref="AdminResult.WaitForCompletionAsync"/> is a no-op if the
        /// operation has already completed).
        /// </para>
        /// <para>
        /// This API takes the <paramref name="statement"/> as <c>char[]</c>
        /// because some statements will include passwords and using an array
        /// allows the application to clear the memory to avoid keeping
        /// sensitive information in memory.  For statements that don't
        /// include sensitive information, you may also use
        /// <see cref="ExecuteAdminAsync(string,AdminOptions,System.Threading.CancellationToken)"/>.
        /// </para>
        /// </remarks>
        /// <param name="statement">The statement.</param>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="AdminOptions"/>
        /// <seealso cref="AdminResult"/>
        public async Task<AdminResult> ExecuteAdminAsync(
            char[] statement,
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (AdminResult) await ExecuteRequestAsync(
                new AdminRequest(this, statement, options),
                cancellationToken);
        }

        /// <summary>
        /// On-premise only.  Executes an administrative operation on the
        /// system.
        /// </summary>
        /// <remarks>
        /// This API is the same as
        /// <see cref="ExecuteAdminAsync"/>
        /// but takes the <paramref name="statement"/> as a <c>string</c> and
        /// thus can be used if the statement does not contain sensitive
        /// information.
        /// </remarks>
        /// <param name="statement">The statement.</param>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteAdminAsync"/>
        public Task<AdminResult> ExecuteAdminAsync(
            string statement,
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAdminAsync(statement?.ToCharArray(), options,
                cancellationToken);
        }

        /// <summary>
        /// On-premise only.  Executes an administrative operation on the
        /// system and asynchronously waits for its completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="ExecuteAdminAsync"/> and then calling
        /// <see cref="AdminResult.WaitForCompletionAsync"/> on the returned
        /// <see cref="AdminResult"/> object.  If the operation is successful,
        /// the state in the resulting <see cref="AdminResult"/> object should
        /// be <see cref="AdminState.Complete"/>.
        /// </para>
        /// <para>
        /// For this operation, <see cref="AdminOptions.Timeout"/> covers
        /// the total time interval including waiting for the DDL operation
        /// completion.  If not specified, it defaults to no timeout if
        /// <see cref="NoSQLConfig.AdminPollTimeout"/> is not set or to the
        /// sum of <see cref="NoSQLConfig.AdminTimeout"/> and
        /// <see cref="NoSQLConfig.AdminPollTimeout"/> if the latter is set.
        /// Note that as with <see cref="AdminResult.WaitForCompletionAsync"/>
        /// you may specify the poll delay as
        /// <see cref="AdminOptions.PollDelay"/> which otherwise defaults
        /// to <see cref="NoSQLConfig.AdminPollDelay"/>.
        /// </para>
        /// </remarks>
        /// <param name="statement">The statement.</param>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteAdminAsync"/>
        /// <seealso cref="AdminResult.WaitForCompletionAsync"/>
        public async Task<AdminResult> ExecuteAdminWithCompletionAsync(
            char[] statement,
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var request = new AdminRequest(this, statement, options, true);
            var result = (AdminResult) await ExecuteRequestAsync(request,
                cancellationToken);
            Debug.Assert(result != null);
            var timeout = request.Options?.Timeout -
                (DateTime.Now - startTime);
            return await result.WaitForCompletionAsync(timeout,
                request.Options?.PollDelay, cancellationToken);
        }

        /// <summary>
        /// On-premise only.  Executes an administrative operation on the
        /// system and asynchronously waits for its completion.
        /// </summary>
        /// <remarks>
        /// This API is the same as
        /// <see cref="ExecuteAdminWithCompletionAsync"/>
        /// but takes the <paramref name="statement"/> as a <c>string</c> and
        /// thus can be used if the statement does not contain sensitive
        /// information.
        /// </remarks>
        /// <example>
        /// Create a namespace.  We can ignore the return value since the result
        /// would represent the final state of successful operation (or an
        /// exception will be thrown).
        /// <code>
        /// try
        /// {
        ///     await client.ExecuteAdminWithCompletionAsync(
        ///         "CREATE NAMESPACE my_namespace");
        ///     Console.WriteLine("Namespace created.");
        /// }
        /// catch(Exception e)
        /// {
        ///     Console.WriteLine($"Got exception: {e.Message}");
        /// }
        /// </code>
        /// </example>
        /// <param name="statement">The statement.</param>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteAdminWithCompletionAsync"/>
        public Task<AdminResult> ExecuteAdminWithCompletionAsync(
            string statement,
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAdminWithCompletionAsync(statement?.ToCharArray(),
                options, cancellationToken);
        }

        /// <summary>
        /// On-premise only.  Gets the status of the operation performed by
        /// <see cref="ExecuteAdminAsync"/>.
        /// </summary>
        /// <remarks>
        /// This API is used to get information about the current state of the
        /// admin DDL operation that was issued by
        /// <see cref="ExecuteAdminAsync"/> and is performed by the service
        /// asynchronously.  You do not need to use this API for the immediate
        /// operations or after calling
        /// <see cref="ExecuteAdminWithCompletionAsync"/> because the
        /// the returned <see cref="AdminResult"/> will already reflect the
        /// final completed state.
        /// </remarks>
        /// <param name="adminResult">Result returned by
        /// <see cref="ExecuteAdminAsync"/>.</param>
        /// <param name="options">(Optional) Options for this operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="GetAdminStatusOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="AdminResult"/> that reflects
        /// the current state of the operation.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="adminResult"/> is <c>null</c> or
        /// <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteAdminAsync"/>
        /// <seealso cref="AdminResult"/>
        public async Task<AdminResult> GetAdminStatusAsync(
            AdminResult adminResult,
            GetAdminStatusOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var request = new AdminStatusRequest(this, adminResult, options);

            if (adminResult != null && adminResult.OperationId == null)
            {
                if (adminResult.State != AdminState.Complete)
                {
                    throw new InvalidOperationException(
                        "Missing operation id for not completed admin result");
                }
                request.Validate();
                return adminResult;
            }

            return (AdminResult) await ExecuteRequestAsync(request,
                cancellationToken);
        }

        /// <summary>
        /// On-premise only.  Returns the users in the store as a list of
        /// <see cref="UserInfo"/> objects.
        /// </summary>
        /// <remarks>
        /// If no users are found, empty list is returned.  This operation
        /// involves performing admin DDL <em>SHOW USERS</em> command.
        /// </remarks>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning a list of <see cref="UserInfo"/> objects.
        /// </returns>
        /// <exception cref="ArgumentException">If <paramref name="options"/>
        /// contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="BadProtocolException">If received invalid output
        /// from the <em>SHOW USERS</em> command.</exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <see cref="ExecuteAdminAsync"/>
        /// <see cref="UserInfo"/>
        public async Task<IReadOnlyList<UserInfo>> ListUsersAsync(
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await ExecuteAdminListOpAsync(
                "SHOW AS JSON USERS", options, cancellationToken);
            try
            {
                if (doc == null || !doc.RootElement.TryGetProperty("users",
                    out var users))
                {
                    return new UserInfo[0];
                }

                var result = new UserInfo[users.GetArrayLength()];
                var i = 0;
                foreach (var elem in users.EnumerateArray())
                {
                    result[i++] = new UserInfo(
                        elem.GetProperty("id").GetString(),
                        elem.GetProperty("name").GetString());
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new BadProtocolException(
                    "Received invalid output for ListUsers operation",
                        ex);
            }
            finally
            {
                doc.Dispose();
            }
        }

        /// <summary>
        /// On-premise only.  Returns the namespaces in the store as a list of
        /// strings.
        /// </summary>
        /// <remarks>
        /// If no namespaces are found, empty list is returned.  This operation
        /// involves performing admin DDL <em>SHOW NAMESPACES</em> command.
        /// </remarks>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning a list of namespaces as strings.
        /// </returns>
        /// <exception cref="ArgumentException">If <paramref name="options"/>
        /// contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="BadProtocolException">If received invalid output
        /// from the <em>SHOW NAMESPACES</em> command.</exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <see cref="ExecuteAdminAsync"/>
        public async Task<IReadOnlyList<string>> ListNamespacesAsync(
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await ExecuteAdminListOpAsync(
                "SHOW AS JSON NAMESPACES", options, cancellationToken);
            try
            {
                if (doc == null || !doc.RootElement.TryGetProperty(
                    "namespaces", out var namespaces))
                {
                    return new string[0];
                }

                var result = new string[namespaces.GetArrayLength()];
                var i = 0;
                foreach (var elem in namespaces.EnumerateArray())
                {
                    result[i++] = elem.GetString();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new BadProtocolException(
                    "Received invalid output for ListNamespaces operation",
                        ex);
            }
            finally
            {
                doc.Dispose();
            }
        }

        /// <summary>
        /// On-premise only.  Returns the roles in the store as a list of
        /// strings.
        /// </summary>
        /// <remarks>
        /// If no roles are found, empty list is returned.  This operation
        /// involves performing admin DDL <em>SHOW ROLES</em> command.
        /// </remarks>
        /// <param name="options">(Optional) Options for admin DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="AdminOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning a list of namespaces as strings.
        /// </returns>
        /// <exception cref="ArgumentException">If <paramref name="options"/>
        /// contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The system is not in a
        /// valid state to perform this operation.
        /// </exception>
        /// <exception cref="BadProtocolException">If received invalid output
        /// from the <em>SHOW ROLES</em> command.</exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <see cref="ExecuteAdminAsync"/>
        public async Task<IReadOnlyList<string>> ListRolesAsync(
            AdminOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await ExecuteAdminListOpAsync(
                "SHOW AS JSON ROLES", options, cancellationToken);
            try
            {
                if (doc == null || !doc.RootElement.TryGetProperty("roles",
                    out var roles))
                {
                    return new string[0];
                }

                var result = new string[roles.GetArrayLength()];
                var i = 0;
                foreach (var elem in roles.EnumerateArray())
                {
                    result[i++] = elem.GetProperty("name").GetString();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new BadProtocolException(
                    "Received invalid output for ListRoles operation",
                        ex);
            }
            finally
            {
                doc.Dispose();
            }
        }

    }

}
