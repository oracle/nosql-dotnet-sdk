/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a collection of Put and Delete operations for
    /// <see cref="NoSQLClient.WriteManyAsync"/> API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class represents the operations to be performed as a list of
    /// instances implementing <see cref="IWriteOperation"/> interface,
    /// consisting of the following classes:
    /// <list>
    /// <item>
    /// <description>
    /// <see cref="PutOperation"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="PutIfAbsentOperation"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="PutIfPresentOperation"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="PutIfVersionOperation"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="DeleteOperation"/>
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="DeleteIfVersionOperation"/>
    /// </description>
    /// </item>
    /// </list>
    /// You do not need to instantiate the above classes to add operations
    /// to the collection, instead use methods of
    /// <see cref="WriteOperationCollection"/>.  However the instances of
    /// the classes above will be available for inspection if you iterate
    /// through the collection using <c>foreach</c> loop, in which case you
    /// can downcast <see cref="IWriteOperation"/> to one of the classes
    /// listed above.
    /// </para>
    /// <para>
    /// The methods of <see cref="WriteOperationCollection"/> return the
    /// instance itself so that their calls can be chained as shown in the
    /// example.  Validation is performed on the parameters before the
    /// operation is added to the collection.
    /// </para>
    /// <para>
    /// Note that some of the methods of
    /// <see cref="WriteOperationCollection"/> take <see cref="PutOptions"/>
    /// or <see cref="DeleteOptions"/> parameters as the options for the
    /// operation. Properties such as <see cref="PutOptions.Compartment"/>,
    /// <see cref="PutOptions.Timeout"/>,
    /// <see cref="DeleteOptions.Compartment"/> and
    /// <see cref="DeleteOptions.Timeout"/> are ignored as they are not
    /// not relevant for a single operation in the collection.  Instead, use
    /// <see cref="WriteManyOptions.Compartment"/> and
    /// <see cref="WriteManyOptions.Timeout"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Populating an instance of <see cref="WriteOperationCollection"/>.
    /// <code>
    /// var woc = new WriteOperationCollection()
    ///     .AddPut(row1, true)
    ///     .AddPutIfVersion(row2, version2)
    ///     .AddPut(row3, new PutOptions
    ///     {
    ///         TTL = TimeToLive.OfDays(1)
    ///     })
    ///     .AddDelete(primaryKey3, true)
    ///     .AddDeleteIfVersion(primaryKey4, version4, true));
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.WriteManyAsync"/>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="PutOperation"/>
    /// <seealso cref="PutIfAbsentOperation"/>
    /// <seealso cref="PutIfPresentOperation"/>
    /// <seealso cref="PutIfVersionOperation"/>
    /// <seealso cref="DeleteOperation"/>
    /// <seealso cref="DeleteIfVersionOperation"/>
    public partial class WriteOperationCollection :
        IReadOnlyCollection<IWriteOperation>
    {
        internal List<IWriteOperation> ops = new List<IWriteOperation>();

        /// <summary>
        /// Adds a <see cref="PutOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="options">Options for the Put operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <exception cref="ArgumentException">If <paramref name="row"/> is
        /// <c>null</c> or <paramref name="options"/> contains invalid values.
        /// </exception>
        public WriteOperationCollection AddPut(MapValue row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPut<MapValue>(row, options, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <exception cref="ArgumentException">If <paramref name="row"/> is
        /// <c>null</c>.
        /// </exception>
        public WriteOperationCollection AddPut(MapValue row,
            bool abortIfUnsuccessful = false)
        {
            return AddPut<MapValue>(row, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfAbsentOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="options">Options for the Put operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <inheritdoc cref="AddPut(MapValue, PutOptions, bool)" path="exception"/>
        public WriteOperationCollection AddPutIfAbsent(MapValue row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent<MapValue>(row, options,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfAbsentOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <inheritdoc cref="AddPut(MapValue, bool)" path="exception"/>
        public WriteOperationCollection AddPutIfAbsent(MapValue row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent<MapValue>(row, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfPresentOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="options">Options for the Put operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <inheritdoc cref="AddPut(MapValue, PutOptions, bool)" path="exception"/>
        public WriteOperationCollection AddPutIfPresent(MapValue row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent<MapValue>(row, options,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfPresentOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <inheritdoc cref="AddPut(MapValue, bool)" path="exception"/>
        public WriteOperationCollection AddPutIfPresent(MapValue row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent<MapValue>(row, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfVersionOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="options">Options for the Put operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <exception cref="ArgumentException">If <paramref name="row"/> is
        /// <c>null</c> or <paramref name="version"/> is <c>null</c> or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        public WriteOperationCollection AddPutIfVersion(MapValue row,
            RowVersion version, PutOptions options,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion<MapValue>(row, version, options,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="PutIfVersionOperation"/> to the collection.
        /// </summary>
        /// <param name="row">Table row.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Put operation fails, it will cause the entire transaction to
        /// abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Put operation was
        /// added.</returns>
        /// <exception cref="ArgumentException">If <paramref name="row"/> is
        /// <c>null</c> or <paramref name="version"/> is <c>null</c>.
        /// </exception>
        public WriteOperationCollection AddPutIfVersion(MapValue row,
            RowVersion version, bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion<MapValue>(row, version,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="DeleteOperation"/> to the collection.
        /// </summary>
        /// <param name="primaryKey">Primary key of the row to delete.</param>
        /// <param name="options">Options for the Delete operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Delete operation fails, it will cause the entire transaction
        /// to abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Delete operation
        /// was added.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="primaryKey"/> is <c>null</c> or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        public WriteOperationCollection AddDelete(MapValue primaryKey,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            return AddDelete((object)primaryKey, options,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="DeleteOperation"/> to the collection.
        /// </summary>
        /// <param name="primaryKey">Primary key of the row to delete.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Delete operation fails, it will cause the entire transaction
        /// to abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Delete operation
        /// was added.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="primaryKey"/> is <c>null</c>.
        /// </exception>
        public WriteOperationCollection AddDelete(MapValue primaryKey,
            bool abortIfUnsuccessful = false)
        {
            return AddDelete((object)primaryKey, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="DeleteOperation"/> to the collection.
        /// </summary>
        /// <param name="primaryKey">Primary key of the row to delete.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="options">Options for the Delete operation.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Delete operation fails, it will cause the entire transaction
        /// to abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Delete operation
        /// was added.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="primaryKey"/> is <c>null</c> or
        /// <paramref name="version"/> is <c>null</c> or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        public WriteOperationCollection AddDeleteIfVersion(
            MapValue primaryKey, RowVersion version,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion((object)primaryKey, version,
                options, abortIfUnsuccessful);
        }

        /// <summary>
        /// Adds a <see cref="DeleteOperation"/> to the collection.
        /// </summary>
        /// <param name="primaryKey">Primary key of the row to delete.</param>
        /// <param name="version">Row version to match.</param>
        /// <param name="abortIfUnsuccessful">(Optional) If <c>true</c> and
        /// this Delete operation fails, it will cause the entire transaction
        /// to abort, see <see cref="NoSQLClient.WriteManyAsync"/>.</param>
        /// <returns>A reference to this instance after the Delete operation
        /// was added.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="primaryKey"/> is <c>null</c> or
        /// <paramref name="version"/> is <c>null</c>.
        /// </exception>
        public WriteOperationCollection AddDeleteIfVersion(
            MapValue primaryKey, RowVersion version,
            bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion((object)primaryKey, version,
                abortIfUnsuccessful);
        }

        /// <summary>
        /// Gets the number of operations in the collection.
        /// </summary>
        /// <value>
        /// The number of operations in the collection.
        /// </value>
        public int Count => ops.Count;

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator{T}"/> of
        /// <see cref="IWriteOperation"/> to enumerate through the collection.
        /// </returns>
        IEnumerator<IWriteOperation>
            IEnumerable<IWriteOperation>.GetEnumerator()
        {
            return ops.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> to enumerate through the
        /// collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ops.GetEnumerator();
        }
    }

}
