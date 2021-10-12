/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

using System.Collections;
using System.Collections.Generic;

namespace Oracle.NoSQL.Driver.Query {
    using System;

    internal static class Utils
    {
        internal static T[] ConcatArrays<T>(params T[][] arrays)
        {
            var length = 0;
            foreach (var array in arrays)
            {
                length += array.Length;
            }

            var result = new T[length];
            var offset = 0;
            foreach (var array in arrays)
            {
                Array.Copy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }

            return result;
        }

        internal static void ConvertEmptyToNull(MapValue value)
        {
            foreach(var kvPair in value)
            {
                if (kvPair.Value == FieldValue.Empty)
                {
                    value[kvPair.Key] = FieldValue.Null;
                }
            }
        }

        internal static int CompareRows(RecordValue row1, RecordValue row2,
            SortSpec[] sortSpecs)
        {
            foreach (var spec in sortSpecs)
            {
                var result = row1[spec.FieldName].QueryCompare(
                    row2[spec.FieldName], spec.NullRank);

                if (spec.IsDescending)
                {
                    result = -result;
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        internal static bool IsCountFunc(SQLFuncCode code)
        {
            return code == SQLFuncCode.CountStar ||
                   code == SQLFuncCode.Count ||
                   code == SQLFuncCode.CountNumbers;
        }

    }

}
