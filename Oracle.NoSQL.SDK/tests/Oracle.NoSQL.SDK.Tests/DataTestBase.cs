/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static TestSchemas;

    // Changed to decouple functionality in DataTestBase from NoSQLClient
    // handle, so that it can be used in the outside tests. Moved the
    // functionality into DataTestUtils class, so that DataTestBase only has
    // the wrapper methods.
    internal static class DataTestUtils
    {
        internal static async Task PutRowAsync(NoSQLClient client,
            TableInfo table, DataRow row)
        {
            var opt = new PutOptions();

            row.Reset();

            if (row.TTL.HasValue)
            {
                opt.TTL = row.TTL.Value;
            }
            else if (table.TTL.HasValue)
            {
                opt.UpdateTTLToDefault = true;
            }
            else
            {
                opt.TTL = TimeToLive.DoNotExpire;
            }

            var result = await client.PutAsync(table.Name, (MapValue)row,
                opt);
            Assert.IsTrue(result.Success);
            row.PutTime = DateTime.UtcNow;
            Assert.IsNotNull(result.Version);
            row.Version = result.Version;
        }

        internal static async Task PutRowsAsync(NoSQLClient client,
            TableInfo table, IEnumerable<DataRow> rows)
        {
            foreach (var row in rows)
            {
                await PutRowAsync(client, table, row);
            }
        }

        internal static async Task DeleteRowAsync(NoSQLClient client,
            TableInfo table, DataRow row)
        {
            var result = await client.DeleteAsync(table.Name,
                MakePrimaryKey(table, row));
            row.Reset();
        }

        internal static void VerifyConsumedCapacity(NoSQLClient client,
            ConsumedCapacity cc)
        {
            if (IsOnPrem(client))
            {
                Assert.IsNull(cc);
            }
            else
            {
                Assert.IsNotNull(cc);
                Assert.IsTrue(cc.ReadKB >= 0);
                Assert.IsTrue(cc.ReadUnits >= 0);
                Assert.IsTrue(cc.WriteKB >= 0);
                Assert.IsTrue(cc.WriteUnits >= 0);
            }
        }

        // We assume that if row.TTL is null, then the row's TTL is table
        // default TTL, or if table does not have default TTL, then the row
        // does not expire.  If table has default TTL but we want the row not to
        // expire, we must have row.TTL == TimeToLive.DoNotExpire.
        internal static void VerifyExpirationTime(TableInfo table,
            DataRow row, DateTime? expirationTime)
        {
            var ttl = row.TTL ?? table.TTL;

            // TTL.Value == 0 means no expiration
            if (!ttl.HasValue || ttl.Value.Value == 0)
            {
                Assert.IsNull(expirationTime);
            }
            else
            {
                Assert.IsNotNull(expirationTime);
                Assert.IsNotNull(row.PutTime); // test self-check
                Assert.IsTrue(expirationTime > row.PutTime);
                var expectedTTL = TimeToLive.FromExpirationTime(
                    expirationTime.Value, row.PutTime, ttl.Value.TimeUnit);

                // We allow differences in value of 1 to account for potential
                // differences between row.PutTime and the real put time if
                // the put operation is performed very close to day or hour
                // boundary.  This estimation can be improved by considering
                // the boundary cases separately.
                Assert.IsTrue(
                    Math.Abs(ttl.Value.Value - expectedTTL.Value) <= 1);
            }
        }

        private const int ModTimeDeltaMillisLocal = 1000;
        private const int ModeTimeDeltaMillisRemote = 5000;
        private const int ModeTimeDeltaMillisBig = 3600 * 1000;

        internal static void VerifyModificationTime(NoSQLClient client,
            DataRow row, DateTime? modificationTime, bool isPrecise = true)
        {
            if (IsProtocolV3OrAbove(client))
            {
                Assert.IsNotNull(modificationTime);
                var delta = modificationTime.Value - row.ModificationTime;
                // If we don't have more or less precise modification time,
                // just verify that it is somewhere within test run time
                // boundary.  Note that in either case it cannot be verified
                // 100% accurately.
                var maxDeltaMillis = isPrecise
                    ? (IsServerLocal(client)
                        ? ModTimeDeltaMillisLocal
                        : ModeTimeDeltaMillisRemote)
                    : ModeTimeDeltaMillisBig;

                Assert.IsTrue(
                    Math.Abs(delta.TotalMilliseconds) < maxDeltaMillis);
            }
            else
            {
                Assert.IsNull(modificationTime);
            }
        }

        internal static void VerifyExistingModificationTime(NoSQLClient client,
            DataRow row, IWriteResult<RecordValue> result)
        {
            if (IsProtocolV3OrAbove(client))
            {
                Assert.IsNotNull(result.ExistingModificationTime);
                var delta = result.ExistingModificationTime.Value -
                            row.ModificationTime;
                Assert.IsTrue(Math.Abs(delta.TotalMilliseconds) < 500);
            }
            else
            {
                Assert.IsNull(result.ExistingModificationTime);
            }
        }

        private static void VerifyNumber(double expected,
            double actual, double maxRelativeDelta)
        {
            if (expected.Equals(actual))
            {
                return;
            }

            if (!double.IsFinite(expected) || !double.IsFinite(actual))
            {
                Assert.AreEqual(expected, actual);
                return;
            }

            var divisor = Math.Max(Math.Abs(expected), Math.Abs(actual));
            Assert.IsTrue(divisor > 0); // test self-check

            var relativeDelta = Math.Abs(actual - expected) / divisor;
            Assert.IsTrue(relativeDelta <= maxRelativeDelta);
        }

        private static void VerifyNumber(decimal expected,
            decimal actual, decimal maxRelativeDelta)
        {
            if (expected.Equals(actual))
            {
                return;
            }

            var divisor = Math.Max(Math.Abs(expected), Math.Abs(actual));
            Assert.IsTrue(divisor > 0); // test self-check

            var relativeDelta = Math.Abs(actual - expected) / divisor;
            Assert.IsTrue(relativeDelta <= maxRelativeDelta);
        }

        private static readonly double FloatEpsilon = Math.Pow(2, -24);
        private static readonly double DoubleEpsilon = Math.Pow(2, -52);

        private static readonly decimal DecimalEpsilon =
            0.0000000000000000000000000001m;

        private static void VerifyNumeric(FieldValue expected,
            FieldValue actual, FieldType type)
        {
            var deltaMultiplier = type is NumericFieldType numType
                ? numType.DeltaMultiplier
                : 1;

            if (type.DataType == DataType.Double ||
                type.DataType == DataType.Float)
            {
                Assert.IsTrue(actual is DoubleValue);
                VerifyNumber(expected.ToDouble(), actual.AsDouble,
                    type.DataType == DataType.Double
                        ? DoubleEpsilon * deltaMultiplier
                        : FloatEpsilon * deltaMultiplier);
                return;
            }

            // test self-check
            Assert.AreEqual(DataType.Number, type.DataType);
            Assert.IsTrue(actual is NumberValue);

            if (expected is DoubleValue)
            {
                Assert.IsTrue(expected.AsDouble <= (double)decimal.MaxValue &&
                              expected.AsDouble >= (double)decimal.MinValue);
            }

            VerifyNumber(expected.ToDecimal(), actual.AsDecimal,
                DecimalEpsilon * deltaMultiplier);
        }

        private static void VerifyTimestamp(DateTime expected,
            DateTime actual, TimestampFieldType type)
        {
            Assert.AreEqual(DateTimeKind.Utc, actual.Kind);

            // 7 is the precision of 1 tick
            if (type.Precision >= 7)
            {
                // If the precision is >= tick precision, the value should be
                // stored exactly.
                Assert.AreEqual(expected, actual);
                return;
            }

            // This is not most precise, but allows us to avoid dealing with
            // the rounding semantics at the server.
            var deltaLimit = (long)Math.Pow(10, 7 - type.Precision);
            Assert.IsTrue(Math.Abs(actual.Ticks - expected.Ticks) <
                          deltaLimit);
        }

        private static void VerifyMap(FieldValue expected, FieldValue actual,
            MapFieldType type)
        {
            // test self-check
            Assert.AreEqual(typeof(MapValue), expected.GetType());
            // see ReadMap(stream) in BinaryProtocol/Protocol.Reader.cs
            Assert.IsTrue(actual is MapValue);
            Assert.AreEqual(expected.Count, actual.Count);
            var expectedMap = (MapValue)expected;
            var actualMap = (MapValue)actual;
            foreach (var key in expectedMap.Keys)
            {
                var expectedValue = expectedMap[key];
                Assert.IsTrue(actualMap.TryGetValue(key,
                    out var actualValue));
                VerifyFieldValue(expectedValue, actualValue, type.ElementType);
            }
        }

        private static void VerifyRecord(FieldValue expected,
            FieldValue actual, RecordFieldType type)
        {
            // test self-check
            Assert.IsTrue(expected is RecordValue);
            Assert.IsTrue(actual is RecordValue);
            Assert.AreEqual(type.Fields.Count, actual.Count);
            var expectedRecord = (RecordValue)expected;
            var actualRecord = (RecordValue)actual;

            var actualIndex = 0;
            foreach (var field in type.Fields)
            {
                // Verify the field order in the actual record.
                Assert.AreEqual(field.Name, actualRecord.GetKeyAtIndex(
                    actualIndex++));

                Assert.IsTrue(actualRecord.TryGetValue(field.Name,
                    out var actualValue));

                if (!expectedRecord.TryGetValue(field.Name,
                        out var expectedValue))
                {
                    // Skip verification of identity column since its expected
                    // value is not known here.  Where necessary, identity column
                    // values will be verified in VerifyPutAsync().
                    Assert.IsTrue(field.FieldType.IsIdentity);
                    continue;
                }

                VerifyFieldValue(expectedValue, actualValue, field.FieldType);
            }
        }

        private static void VerifyJson(FieldValue expected, FieldValue actual)
        {
            // Note that currently maps can be returned as records,
            // see ReadMap(stream) in BinaryProtocol/Protocol.Reader.cs.  Make
            // sure we use the map equality (expected should not be a record).
            Assert.IsTrue(expected.Equals(actual));
        }

        private static IEnumerable<FieldValue> SortTotalOrder(
            IEnumerable<FieldValue> values) =>
            values.OrderBy(value => value,
                Comparer<FieldValue>.Create((val1, val2) =>
                    val1.QueryCompareTotalOrder(val2)));

        // Check the value returned from the service.  It should be of
        // correct type and equal to the value we sent to the service (after
        // being properly type converted)  The equality may not be exact for
        // types Double and Float.
        internal static void VerifyFieldValue(FieldValue expected,
            FieldValue actual, FieldType type)
        {
            // test self-check
            Assert.IsNotNull(expected);
            Assert.IsNotNull(type);
            Assert.IsNotNull(actual);

            // It is possible for expected advanced query results contain
            // empty value, which should become SQL NULL in actual query
            // results.
            if (expected == FieldValue.Null || expected == FieldValue.Empty)
            {
                if (type.DataType == DataType.Json)
                {
                    // Currently nulls in Json fields are stored as Json nulls.
                    Assert.IsTrue(actual == FieldValue.JsonNull ||
                                  actual == FieldValue.Null);
                }
                else
                {
                    Assert.AreEqual(FieldValue.Null, actual);
                }

                return;
            }

            switch (type.DataType)
            {
                case DataType.Boolean:
                    // test self-check
                    Assert.IsTrue(expected is BooleanValue);
                    Assert.IsTrue(actual is BooleanValue);
                    Assert.AreEqual(expected.AsBoolean, actual.AsBoolean);
                    break;
                case DataType.Integer:
                    // test self-check
                    Assert.IsTrue(expected is IntegerValue);
                    Assert.IsTrue(actual is IntegerValue);
                    Assert.AreEqual(expected.AsInt32, actual.AsInt32);
                    break;
                case DataType.Long:
                    Assert.IsTrue(actual is LongValue);
                    Assert.AreEqual(expected.ToInt64(), actual.AsInt64);
                    break;
                case DataType.Float:
                case DataType.Double:
                    Assert.IsTrue(actual is DoubleValue);
                    VerifyNumeric(expected, actual, type);
                    break;
                case DataType.Number:
                    Assert.IsTrue(actual is NumberValue);
                    VerifyNumeric(expected, actual, type);
                    break;
                case DataType.String:
                case DataType.Enum:
                    // test self-check
                    Assert.IsTrue(expected is StringValue);
                    Assert.IsTrue(actual is StringValue);
                    Assert.AreEqual(expected.AsString, actual.AsString);
                    break;
                case DataType.Timestamp:
                    // test self-check
                    Assert.IsTrue(type is TimestampFieldType);
                    Assert.IsTrue(expected is TimestampValue);
                    Assert.IsTrue(actual is TimestampValue);
                    VerifyTimestamp(expected.AsDateTime, actual.AsDateTime,
                        (TimestampFieldType)type);
                    break;
                case DataType.Binary:
                    // test self-check
                    Assert.IsTrue(expected is BinaryValue);
                    Assert.IsTrue(actual is BinaryValue);
                    AssertDeepEqual(expected.AsByteArray, actual.AsByteArray);
                    break;
                case DataType.Array:
                    // test self-check
                    Assert.IsTrue(expected is ArrayValue);
                    Assert.IsTrue(actual is ArrayValue);
                    Assert.AreEqual(expected.Count, actual.Count);
                    var arrayType = (ArrayFieldType)type;
                    
                    if (arrayType.IsUnordered)
                    {
                        expected = new ArrayValue(
                            SortTotalOrder(expected.AsArrayValue));
                        actual = new ArrayValue(
                            SortTotalOrder(actual.AsArrayValue));
                    }

                    for (var i = 0; i < expected.Count; i++)
                    {
                        VerifyFieldValue(expected[i], actual[i],
                            arrayType.ElementType);
                    }

                    break;
                case DataType.Map:
                    VerifyMap(expected, actual, (MapFieldType)type);
                    break;
                case DataType.Record:
                    VerifyRecord(expected, actual, (RecordFieldType)type);
                    break;
                case DataType.Json:
                    VerifyJson(expected, actual);
                    break;
                default:
                    Assert.Fail(
                        $"Unexpected value of DataType: {type.DataType}");
                    break;
            }
        }

        internal static void VerifyGetResult(NoSQLClient client,
            GetResult<RecordValue> result, TableInfo table, DataRow row,
            Consistency consistency = Consistency.Eventual,
            bool skipVerifyVersion = false,
            bool preciseModificationTime = true)
        {
            Assert.IsNotNull(result);
            VerifyConsumedCapacity(client, result.ConsumedCapacity);

            if (!IsOnPrem(client))
            {
                Assert.AreEqual(0, result.ConsumedCapacity.WriteKB);
                Assert.AreEqual(0, result.ConsumedCapacity.WriteUnits);
                Assert.IsTrue(result.ConsumedCapacity.ReadKB >= 1);
                Assert.IsTrue(result.ConsumedCapacity.ReadUnits >=
                              (consistency == Consistency.Absolute ? 2 : 1));
            }

            if (row == null)
            {
                Assert.IsNull(result.Row);
                Assert.IsNull(result.Version);
                Assert.IsNull(result.ExpirationTime);
                Assert.IsNull(result.ModificationTime);
                return;
            }

            Assert.IsNotNull(result.Version);
            VerifyExpirationTime(table, row, result.ExpirationTime);
            VerifyModificationTime(client, row, result.ModificationTime,
                preciseModificationTime);

            // For update queries (unlike puts) we don't know the row's
            // current version, so we don't verify.
            if (!skipVerifyVersion)
            {
                AssertDeepEqual(row.Version, result.Version, true);
            }

            VerifyFieldValue(row, result.Row, table.RecordType);
        }

        internal static async Task VerifyPutAsync(NoSQLClient client,
            PutResult<RecordValue> result,
            TableInfo table, DataRow row, PutOptions options = null,
            bool success = true, DataRow existingRow = null,
            bool isSubOp = false, bool isConditional = false,
            bool verifyExistingModTime = true)
        {
            Assert.IsNotNull(row); // test self-check
            Assert.IsNotNull(result);

            isConditional = isConditional || options != null &&
                options.putOpKind != PutOpKind.Always;

            Assert.AreEqual(success, result.Success);

            if (!isSubOp)
            {
                VerifyConsumedCapacity(client, result.ConsumedCapacity);
                if (!IsOnPrem(client))
                {
                    if (success)
                    {
                        Assert.IsTrue(result.ConsumedCapacity.WriteKB >= 1);
                        Assert.IsTrue(
                            result.ConsumedCapacity.WriteUnits >= 1);
                    }
                    else
                    {
                        Assert.IsTrue(result.ConsumedCapacity.WriteKB == 0);
                        Assert.IsTrue(
                            result.ConsumedCapacity.WriteUnits == 0);
                    }

                    if (isConditional)
                    {
                        Assert.IsTrue(result.ConsumedCapacity.ReadKB >= 1);
                        Assert.IsTrue(result.ConsumedCapacity.ReadUnits >= 1);
                    }
                    else
                    {
                        Assert.IsTrue(result.ConsumedCapacity.ReadKB == 0);
                        Assert.IsTrue(result.ConsumedCapacity.ReadUnits == 0);
                    }
                }
            }

            if (success)
            {
                Assert.IsNotNull(result.Version);
                AssertNotDeepEqual(row.Version, result.Version, true);
                Assert.IsNull(result.ExistingRow);
                Assert.IsNull(result.ExistingVersion);

                var isNew = row.Version == null;
                row.Version = result.Version;
                row.ModificationTime = DateTime.UtcNow;
                // We update the put time if TTL is indicated in options or if
                // TTL is being updated to the table's default or if this is a
                // new row.
                if (isNew || options?.TTL != null ||
                    options != null && options.UpdateTTLToDefault &&
                    table.TTL.HasValue)
                {
                    row.PutTime = DateTime.UtcNow;
                    // If TTL is not specified in options, VerifyGetResult()
                    // will use the table default TTL.
                    row.TTL = options?.TTL;
                }
            }
            else
            {
                Assert.IsTrue(isConditional); // test self-check
                Assert.IsNull(result.Version);
                // existingRow should be provided if and only if this is a
                // failure case and there is an existing row matching the
                // primary key.
                if (options != null && options.ReturnExisting &&
                    existingRow != null)
                {
                    VerifyFieldValue(existingRow, result.ExistingRow,
                        table.RecordType);
                    Assert.IsNotNull(existingRow.Version); // test self-check
                    AssertDeepEqual(existingRow.Version,
                        result.ExistingVersion, true);
                    if (verifyExistingModTime)
                    {
                        VerifyExistingModificationTime(client, existingRow,
                            result);
                    }
                }
                else
                {
                    Assert.IsNull(result.ExistingRow);
                    Assert.IsNull(result.ExistingVersion);
                }
            }

            var getResult = await client.GetAsync(table.Name,
                MakePrimaryKey(table, row));
            // This will verify that we get the same row as we put, including its
            // version, expiration time and modification time
            VerifyGetResult(client, getResult, table,
                success ? row : existingRow);

            // Verify generated value for an identity column if any
            if (success && result.GeneratedValue != null)
            {
                Assert.IsNotNull(table.IdentityField); // test self-check
                Assert.IsTrue(result.Success);
                Assert.IsNotNull(getResult.Row);
                VerifyFieldValue(getResult.Row[table.IdentityField.Name],
                    result.GeneratedValue, table.IdentityField.FieldType);
            }
        }

        internal static async Task VerifyDeleteAsync(NoSQLClient client,
            DeleteResult<RecordValue> result, TableInfo table,
            MapValue primaryKey, DeleteOptions options = null,
            bool success = true, DataRow existingRow = null,
            bool isSubOp = false, bool isConditional = false,
            bool verifyExistingModTime = true)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(success, result.Success);
            isConditional = isConditional || options?.MatchVersion != null;

            if (!isSubOp)
            {
                VerifyConsumedCapacity(client, result.ConsumedCapacity);
                if (!IsOnPrem(client))
                {
                    Assert.IsTrue(result.ConsumedCapacity.ReadKB >= 1);
                    Assert.IsTrue(result.ConsumedCapacity.ReadUnits >= 1);
                    if (success)
                    {
                        Assert.IsTrue(result.ConsumedCapacity.WriteKB >= 1);
                        Assert.IsTrue(
                            result.ConsumedCapacity.WriteUnits >= 1);
                    }
                    else
                    {
                        Assert.IsTrue(result.ConsumedCapacity.WriteKB == 0);
                        Assert.IsTrue(
                            result.ConsumedCapacity.WriteUnits == 0);
                    }
                }
            }

            if (!result.Success && options != null &&
                options.ReturnExisting && existingRow != null)
            {
                Assert.IsTrue(isConditional); // test self-check
                VerifyFieldValue(existingRow, result.ExistingRow,
                    table.RecordType);
                Assert.IsNotNull(existingRow.Version); // test self-check
                AssertDeepEqual(existingRow.Version, result.ExistingVersion,
                    true);
                if (verifyExistingModTime)
                {
                    VerifyExistingModificationTime(client, existingRow,
                        result);
                }
            }
            else
            {
                Assert.IsNull(result.ExistingRow);
                Assert.IsNull(result.ExistingVersion);
            }

            // This will verify that row does not exist after successful
            // delete or delete on non-existent row, or that the row is the
            // same as existingRow if deleteIfVersion fails on existing row
            var getResult = await client.GetAsync(table.Name, primaryKey);
            VerifyGetResult(client, getResult, table,
                success ? null : existingRow);
        }
    }

    public abstract class DataTestBase<TTests> : TablesTestBase<TTests>
    {
        internal static Task PutRowAsync(TableInfo table, DataRow row) =>
            DataTestUtils.PutRowAsync(client, table, row);

        internal static Task PutRowsAsync(TableInfo table,
            IEnumerable<DataRow> rows) =>
            DataTestUtils.PutRowsAsync(client, table, rows);

        internal static Task DeleteRowAsync(TableInfo table, DataRow row) =>
            DataTestUtils.DeleteRowAsync(client, table, row);

        internal static void VerifyConsumedCapacity(ConsumedCapacity cc) =>
            DataTestUtils.VerifyConsumedCapacity(client, cc);

        // We assume that if row.TTL is null, then the row's TTL is table
        // default TTL, or if table does not have default TTL, then the row
        // does not expire.  If table has default TTL but we want the row not to
        // expire, we must have row.TTL == TimeToLive.DoNotExpire.
        internal static void VerifyExpirationTime(TableInfo table,
            DataRow row, DateTime? expirationTime) =>
            DataTestUtils.VerifyExpirationTime(table, row, expirationTime);

        internal static void VerifyModificationTime(DataRow row,
            DateTime? modificationTime, bool isPrecise = true) =>
            DataTestUtils.VerifyModificationTime(client, row,
                modificationTime, isPrecise);

        internal static void VerifyExistingModificationTime(DataRow row,
            IWriteResult<RecordValue> result) =>
            DataTestUtils.VerifyExistingModificationTime(client, row, result);

        internal static void VerifyFieldValue(FieldValue expected,
            FieldValue actual, FieldType type) =>
            DataTestUtils.VerifyFieldValue(expected, actual, type);

        internal static void VerifyGetResult(GetResult<RecordValue> result,
            TableInfo table, DataRow row,
            Consistency consistency = Consistency.Eventual,
            bool skipVerifyVersion = false,
            bool preciseModificationTime = true) =>
            DataTestUtils.VerifyGetResult(client, result, table, row,
                consistency, skipVerifyVersion, preciseModificationTime);

        internal static Task VerifyPutAsync(
            PutResult<RecordValue> result,
            TableInfo table, DataRow row, PutOptions options = null,
            bool success = true, DataRow existingRow = null,
            bool isSubOp = false, bool isConditional = false,
            bool verifyExistingModTime = true) =>
            DataTestUtils.VerifyPutAsync(client, result, table, row, options,
                success, existingRow, isSubOp, isConditional,
                verifyExistingModTime);

        internal static Task VerifyDeleteAsync(
            DeleteResult<RecordValue> result, TableInfo table,
            MapValue primaryKey, DeleteOptions options = null,
            bool success = true, DataRow existingRow = null,
            bool isSubOp = false, bool isConditional = false,
            bool verifyExistingModTime = true) =>
            DataTestUtils.VerifyDeleteAsync(client, result, table, primaryKey,
                options, success, existingRow, isSubOp, isConditional,
                verifyExistingModTime);
    }
}
