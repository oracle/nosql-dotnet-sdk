/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    internal interface IDataResult
    {
        ConsumedCapacity ConsumedCapacity { get; set; }
    }

    internal interface IWriteResult<TRow>
    {
        bool Success { get; set; }

        TRow ExistingRow { get; set; }

        RowVersion ExistingVersion { get; set; }

        DateTime? ExistingModificationTime { get; set; }
    }

    internal interface IWriteResultWithId<TRow> : IWriteResult<TRow>
    {
        FieldValue GeneratedValue { get; set; }
    }

}
