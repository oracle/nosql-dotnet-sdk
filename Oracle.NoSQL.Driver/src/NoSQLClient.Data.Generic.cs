/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver {
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class NoSQLClient
    {
        internal async Task<GetResult<TRow>> GetAsync<TRow>(
            string tableName,
            object primaryKey,
            GetOptions options = null,
            CancellationToken cancellationToken = default) where TRow : class
        {
            return (GetResult<TRow>) await ExecuteRequestAsync(
                new GetRequest<TRow>(this, tableName, primaryKey, options),
                cancellationToken);
        }

        internal async Task<PutResult<TRow>> PutAsync<TRow>(
            string tableName,
            TRow row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<TRow>) await ExecuteRequestAsync(
                new PutRequest<TRow>(this, tableName, row, options),
                cancellationToken);
        }

        internal async Task<PutResult<TRow>> PutIfAbsentAsync<TRow>(
            string tableName,
            TRow row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<TRow>) await ExecuteRequestAsync(
                new PutIfAbsentRequest<TRow>(this, tableName, row, options),
                cancellationToken);
        }

        internal async Task<PutResult<TRow>> PutIfPresentAsync<TRow>(
            string tableName,
            TRow row,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<TRow>) await ExecuteRequestAsync(
                new PutIfPresentRequest<TRow>(this, tableName, row, options),
                cancellationToken);
        }

        internal async Task<PutResult<TRow>> PutIfVersionAsync<TRow>(
            string tableName,
            TRow row,
            RowVersion version,
            PutOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PutResult<TRow>) await ExecuteRequestAsync(
                new PutIfVersionRequest<TRow>(this, tableName, row, version,
                    options), cancellationToken);
        }

        internal async Task<DeleteResult<TRow>> DeleteAsync<TRow>(
            string tableName,
            object primaryKey,
            DeleteOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (DeleteResult<TRow>) await ExecuteRequestAsync(
                new DeleteRequest<TRow>(this, tableName, primaryKey, options),
                cancellationToken);
        }

        internal async Task<DeleteResult<TRow>> DeleteIfVersionAsync<TRow>(
            string tableName,
            object primaryKey,
            RowVersion version,
            DeleteOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (DeleteResult<TRow>) await ExecuteRequestAsync(
                new DeleteIfVersionRequest<TRow>(this, tableName, primaryKey,
                    version, options), cancellationToken);
        }

        internal Task<WriteManyResult<TRow>> WriteManyAsync<TRow>(
            string tableName,
            WriteOperationCollection operations,
            WriteManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<TRow>(tableName, operations,
                options, cancellationToken);
        }

        internal Task<WriteManyResult<TRow>> PutManyAsync<TRow>(
            string tableName,
            IReadOnlyCollection<TRow> rows,
            PutManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<TRow>(tableName,
                CreatePutManyOps(rows, options), options, cancellationToken);
        }

        internal Task<WriteManyResult<TRow>> DeleteManyAsync<TRow>(
            string tableName,
            IReadOnlyCollection<object> primaryKeys,
            DeleteManyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return WriteManyInternalAsync<TRow>(tableName,
                CreateDeleteManyOps(primaryKeys, options), options,
                cancellationToken);
        }
    }

}
