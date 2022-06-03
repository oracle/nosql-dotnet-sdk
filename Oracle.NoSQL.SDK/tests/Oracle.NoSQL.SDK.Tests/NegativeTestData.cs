/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static TestSchemas;

    internal static class NegativeTestData
    {
        internal static readonly int[] BadPositiveInt32 =
        {
            -10,
            0
        };

        internal static readonly TimeSpan[] BadTimeSpans =
        {
            TimeSpan.FromSeconds(-1),
            TimeSpan.Zero
        };

        internal static readonly string[] BadNonEmptyStrings =
            { null, string.Empty };

        internal static readonly string[] BadTableNames = BadNonEmptyStrings;

        internal static readonly Consistency[] BadConsistencies =
        {
            (Consistency)10,
            (Consistency)(-1)
        };

        internal static readonly Durability[] BadDurabilities =
        {
            new Durability((SyncPolicy)(-1), SyncPolicy.Sync,
                ReplicaAckPolicy.All),
            new Durability(SyncPolicy.NoSync, (SyncPolicy)100,
                ReplicaAckPolicy.None),
            new Durability(SyncPolicy.WriteNoSync, SyncPolicy.NoSync,
                (ReplicaAckPolicy)(-1)),
            new Durability(SyncPolicy.WriteNoSync, SyncPolicy.NoSync,
                (ReplicaAckPolicy)25)
        };

        // Relevant to both Put and individual put sub-op in WriteMany.
        internal static IEnumerable<TPutOptions>
            GetBaseBadPutOptions<TPutOptions>()
            where TPutOptions : PutOptions, new() =>
            Enumerable.Empty<TPutOptions>()
                .Append(new TPutOptions
                {
                    // cannot have these together
                    TTL = TimeToLive.OfDays(1),
                    UpdateTTLToDefault = true
                })
                .Concat(from size in BadPositiveInt32
                    select new TPutOptions
                    {
                        IdentityCacheSize = size
                    });

        // We use the generic to be able to instantiate both bad PutOptions
        // and bad PutManyOptions.
        internal static IEnumerable<TPutOptions>
            GetBadPutOptions<TPutOptions>()
            where TPutOptions : PutOptions, new() =>
            (from timeout in BadTimeSpans
                select new TPutOptions
                {
                    Timeout = timeout
                })
            .Concat(from durability in BadDurabilities
                select new TPutOptions
            {
                Durability = durability
            })
            .Concat(GetBaseBadPutOptions<TPutOptions>());

        // Relevant to both Put and individual put sub-op in WriteMany.
        // Currently, none apply.
        internal static IEnumerable<TDeleteOptions>
            GetBaseBadDeleteOptions<TDeleteOptions>()
            where TDeleteOptions : DeleteOptions, new() =>
            Enumerable.Empty<TDeleteOptions>();

        // We use the generic to be able to instantiate both bad DeleteOptions
        // and bad DeleteManyOptions.
        internal static IEnumerable<TDeleteOptions>
            GetBadDeleteOptions<TDeleteOptions>()
            where TDeleteOptions : DeleteOptions, new() =>
            (from timeout in BadTimeSpans
                select new TDeleteOptions
                {
                    Timeout = timeout
                })
            .Concat(from durability in BadDurabilities
                select new TDeleteOptions
                {
                    Durability = durability
                })
            .Concat(GetBaseBadDeleteOptions<TDeleteOptions>());

        internal static List<MapValue> GetBadPrimaryKeys(
            TableInfo table, MapValue goodPK,
            bool allowPartial = false)
        {
            var badPKs = new List<MapValue>
            {
                null,
                new MapValue(), // Empty primary key
                new MapValue
                {
                    ["no_such_field"] = 1
                }
            };

            var firstField = table.PrimaryKey[0];
            var lastField = table.PrimaryKey[^1];

            var badPKFieldValues = new List<FieldValue>
            {
                new byte[10],
                new ArrayValue { 1, 2, 3 },
                new MapValue { ["id"] = 1 },
                new RecordValue { ["id"] = 1 }
            };

            // This is due to proxy/kv issue in 21.1.12 that allows null and
            // JSON null in partial multi-field PKs for DeleteRange.  These
            // can be added unconditionally once the issue is fixed.
            if (!allowPartial)
            {
                badPKFieldValues.Add(FieldValue.Null);
                badPKFieldValues.Add(FieldValue.JsonNull);
            }

            badPKs.AddRange(from value in badPKFieldValues
                select
                    SetFieldValueInMap(goodPK, lastField, value));

            // Add invalid multi-field keys
            if (table.PrimaryKey.Length > 1)
            {
                badPKs.Add(RemoveFieldFromMap(goodPK, firstField));
                if (!allowPartial)
                {
                    badPKs.Add(RemoveFieldFromMap(goodPK, lastField));
                }
            }

            return badPKs;
        }

        internal static IEnumerable<MapValue> GetBadRows(
            TableInfo table, MapValue goodRow)
        {
            // Bad primary keys are also bad rows (this assumes all other
            // fields are null and ExactMatch option is not specified).
            var badPKs = GetBadPrimaryKeys(table,
                MakePrimaryKey(table, goodRow));

            var badRows = badPKs.Append(
                    new MapValue
                    {
                        ["no_such_field1"] = 1,
                        ["no_such_field2"] = "a"
                    })
                .Concat(from badPK in badPKs
                    select SetFieldValuesInMap(
                        goodRow, table.PrimaryKey, badPK));

            var nonPKFields =
                (from field in table.Fields select field.Name).Except(
                    table.PrimaryKey);

            var newValues = new[]
            {
                FieldValue.JsonNull,
                "a",
                new RecordValue(),
                new ArrayValue()
            };

            // set values that would be invalid for some non-pk columns
            badRows = badRows.Concat(from newValue in newValues
                select SetFieldValuesInMapAsOne(goodRow, nonPKFields,
                    newValue));

            if (table.IdentityField != null)
            {
                // Here we assume that the identity column is
                // "generated always", so supplying a value for for it is an
                // error.  Also assume that 1 is in allowed range for this
                // identity column.
                badRows = badRows.Append(SetFieldValueInMap(goodRow,
                    table.IdentityField.Name, 1));
            }

            return badRows;
        }

        internal static IEnumerable<MapValue> GetBadExactMatchRows(
            TableInfo table, MapValue goodRow)
        {
            var nonPKField = table.Fields[^1] != table.IdentityField
                ? table.Fields[^1]
                : table.Fields[^2];

            var withMissingField = RemoveFieldFromMap(goodRow,
                nonPKField.Name);
            var withExtraField =
                SetFieldValueInMap(goodRow, "no_such_field", 1);
            var withReplacedField = SetFieldValueInMap(withMissingField,
                "no_such_field2", "replaced");

            return new[] {withExtraField, withReplacedField};
        }

        // goodPK2 > goodPK
        internal static IEnumerable<FieldRange> GetBadFieldRanges(
            TableInfo table, MapValue goodPK, MapValue goodPK2)
        {
            // Test self-check.  We need multi-field primary key to test
            // DeleteRange operation.
            Assert.IsTrue(table.PrimaryKey.Length > 1);

            var firstField = table.PrimaryKey[0];
            var lastField = table.PrimaryKey[^1];

            var badFieldRanges = new List<FieldRange>
            {
                new FieldRange(), // no field name or bounds
                new FieldRange // no field name
                {
                    StartsAfter = goodPK[lastField]
                },
                new FieldRange(lastField), // no bounds specified
                new FieldRange("no_such_field")
                {
                    StartsWith = goodPK[lastField]
                },
                // cannot specify field range on the shard key
                new FieldRange(firstField)
                {
                    EndsWith = goodPK[firstField]
                },
                new FieldRange(lastField)
                {
                    // unsupported type for primary key
                    StartsAfter = new ArrayValue(
                        new List<FieldValue> {1, 2, 3})
                },
                new FieldRange(lastField)
                {
                    EndsBefore = goodPK
                },
                new FieldRange(lastField) // start > end with inclusive
                {
                    StartsWith = goodPK2[lastField],
                    EndsWith = goodPK[lastField]
                },
                new FieldRange(lastField) // start == end with start exclusive
                {
                    StartsAfter = goodPK[lastField],
                    EndsWith = goodPK[lastField]
                },
                new FieldRange(lastField) // start == end with both exclusive
                {
                    StartsAfter = goodPK2[lastField],
                    EndsBefore = goodPK2[lastField]
                }
            };

            if (goodPK[firstField].DbType != goodPK[lastField].DbType)
            {
                badFieldRanges.Add(new FieldRange(lastField)
                {
                    StartsWith = goodPK[firstField],
                    EndsWith = goodPK[lastField]
                });
            }

            return badFieldRanges;
        }
    }
}
