/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class NoSQLClient
    {
        /// <summary>
        /// Gets the row associated with a primary key.
        /// </summary>
        /// <remarks>
        /// On success the value of the row is available as
        /// <see cref="GetResult{TRow}.Row"/> property. If matching row
        /// does not exist, the operation is still successful and
        /// <see cref="GetResult{TRow}.Row"/> property will be set to
        /// <c>null</c>.
        /// </remarks>
        /// <example>
        /// Executing Get operation on table with schema MyTable(id LONG,
        /// name STRING, PRIMARY KEY(id)).
        /// <code>
        /// var result = await client.GetAsync(
        ///     "MyTable",
        ///     new MapValue
        ///     {
        ///         ["id"] = 1000
        ///     });
        ///
        /// if (result.Row != null)
        /// {
        ///     // result.Row.ToString() will produce JSON string:
        ///     // { id: 1000, name: "John" }
        ///     Console.WriteLine(result.Row);
        /// }
        /// </code>
        /// </example>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="primaryKey">Primary key of the row as
        /// <see cref="MapValue"/> representing names and values of the
        /// primary key fields.</param>
        /// <param name="options">(Optional) Options for the Get operation.
        /// If not specified or <c>null</c>, appropriate defaults will be
        /// used.  See <see cref="GetOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="GetResult{TRow}"/> of
        /// <see cref="RecordValue"/>.  If there is no matching row,
        /// <see cref="GetResult{TRow}.Row"/> is <c>null</c>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="primaryKey"/> is <c>null</c> or invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetOptions"/>
        /// <seealso cref="GetResult{TRow}"/>
        public Task<GetResult<RecordValue>> GetAsync(
            string tableName,
            MapValue primaryKey,
            GetOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetAsync<RecordValue>(tableName, primaryKey, options,
                cancellationToken);
        }

        /// <summary>
        /// Puts a row into a table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a new row or overwrites an existing row
        /// entirely.  The value used for the put must contain a complete
        /// primary key and all required fields.
        /// </para>
        /// <para>
        /// It is not possible to put part of a row.  Any fields that are not
        /// provided will be defaulted, overwriting any existing value. Fields
        /// that are not nullable or defaulted must be provided or the
        /// operation will fail.
        /// </para>
        /// <para>
        /// By default the Put operation is unconditional, but Put
        /// operations can also be conditional based on existence, or not, of
        /// a previous value as well as conditional on the
        /// <see cref="RowVersion"/> of the existing value.  You may specify
        /// conditional Put via <see cref="PutOptions"/> as follows:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Use <see cref="PutOptions.IfAbsent"/> to do a put only if there is
        /// no existing row that matches the primary key.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Use <see cref="PutOptions.IfPresent"/> to do a put only if there
        /// is an existing row that matches the primary key.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Use <see cref="PutOptions.MatchVersion"/> to do a put only if
        /// there is an existing row that matches the primary key <em>and</em>
        /// its <see cref="RowVersion"/> matches that provided.
        /// </description>
        /// </item>
        /// </list>
        /// Note that the options listed above are mutually exclusive in that
        /// specify one will unset the others.  Instead of specifying these
        /// options you may also use additional APIs provided as shorthands
        /// for conditional Put operations such as
        /// <see cref="NoSQLClient.PutIfAbsentAsync"/>,
        /// <see cref="NoSQLClient.PutIfPresentAsync"/> and
        /// <see cref="NoSQLClient.PutIfVersionAsync"/>.
        /// </para>
        /// <para>
        /// It is also possible, on failure, to return information about the
        /// existing row.  The row and it's <see cref="RowVersion"/> can be
        /// optionally returned as part of <see cref="PutResult{TRow}"/> if a
        /// put operation fails because of a version mismatch or if the
        /// operation fails because the row already exists. The existing row
        /// information will only be returned if
        /// <see cref="PutOptions.ReturnExisting"/> is <c>true</c> and one of
        /// the following occurs:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="PutOptions.IfAbsent"/> is <c>true</c> and the operation
        /// fails because the row already exists.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="PutOptions.MatchVersion"/> is set and the operation
        /// fails because the row exists and its <see cref="RowVersion"/> does
        /// not match the one provided.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// Use of <see cref="PutOptions.ReturnExisting"/> may result in
        /// additional consumed read capacity and may affect the operation's
        /// latency.  If the operation is successful there will be no
        /// information returned about the previous row.
        /// </para>
        /// <para>
        /// Note that the failures of conditional Put operations discussed
        /// above will not result in an exception and the
        /// <see cref="PutResult{TRow}"/> will still be returned.
        /// </para>
        /// </remarks>
        /// <example>
        /// Executing Put operation on table with schema MyTable(id LONG,
        /// name STRING, PRIMARY KEY(id)).
        /// <code>
        /// var result = await client.PutAsync(
        ///     "MyTable",
        ///     new MapValue
        ///     {
        ///         ["id"] = 1000,
        ///         ["name"] = "John"
        ///     });
        ///
        /// // Will output true because unconditional Put operation is
        /// // always successful unless an exception is thrown.
        /// Console.WriteLine(result.Success);
        /// </code>
        /// </example>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="row">Table row as <see cref="MapValue"/> representing
        /// names and values of the fields.  Using <see cref="RecordValue"/>
        /// is not required as the field order doesn't matter for this
        /// operation.</param>
        /// <param name="options">(Optional) Options for the Put operation.
        /// If not specified or <c>null</c>, appropriate defaults will be
        /// used.  See <see cref="PutOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="PutResult{TRow}"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="row"/> is <c>null</c> or invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="PutOptions"/>
        /// <seealso cref="PutResult{TRow}"/>
        /// <seealso cref="PutIfAbsentAsync"/>
        /// <seealso cref="PutIfPresentAsync"/>
        /// <seealso cref="PutIfVersionAsync"/>
        public async Task<PutResult<RecordValue>> PutAsync(
            string tableName,
            MapValue row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<RecordValue>) await ExecuteRequestAsync(
                new PutRequest<RecordValue>(this, tableName, row, options),
                cancellationToken);
        }

        /// <summary>
        /// Puts a row into a table if there is no existing row that matches
        /// the primary key.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for <see cref="NoSQLClient.PutAsync"/> with
        /// <see cref="PutOptions.IfAbsent"/> set to <c>true</c>.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="row">Table row.</param>
        /// <param name="options">(Optional) Options for the Put operation.
        /// Note that the values of <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> and
        /// <see cref="PutOptions.MatchVersion"/> are not used for this API.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="PutResult{TRow}"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="row"/> is <c>null</c> or invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="PutAsync"/>
        public async Task<PutResult<RecordValue>> PutIfAbsentAsync(
            string tableName,
            MapValue row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<RecordValue>) await ExecuteRequestAsync(
                new PutIfAbsentRequest<RecordValue>(this, tableName, row,
                    options), cancellationToken);
        }

        /// <summary>
        /// Puts a row into a table if there is an existing row that matches
        /// the primary key.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for <see cref="NoSQLClient.PutAsync"/> with
        /// <see cref="PutOptions.IfPresent"/> set to <c>true</c>.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="row">Table row.</param>
        /// <param name="options">(Optional) Options for the Put operation.
        /// Note that the values of <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> and
        /// <see cref="PutOptions.MatchVersion"/> are not used for this API.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="PutResult{TRow}"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="row"/> is <c>null</c> or invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="PutAsync"/>
        public async Task<PutResult<RecordValue>> PutIfPresentAsync(
            string tableName,
            MapValue row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<RecordValue>) await ExecuteRequestAsync(
                new PutIfPresentRequest<RecordValue>(this, tableName, row,
                    options), cancellationToken);
        }

        /// <summary>
        /// Puts a row into a table if there is an existing row that matches
        /// the primary key and its <see cref="RowVersion"/> matches the
        /// provided value.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for <see cref="NoSQLClient.PutAsync"/> with
        /// <see cref="PutOptions.MatchVersion"/> specified.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="row">Table row.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="options">(Optional) Options for the Put operation.
        /// Note that the values of <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> and
        /// <see cref="PutOptions.MatchVersion"/> are not used for this API.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="PutResult{TRow}"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="row"/> is <c>null</c> or invalid or
        /// <paramref name="version"/> is <c>null</c> or invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="PutAsync"/>
        public async Task<PutResult<RecordValue>> PutIfVersionAsync(
            string tableName,
            MapValue row,
            RowVersion version,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<RecordValue>) await ExecuteRequestAsync(
                new PutIfVersionRequest<RecordValue>(this, tableName, row,
                    version, options), cancellationToken);
        }

        /// <summary>
        /// Deletes a row from a table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The row is identified using a primary key value.
        /// </para>
        /// <para>
        /// By default a delete operation is unconditional and will succeed if
        /// the specified row exists.  Delete operations can be made
        /// conditional based on whether the <see cref="RowVersion"/> of an
        /// existing row matches that supplied by
        /// <see cref="DeleteOptions.MatchVersion"/>.  Instead of using
        /// <see cref="DeleteOptions.MatchVersion"/> you may also use
        /// <see cref="NoSQLClient.DeleteIfVersionAsync"/> API to perform the
        /// same conditional operation.
        /// </para>
        /// <para>
        /// It is also possible, on failure, to return information about the
        /// existing row.  The row and its version can be optionally returned
        /// as part of <see cref="DeleteResult{TRow}"/> if a delete operation
        /// fails because of a version mismatch.The existing row information
        /// will only be returned if
        /// <see cref="DeleteOptions.ReturnExisting"/> is <c>true</c> and
        /// <see cref="DeleteOptions.MatchVersion"/> is set and the operation
        /// fails because the row exists and its <see cref="RowVersion"/> does
        /// not match the one provided.
        /// </para>
        /// <para>
        /// Use of <see cref="DeleteOptions.ReturnExisting"/> may result in
        /// additional consumed read capacity and may affect the operation's
        /// latency.  If the operation is successful there will be no
        /// information returned about the deleted row.
        /// </para>
        /// <para>
        /// Note that the failures of conditional Delete operation as
        /// discussed above will not result in an exception and the
        /// <see cref="DeleteResult{TRow}"/> will still be returned.
        /// </para>
        /// </remarks>
        /// <example>
        /// Executing Delete operation on table with schema MyTable(id LONG,
        /// name STRING, PRIMARY KEY(id)).
        /// <code>
        /// var result = await client.DeleteAsync(
        ///     "MyTable",
        ///     new MapValue
        ///     {
        ///         ["id"] = 1000
        ///     });
        ///
        /// // true if the delete succeeded, false if the row was not found
        /// Console.WriteLine(result.Success);
        /// </code>
        /// </example>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="primaryKey">Primary key of the row as
        /// <see cref="MapValue"/> representing names and values of the
        /// primary key fields.</param>
        /// <param name="options">(Optional) Options for the Delete operation.
        /// If not specified or <c>null</c>, appropriate defaults will be
        /// used.  See <see cref="DeleteOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="DeleteResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="primaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DeleteOptions"/>
        /// <seealso cref="DeleteResult{TRow}"/>
        /// <seealso cref="DeleteIfVersionAsync"/>
        public Task<DeleteResult<RecordValue>> DeleteAsync(
            string tableName,
            MapValue primaryKey,
            DeleteOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DeleteAsync<RecordValue>(tableName, primaryKey, options,
                cancellationToken);
        }

        /// <summary>
        /// Deletes a row from a table if there is an existing row that
        /// matches the primary key and its <see cref="RowVersion"/> matches
        /// the provided value.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for <see cref="NoSQLClient.DeleteAsync"/>
        /// with <see cref="DeleteOptions.MatchVersion"/> specified.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="primaryKey">Primary key of the row.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="options">(Optional) Options for the Delete operation.
        /// Note that the value of <see cref="DeleteOptions.MatchVersion"/> is
        /// not used for this API.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="DeleteResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="primaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="version"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="NoSQLClient.DeleteAsync"/>
        public Task<DeleteResult<RecordValue>> DeleteIfVersionAsync(
            string tableName,
            MapValue primaryKey,
            RowVersion version,
            DeleteOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DeleteIfVersionAsync<RecordValue>(tableName, primaryKey,
                version, options, cancellationToken);
        }

        /// <summary>
        /// Deletes multiple rows from a table in an atomic operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation deletes multiple rows from a table residing on the
        /// same shard in an atomic operation.  A range of rows is specified
        /// using a partial primary key plus a field range based on the
        /// portion of the key that is not provided.The partial primary key
        /// must contain all of the fields that are in the shard key.  For
        /// example if a table's primary key is &lt;id, timestamp&gt; and its
        /// shard key is the id, it is possible to delete a range of timestamp
        /// values for a specific id by providing a key with an id value but
        /// no timestamp value and providing a range of timestamp values as
        /// <see cref="DeleteRangeOptions.FieldRange"/>.  If the field range
        /// is not provided, the operation will delete all rows matching the
        /// partial primary key.
        /// </para>
        /// <para>
        /// Because this operation can exceed the maximum amount of data
        /// modified in a single operation as specified by
        /// <see cref="DeleteRangeOptions.MaxWriteKB"/> or the system limit,
        /// it is possible that it will delete only part of the range of rows
        /// and a continuation key (provided as
        /// <see cref="DeleteRangeResult.ContinuationKey"/>) can be used to
        /// continue the operation.  In this case, call
        /// <see cref="NoSQLClient.DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// in a loop until
        /// <see cref="DeleteRangeResult.ContinuationKey"/> is <c>null</c> as
        /// shown in the example below.
        /// Alternatively, you can call
        /// <see cref="GetDeleteRangeAsyncEnumerable(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// and iterate over resulting <see cref="IAsyncEnumerable{T}"/>.
        /// </para>
        /// <para>
        /// Note that when the DeleteRange operation requires multiple calls
        /// with the continuation key or iteration with
        /// <see cref="GetDeleteRangeAsyncEnumerable(string,object,DeleteRangeOptions,CancellationToken)"/>,
        /// the operation is no longer atomic as a whole, although each call
        /// to <see cref="DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// and each iteration of the loop over
        /// <see cref="GetDeleteRangeAsyncEnumerable(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// is still atomic.
        /// </para>
        /// </remarks>
        /// <example>
        /// Executing DeleteRange operation on table with schema myTable(
        /// storeId LONG, itemId LONG, itemName STRING, PRIMARY KEY(
        /// SHARD(storeId), itemId)).  This operation will delete all items
        /// for a given store because we do not provide the field range.  This
        /// operation is performed in a loop using the continuation key to
        /// allow deleting large quantity of the table rows.
        /// <code>
        ///     var partialPrimaryKey = new MapValue
        ///     {
        ///         ["storeId"] = 50
        ///     };
        ///     var options = new DeleteRangeOptions();
        ///     do
        ///     {
        ///         var result = await client.DeleteRangeAsync("myTable",
        ///             partialPrimaryKey, options);
        ///         Console.WriteLine($"Deleted {result.DeletedCount} row(s)");
        ///         options.ContinuationKey = result.ContinuationKey;
        ///     }
        ///     while(options.ContinuationKey != null);
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="options">(Optional) Options for the DeleteRange
        /// operation.  If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="DeleteRangeOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="DeleteRangeResult"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DeleteRangeOptions"/>
        /// <seealso cref="DeleteRangeResult"/>
        /// <seealso cref="GetDeleteRangeAsyncEnumerable(string,object,DeleteRangeOptions,CancellationToken)"/>
        public async Task<DeleteRangeResult> DeleteRangeAsync(
            string tableName,
            object partialPrimaryKey,
            DeleteRangeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (DeleteRangeResult) await ExecuteRequestAsync(
                new DeleteRangeRequest(this, tableName, partialPrimaryKey,
                    options), cancellationToken);
        }

        /// <summary>
        /// Deletes multiple rows from a table in an atomic operation.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="NoSQLClient.DeleteRangeAsync(string, object, DeleteRangeOptions, CancellationToken)"/>
        /// that takes <paramref name="fieldRange"/> instead of the options
        /// and can be used when no other options are required.
        /// </remarks>
        /// <example>
        /// Executing DeleteRange operation on table with schema myTable(
        /// storeId LONG, itemId LONG, itemName STRING, PRIMARY KEY(
        /// SHARD(storeId), itemId)).  This operation will delete a range of
        /// items in a given store which should not be large.
        /// <code>
        ///     var result = await client.DeleteRangeAsync(
        ///         "myTable",
        ///         new MapValue // partial primary key
        ///         {
        ///             ["storeId"] = 50
        ///         },
        ///         new FieldRange("itemId")
        ///         {
        ///             StartsWith = 1000,
        ///             EndsBefore = 1010
        ///         });
        ///
        ///     Console.WriteLine($"Deleted {result.DeletedCount} row(s)");
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="fieldRange">Field range for the DeleteRange
        /// operation, see <see cref="FieldRange"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="DeleteRangeResult"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="fieldRange"/> is invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="NoSQLClient.DeleteRangeAsync(string, object, DeleteRangeOptions, CancellationToken)"/>
        public async Task<DeleteRangeResult> DeleteRangeAsync(
            string tableName,
            object partialPrimaryKey,
            FieldRange fieldRange,
            CancellationToken cancellationToken = default)
        {
            return (DeleteRangeResult) await ExecuteRequestAsync(
                new DeleteRangeRequest(this, tableName, partialPrimaryKey,
                    fieldRange), cancellationToken);
        }

        /// <summary>
        /// Deletes multiple rows from a table in an atomic operation.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="NoSQLClient.DeleteRangeAsync(string, object, DeleteRangeOptions, CancellationToken)"/>.
        /// Use this overload when you need to provide
        /// <see cref="CancellationToken"/> but you don't need to provide
        /// <see cref="DeleteRangeOptions"/>.
        /// </remarks>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="DeleteRangeResult"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="NoSQLClient.DeleteRangeAsync(string, object, DeleteRangeOptions, CancellationToken)"/>
        public Task<DeleteRangeResult> DeleteRangeAsync(
            string tableName,
            object partialPrimaryKey,
            CancellationToken cancellationToken)
        {
            return DeleteRangeAsync(tableName, partialPrimaryKey,
                (DeleteRangeOptions)null, cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to delete range of rows
        /// from a table in multiple successive atomic operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this API when the number of rows to delete is large and they
        /// cannot be deleted in one atomic operation because of the
        /// limitation on the maximum amount of data modified as specified by
        /// <see cref="DeleteRangeOptions.MaxWriteKB"/> or the system limit.
        /// </para>
        /// <para>
        /// This API is similar to
        /// <see cref="DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// but creates <see cref="IAsyncEnumerable{T}"/> that allows you to
        /// delete range of rows in multiple successive atomic operations
        /// using <c>await foreach</c> construct, when the DeleteRange
        /// operation cannot be performed in one atomic operation because of
        /// the limitations discussed above.  Each iteration is equivalent to
        /// calling
        /// <see cref="DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// with a continuation key
        /// and returns <see cref="DeleteRangeResult"/> containing the number
        /// of deleted rows during this iteration.  The quantity of rows
        /// deleted during each iteration is thus determined in the same way
        /// as for <see cref="DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>,
        /// using either <see cref="DeleteRangeOptions.MaxWriteKB"/> or
        /// the system default.
        /// </para>
        /// <para>
        /// Note that this method itself may only throw
        /// <see cref="ArgumentException"/>.  Other exceptions listed can only
        /// be thrown during the iteration process as per deferred execution
        /// semantics of enumerables.
        /// </para>
        /// </remarks>
        /// <example>
        /// Asynchronously iterating over
        /// <see cref="GetDeleteRangeAsyncEnumerable(string,object,DeleteRangeOptions,CancellationToken)"/>.
        /// <code>
        /// var partialPrimaryKey =
        /// var options = new DeleteRangeOptions
        /// {
        ///     Compartment = "my_compartment",
        ///     FieldRange = new FieldRange("itemId")
        ///     {
        ///         StartAfter = 1000
        ///     }
        /// };
        ///
        /// await foreach(var result in client.GetDeleteRangeAsyncEnumerable(
        ///     "myTable", partialPrimaryKey, options))
        /// {
        ///     Console.WriteLine($"Deleted {result.DeletedCount} row(s)");
        /// }
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="options">(Optional) Options for the DeleteRange
        /// operation.  If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="DeleteRangeOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="DeleteRangeResult"/> objects.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="NoSQLClient.DeleteRangeAsync(string,object,DeleteRangeOptions,CancellationToken)"/>
        /// <seealso cref="DeleteRangeOptions"/>
        /// <seealso cref="DeleteRangeResult"/>
        /// <seealso href="https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8">
        /// Iterating with Async Enumerables in C#
        /// </seealso>
        public IAsyncEnumerable<DeleteRangeResult>
            GetDeleteRangeAsyncEnumerable(
            string tableName,
            object partialPrimaryKey,
            DeleteRangeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetDeleteRangeAsyncEnumerableWithOptions(tableName,
                partialPrimaryKey,
                options != null ? options.Clone() : new DeleteRangeOptions(),
                cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to delete range of rows
        /// from a table in multiple successive atomic operations.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="GetDeleteRangeAsyncEnumerable(string, object, DeleteRangeOptions, CancellationToken)"/>
        /// that takes <paramref name="fieldRange"/> instead of the options
        /// and can be used when no other options are required.
        /// </remarks>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="fieldRange">Field range for the DeleteRange
        /// operation, see <see cref="FieldRange"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="DeleteRangeResult"/> objects.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid or
        /// <paramref name="fieldRange"/> is invalid.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetDeleteRangeAsyncEnumerable(string, object, DeleteRangeOptions, CancellationToken)"/>
        public IAsyncEnumerable<DeleteRangeResult>
            GetDeleteRangeAsyncEnumerable(
            string tableName,
            object partialPrimaryKey,
            FieldRange fieldRange,
            CancellationToken cancellationToken = default)
        {
            return GetDeleteRangeAsyncEnumerableWithOptions(tableName,
                partialPrimaryKey, new DeleteRangeOptions
                {
                    FieldRange = fieldRange
                }, cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to delete range of rows
        /// from a table in multiple successive atomic operations.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="GetDeleteRangeAsyncEnumerable(string, object, DeleteRangeOptions, CancellationToken)"/>.
        /// Use this overload when you need to provide
        /// <see cref="CancellationToken"/> but you don't need to provide
        /// <see cref="DeleteRangeOptions"/>.
        /// </remarks>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="partialPrimaryKey">Partial primary key.  Currently
        /// must be provided as <see cref="MapValue"/> representing names and
        /// values of the partial primary key fields.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="DeleteRangeResult"/>.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="partialPrimaryKey"/> is <c>null</c> or invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetDeleteRangeAsyncEnumerable(string, object, DeleteRangeOptions, CancellationToken)"/>
        public IAsyncEnumerable<DeleteRangeResult>
            GetDeleteRangeAsyncEnumerable(
            string tableName,
            object partialPrimaryKey,
            CancellationToken cancellationToken)
        {
            return GetDeleteRangeAsyncEnumerable(tableName, partialPrimaryKey,
                (DeleteRangeOptions)null, cancellationToken);
        }

        /// <summary>
        /// Atomically executes a sequence of Put and Delete operations on one
        /// or more tables that share the same shard key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method executes a sequence of put and delete operations
        /// associated with one or more tables that share the same
        /// <em>shard key</em> portion of their primary keys.  All the
        /// specified operations are executed within the scope of a single
        /// transaction, thus making the operation atomic.  It is an
        /// efficient way to atomically modify multiple related rows.
        /// </para>
        /// <para>
        /// You can issue operations for multiple tables as long as these
        /// tables have the same shard key.  This means that these tables must
        /// be part of the same parent/child table hierarchy that has a single
        /// ancestor table specifying the shard key. Thus you may include
        /// operations for this ancestor table and/or any of its descendants
        /// (for example, parent and child tables, sibling tables, etc.).  All
        /// operations must be on rows that share the same shard key value.
        /// </para>
        /// <para>
        /// There are some size-based limitations on this operation:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// The max number of individual operations (Put, Delete) in a single
        /// call to this API is 50.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// The total request size is limited to 25 MB.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// The sequence of Put and Delete operations to execute is specified
        /// as <see cref="WriteOperationCollection"/>.  Each operation must be
        /// on a distinct row in the table (you may not have multiple
        /// operations on the same table that share the same primary key).
        /// When you add an operation to the collection, you may also specify
        /// its <see cref="PutOptions"/> or <see cref="DeleteOptions"/>.  You
        /// may share the same options object between different operations in
        /// the collection.  You may not specify <em>Compartment</em>,
        /// <em>Timeout</em> or <em>Durability</em> properties in the options
        /// added to the collection, these can only be specified as part of
        /// <see cref="WriteManyOptions"/>.
        /// </para>
        /// <para>
        /// Using this method requires specifying table name for each
        /// operation.  Thus, to add operations to
        /// <see cref="WriteOperationCollection"/> you must use methods of
        /// <see cref="WriteOperationCollection"/> that take table name as a
        /// parameter.  See <see cref="WriteOperationCollection"/> for
        /// details.
        /// </para>
        /// <para>
        /// <em>AbortIfUnsuccessful</em> parameter specifies whether the whole
        /// transaction should be aborted if a particular operation fails.
        /// You may specify this parameter for any operation added to the
        /// <see cref="WriteOperationCollection"/>.  In addition, if
        /// <see cref="WriteManyOptions.AbortIfUnsuccessful"/> is set to
        /// <c>true</c>, this will enable this option for all operations in
        /// the collection.  Note that the success or failure of an operation
        /// is defined here in the same way as indicated by
        /// <see cref="PutResult{TRow}.Success"/> and
        /// <see cref="DeleteResult{TRow}.Success"/>, that is the failure
        /// would occur if conditional Put operation fails or conditional
        /// Delete operation fails or Delete operation fails because the given
        /// row does not exist (other failures will cause an exception to be
        /// thrown and may also abort the transaction regardless of
        /// <em>AbortIfUnsuccessful</em> setting).
        /// </para>
        /// <para>
        /// On successful completion of this API,
        /// <see cref="WriteManyResult{TRow}.Results"/> will contain a list of
        /// the execution results of all operations.  If the transaction was
        /// aborted because of failure of an operation which has
        /// <see cref="IWriteOperation.AbortIfUnsuccessful"/> set to
        /// <c>true</c> (or if
        /// <see cref="WriteManyOptions.AbortIfUnsuccessful"/> is set to
        /// <c>true</c>) then the index and execution result of the failed
        /// operation will be available as
        /// <see cref="WriteManyResult{TRow}.FailedOperationIndex"/> and
        /// <see cref="WriteManyResult{TRow}.FailedOperationResult"/>
        /// respectively.
        /// </para>
        /// <para>
        /// If all operations are for single table, you may also use
        /// <see cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        /// method. If, in addition, the operations are all Put or all Delete
        /// and it is sufficient to specify the same options for all
        /// operations (instead of on per-operation basis), you may also opt
        /// to use simpler APIs <see cref="PutManyAsync"/> and
        /// <see cref="DeleteManyAsync"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// Performing WriteMany operation with parent table "emp" and
        /// child table "emp.expenses".
        /// <code>
        /// var row = new MapValue
        /// {
        ///     ["id"] = 1000,
        ///     ["name"] = "Jane"
        /// };
        ///
        /// var childRow = new MapValue
        /// {
        ///     ["id"] = 1001,
        ///     ["name"] = "Jane",
        ///     ["expenseId"] = 100001,
        ///     ["expenseTitle"] = "Books"
        /// };
        /// var primaryKey = new MapValue
        /// {
        ///     ["id"] = 2000
        /// };
        ///
        /// var result = await client.WriteManyAsync(
        ///     new WriteOperationCollection()
        ///         .AddPut("emp", row)
        ///         .AddPutIfAbsent("emp.expenses", childRow, true)
        ///         .AddDelete("emp", primaryKey, true));
        /// </code>
        /// </example>
        /// <param name="operations">Collection of Put and Delete operations
        /// to execute in a single transaction.</param>
        /// <param name="options">(Optional) Options for the WriteMany
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="WriteManyOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="WriteManyResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="operations"/> is <c>null</c> or empty or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="WriteOperationCollection"/>
        /// <seealso cref="WriteManyOptions"/>
        /// <seealso cref="WriteManyResult{TRow}"/>
        /// <seealso cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        public Task<WriteManyResult<RecordValue>> WriteManyAsync(
            WriteOperationCollection operations,
            WriteManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<RecordValue>(null, operations,
                options, cancellationToken);
        }

        /// <summary>
        /// Atomically executes a sequence of Put and Delete operations on a
        /// table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is similar to
        /// <see cref="WriteManyAsync(WriteOperationCollection, WriteManyOptions, CancellationToken)"/>,
        /// and is used to execute sequence of Put and Delete operations on a
        /// single table.  All other considerations and limitations of
        /// <see cref="WriteManyAsync(WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        /// apply to this method.
        /// </para>
        /// <para>
        /// This method takes <paramref name="tableName"/> parameter.  To use
        /// this method, when adding operations to
        /// <see cref="WriteOperationCollection"/>, use methods that do not
        /// take table name as a parameter (or pass <c>null</c> as table
        /// name).  See <see cref="WriteOperationCollection"/> for details.
        /// </para>
        /// </remarks>
        /// <example>
        /// Performing WriteMany operation on a single table.
        /// <code>
        /// var row1 = new MapValue
        /// {
        ///     ["id"] = 1000,
        ///     ["name"] = "John"
        /// };
        ///
        /// var row2 = new MapValue
        /// {
        ///     ["id"] = 1001,
        ///     ["name"] = "Jane"
        /// };
        ///
        /// var primaryKey = new MapValue
        /// {
        ///     ["id"] = 2000
        /// };
        ///
        /// var result = await client.WriteManyAsync(
        ///     "myTable",
        ///     new WriteOperationCollection()
        ///         .AddPut(row1)
        ///         .AddPutIfAbsent(row2, true)
        ///         .AddDelete(primaryKey, true));
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="operations">Collection of Put and Delete operations
        /// to execute in a single transaction.</param>
        /// <param name="options">(Optional) Options for the WriteMany
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="WriteManyOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="WriteManyResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="operations"/> is <c>null</c> or empty or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="WriteOperationCollection"/>
        /// <seealso cref="WriteManyOptions"/>
        /// <seealso cref="WriteManyResult{TRow}"/>
        /// <seealso cref="PutManyAsync"/>
        /// <seealso cref="DeleteManyAsync"/>
        public Task<WriteManyResult<RecordValue>> WriteManyAsync(
            string tableName,
            WriteOperationCollection operations,
            WriteManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<RecordValue>(tableName, operations,
                options, cancellationToken);
        }

        /// <summary>
        /// Atomically executes a sequence of Put operations on a table.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        /// and can be used instead of it when all of the following is true:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// All operations are on a single table.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// All operations in the sequence are Put (no Delete
        /// operations).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// You don't need to specify separate <see cref="PutOptions"/> for
        /// each operation in the sequence and instead it is sufficient to
        /// specify the same options for all operations in
        /// <see cref="PutManyOptions"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// There are no Put operations in the sequence conditional on
        /// <see cref="RowVersion"/> (because the version has to be specified
        /// individually for each row).
        /// </description>
        /// </item>
        /// </list>
        /// Other than the above, this API is the same as
        /// <see cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>.
        /// </remarks>
        /// <example>
        /// Performing PutMany operation.
        /// <code>
        /// var row1 = new MapValue
        /// {
        ///     ["id"] = 1000,
        ///     ["name"] = "John"
        /// };
        ///
        /// var row2 = new MapValue
        /// {
        ///     ["id"] = 1001,
        ///     ["name"] = "Jane"
        /// };
        ///
        /// var row3 = new MapValue
        /// {
        ///     ["id"]= 1002,
        ///     ["name"] = "Jill"
        /// };
        ///
        /// var result = await client.PutManyAsync(
        ///     "myTable",
        ///     new[] {  row1, row2, row3 });
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="rows">A collection of rows to put in a single
        /// transaction (can be an array, list or any other class implementing
        /// <see cref="IReadOnlyCollection{T}"/>)</param>
        /// <param name="options">(Optional) Options for the PutMany
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="PutManyOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="WriteManyResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="rows"/> is <c>null</c> or contains invalid
        /// values or <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        public Task<WriteManyResult<RecordValue>> PutManyAsync(
            string tableName,
            IReadOnlyCollection<MapValue> rows,
            PutManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<RecordValue>(tableName,
                CreatePutManyOps(rows, options), options, cancellationToken);
        }

        /// <summary>
        /// Atomically executes a sequence of Delete operations on a table.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        /// and can be used instead of it when all of the following is true:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// All operations are on a single table.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// All operations in the sequence are Delete (no Put
        /// operations).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// You don't need to specify separate <see cref="DeleteOptions"/> for
        /// each operation in the sequence and instead it is sufficient to
        /// specify the same options for all operations in
        /// <see cref="DeleteManyOptions"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// There are no Delete operations in the sequence conditional on
        /// <see cref="RowVersion"/> (because the version has to be specified
        /// individually for each row).
        /// </description>
        /// </item>
        /// </list>
        /// Other than the above, this API is the same as
        /// <see cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>.
        /// </remarks>
        /// <example>
        /// Performing DeleteMany operation.
        /// <code>
        /// var primaryKey1 = new MapValue
        /// {
        ///     ["id"] = 1000
        /// };
        ///
        /// var primaryKey2 = new MapValue
        /// {
        ///     ["id"] = 1001
        /// };
        ///
        /// var primaryKey3 = new MapValue
        /// {
        ///     ["id"]= 1002
        /// };
        ///
        /// var result = await client.DeleteManyAsync(
        ///     "myTable",
        ///     new[] {  primaryKey1, primaryKey2, primaryKey3 });
        /// </code>
        /// </example>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="primaryKeys">A collection of primary keys that
        /// identify the rows to delete in a single transaction (can be an
        /// array, list or any other class implementing
        /// <see cref="IReadOnlyCollection{T}"/>)</param>
        /// <param name="options">(Optional) Options for the DeleteMany
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="DeleteManyOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="WriteManyResult{TRow}"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="tableName"/> is <c>null</c> or invalid or
        /// <paramref name="primaryKeys"/> is <c>null</c> or contains invalid
        /// values or <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        public Task<WriteManyResult<RecordValue>> DeleteManyAsync(
            string tableName,
            IReadOnlyCollection<MapValue> primaryKeys,
            DeleteManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<RecordValue>(tableName,
                CreateDeleteManyOps(primaryKeys, options), options,
                cancellationToken);
        }

    }

}
