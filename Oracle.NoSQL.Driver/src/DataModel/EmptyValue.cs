/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System.Text.Json;

    internal class EmptyValue : FieldValue
    {
        private EmptyValue()
        {
        }

        public static readonly EmptyValue Instance = new EmptyValue();

        public override DbType DbType => DbType.Empty;

        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteNullValue();
        }

        internal override bool IsSpecial => true;

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Null:
                case DbType.JsonNull:
                    return -1;
                case DbType.Empty:
                    return 0;
                default:
                    return other.SupportsComparison ? nullRank :
                        throw ComparisonNotSupported(other);
            }
        }

        internal override bool QueryEquals(FieldValue other)
        {
            return other.DbType == DbType.Empty;
        }

        internal override int QueryHashCode() => 0;

        internal override long GetMemorySize() => 0;
    }
}
