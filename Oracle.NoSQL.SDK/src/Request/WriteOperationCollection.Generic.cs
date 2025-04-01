/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;

    public partial class WriteOperationCollection
    {
        private readonly List<IWriteOperation> ops;

        // Used by rate limiting.
        internal bool DoesReads { get; private set; }

        // Avoid repeated validation of options when creating
        // WriteOperationCollection for PutManyAsync.
        internal void AddValidatedPutOp(PutOperation putOp)
        {
            ops.Add(putOp);

            if (putOp.DoesReads)
            {
                DoesReads = true;
            }
        }

        // Avoid repeated validation of options when creating
        // WriteOperationCollection for DeleteManyAsync.
        internal void AddValidatedDeleteOp(DeleteOperation deleteOp)
        {
            ops.Add(deleteOp);
        }

        internal void AddPutOp(PutOperation putOp)
        {
            putOp.Validate();
            AddValidatedPutOp(putOp);
        }

        internal void AddDeleteOp(DeleteOperation deleteOp)
        {
            deleteOp.Validate();
            AddValidatedDeleteOp(deleteOp);
        }

        internal IWriteOperation this[int idx] => ops[idx];

        internal WriteOperationCollection AddPut<TRow>(string tableName,
            TRow row, PutOptions options, bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutOperation(tableName, row, options,
                abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPut<TRow>(string tableName,
            TRow row, bool abortIfUnsuccessful = false)
        {
            return AddPut(tableName, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(
            string tableName, TRow row, PutOptions options,
            bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfAbsentOperation(tableName, row,
                options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(
            string tableName, TRow row, bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent(tableName, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(
            string tableName, TRow row, PutOptions options,
            bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfPresentOperation(tableName, row,
                options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(
            string tableName, TRow row, bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent(tableName, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(
            string tableName, TRow row, RowVersion matchVersion,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfVersionOperation(tableName, row,
                matchVersion, options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(
            string tableName, TRow row, RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion(tableName, row, matchVersion, null,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDelete(string tableName,
            object primaryKey, DeleteOptions options,
            bool abortIfUnsuccessful = false)
        {
            AddDeleteOp(new DeleteOperation(tableName, primaryKey,
                options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddDelete(string tableName,
            object primaryKey, bool abortIfUnsuccessful = false)
        {
            return AddDelete(tableName, primaryKey, null,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            string tableName, object primaryKey, RowVersion matchVersion,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            AddDeleteOp(new DeleteIfVersionOperation(tableName,
                primaryKey, matchVersion, options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            string tableName, object primaryKey, RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion(tableName, primaryKey, matchVersion,
                null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPut<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPut(null, row, options, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPut<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPut(null, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent(null, row, options, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent(null, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent(null, row, options, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent(null, row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(TRow row,
            RowVersion matchVersion, PutOptions options,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion(null, row, matchVersion, options,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(TRow row,
            RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion(null, row, matchVersion, null,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDelete(object primaryKey,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            return AddDelete(null, primaryKey, options, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDelete(object primaryKey,
            bool abortIfUnsuccessful = false)
        {
            return AddDelete(null, primaryKey, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            object primaryKey, RowVersion matchVersion,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion(null, primaryKey, matchVersion, options,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            object primaryKey, RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion(null, primaryKey, matchVersion, null,
                abortIfUnsuccessful);
        }
    }

}
