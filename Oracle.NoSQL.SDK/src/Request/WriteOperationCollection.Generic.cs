/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    public partial class WriteOperationCollection
    {
        private void AddPutOp(PutOperation putOp)
        {
            putOp.Validate();
            AddValidatedPutOp(putOp);
        }

        private void AddDeleteOp(DeleteOperation deleteOp)
        {
            deleteOp.Validate();
            AddValidatedDeleteOp(deleteOp);
        }

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

        internal WriteOperationCollection AddPut<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutOperation(row, options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPut<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPut(row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfAbsentOperation(row, options,
                abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfAbsent<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfAbsent(row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(TRow row,
            PutOptions options, bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfPresentOperation(row, options,
                abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfPresent<TRow>(TRow row,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfPresent(row, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(TRow row,
            RowVersion matchVersion, PutOptions options,
            bool abortIfUnsuccessful = false)
        {
            AddPutOp(new PutIfVersionOperation(row, matchVersion, options,
                abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddPutIfVersion<TRow>(TRow row,
            RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddPutIfVersion(row, matchVersion, null,
                abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDelete(object primaryKey,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            AddDeleteOp(new DeleteOperation(primaryKey, options,
                abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddDelete(object primaryKey,
            bool abortIfUnsuccessful = false)
        {
            return AddDelete(primaryKey, null, abortIfUnsuccessful);
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            object primaryKey, RowVersion matchVersion,
            DeleteOptions options, bool abortIfUnsuccessful = false)
        {
            AddDeleteOp(new DeleteIfVersionOperation(primaryKey, matchVersion,
                options, abortIfUnsuccessful));
            return this;
        }

        internal WriteOperationCollection AddDeleteIfVersion(
            object primaryKey, RowVersion matchVersion,
            bool abortIfUnsuccessful = false)
        {
            return AddDeleteIfVersion(primaryKey, matchVersion, null,
                abortIfUnsuccessful);
        }
    }
}
