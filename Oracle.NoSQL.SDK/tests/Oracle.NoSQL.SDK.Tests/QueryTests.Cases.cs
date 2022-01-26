/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

// ReSharper disable StringLiteralTypo

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static TestSchemas;
    using static Utils;

    public partial class QueryTests
    {
        // Fill NULLs for fields that were not specified.
        private static DataRow FillRowForInsert(DataRow row)
        {
            foreach (var field in Fixture.Table.Fields)
            {
                if (!row.ContainsKey(field.Name))
                {
                    row[field.Name] = FieldValue.Null;
                }
            }

            return row;
        }

        //TODO: add TTL
        private static QTest MakeInsertTestWithBindings(
            IReadOnlyList<string> nonPKFieldNames, int testCaseCount = 1)
        {
            var fieldNames = Fixture.Table.PrimaryKey.Concat(
                nonPKFieldNames ?? Enumerable.Empty<string>()).ToList();

            var varDecl = string.Join("; ",
                from name in fieldNames
                let type = Fixture.Table.GetField(name).FieldType.DataTypeName
                select $"${name} {type}");
            var colList = string.Join(FieldSeparator, fieldNames);
            var varList = string.Join(FieldSeparator,
                from name in fieldNames select "$" + name);

            var result = new QTest(
                $"insert with bindings for columns ({colList}), " +
                $"{testCaseCount} test-case(s)",
                $"DECLARE {varDecl}; INSERT INTO " +
                $"{Fixture.Table.Name}({colList}) VALUES({varList})", new[]
                {
                    new TableField("NumRowsInserted", DataType.Integer)
                }, null, true);

            var testCases = new QTestCase[testCaseCount];
            for (var i = 0; i < testCaseCount; i++)
            {
                var row = Fixture.MakeRow(10000 + i);
                var bindings = new MapValue();
                var newRow = new DataRow(row.Id);
                foreach (var name in fieldNames)
                {
                    bindings["$" + name] = row[name];
                    newRow[name] = row[name];
                }

                testCases[i] = new QTestCase($"testCase {i}", bindings,
                    new[]
                    {
                        new RecordValue
                        {
                            ["NumRowsInserted"] = 1
                        }
                    })
                {
                    UpdatedRowList = new[] {FillRowForInsert(newRow)}
                };
            }

            result.TestCases = testCases;
            return result;
        }

        internal static IEnumerable<QTest> QTests()
        {
            yield return new QTest("select *, direct execution",
                $"SELECT * FROM {Fixture.Table.Name}")
            {
                ExpectedRows = Fixture.Rows
            };

            yield return new QTest(
                "selection and projection, direct execution",
                "SELECT colInteger, colNumber as numField, colArray, " +
                $"colMap2, colJSON FROM {Fixture.Table.Name} WHERE " +
                "colBoolean = true ORDER BY shardId, pkString")
            {
                IsOrdered = true,
                ExpectedRows =
                    from row in Fixture.Rows
                    where row.Id % 4 == 0
                    select new RecordValue
                    {
                        ["colInteger"] = row["colInteger"],
                        ["numField"] = row["colNumber"],
                        ["colArray"] = row["colArray"],
                        ["colMap2"] = row["colMap2"],
                        ["colJSON"] = row["colJSON"]
                    },
                ExpectedFields = new[]
                {
                    Fixture.Table.GetField("colInteger"),
                    new TableField("numField", DataType.Number),
                    Fixture.Table.GetField("colArray"),
                    Fixture.Table.GetField("colMap2"),
                    Fixture.Table.GetField("colJSON")
                }
            };

            yield return new QTest("select * with bindings",
                "DECLARE $fldInt INTEGER; SELECT * FROM " +
                $"{Fixture.Table.Name} WHERE colInteger < $fldInt", null,
                new[]
                {
                    new QTestCase("empty result", new MapValue
                    {
                        ["$fldInt"] = 0x70000000
                    }),
                    new QTestCase("5 rows", new MapValue
                        {
                            ["$fldInt"] = 0x70000006
                        }, from id in Enumerable.Range(1, 5)
                        select Fixture.MakeRow(id))
                });

            yield return new QTest("select columns with bindings",
                "DECLARE $fldString STRING; SELECT shardId, pkString FROM " +
                $"{Fixture.Table.Name} t WHERE t.colJSON.x = $fldString",
                new []
                {
                    Fixture.Table.GetField("shardId"),
                    Fixture.Table.GetField("pkString")
                },
                new[]
                {
                    new QTestCase("empty result", new MapValue
                    {
                        ["$fldString"] = "abc"
                    }),
                    new QTestCase("17 rows", new MapValue
                        {
                            ["$fldString"] = "a"
                        }, from id in Enumerable.Range(0, 20)
                        where id % 7 != 0
                        select new RecordValue
                        {
                            ["shardId"] = 0,
                            ["pkString"] = RepeatString("id", id) + id
                        })
                });

            yield return new QTest("update single row, with bindings",
                "DECLARE $fldPKString STRING; $fldDouble DOUBLE; UPDATE " +
                $"{Fixture.Table.Name} t SET t.colDouble = $fldDouble, SET " +
                "t.colJSON.x = 'X' WHERE shardId = 0 AND " +
                "pkString = $fldPKString", new[]
                {
                    new TableField("NumRowsUpdated", DataType.Integer)
                }, new[]
                {
                    new QTestCase("update non-existent row, no updates",
                        new MapValue
                        {
                            ["$fldPKString"] = "blahblah",
                            ["$fldDouble"] = 1.2345e100
                        }, new[]
                        {
                            new RecordValue
                            {
                                ["NumRowsUpdated"] = 0
                            }
                        }),
                    new QTestCase("update existing row, 1 update",
                        new MapValue
                        {
                            ["$fldPKString"] = "idid2",
                            ["$fldDouble"] = -9873.25e-100
                        }, new[]
                        {
                            new RecordValue
                            {
                                ["NumRowsUpdated"] = 1
                            }
                        })
                    {
                        UpdatedRowList = new[]
                        {
                            new Func<DataRow>(() =>
                            {
                                var result = DeepCopy(Fixture.GetRow(2));
                                result["colDouble"] = -9873.25e-100;
                                result["colJSON"]["x"] = "X";
                                return result;
                            })()
                        }
                    }
                }, true);

            yield return new QTest("update TTL direct",
                $"UPDATE {Fixture.Table.Name} $t SET TTL 5 DAYS WHERE " +
                "shardId = 0 AND pkString = '0' RETURNING " +
                "remaining_days($t) AS remainingDays", new[]
                {
                    new TableField("remainingDays", DataType.Integer)
                })
            {
                IsUpdate = true,
                UpdateTTL = true,
                ExpectedRows = new[]
                {
                    new RecordValue
                    {
                        ["remainingDays"] = 5
                    }
                },
                UpdatedRows = new[]
                {
                    CopyAndSetProperties(Fixture.GetRow(0), new
                    {
                        TTL = TimeToLive.OfDays(5)
                    })
                }
            };

            yield return new QTest("insert direct",
                $"INSERT INTO {Fixture.Table.Name}(shardId, pkString, " +
                "colInteger, colTimestamp) VALUES(10, 'new_pk_string1', 1, " +
                "'1990-01-01')", new[]
                {
                    new TableField("NumRowsInserted", DataType.Integer)
                })
            {
                IsUpdate = true,
                ExpectedRows = new[]
                {
                    new RecordValue
                    {
                        ["NumRowsInserted"] = 1
                    }
                },
                UpdatedRows = new[]
                {
                    FillRowForInsert(new DataRow(Fixture.GetRowIdFromShard(10))
                    {
                        ["shardId"] = 10,
                        ["pkString"] = "new_pk_string1",
                        ["colInteger"] = 1,
                        ["colTimestamp"] = new DateTime(1990, 01, 01, 0, 0,
                            0, DateTimeKind.Utc)
                    })
                }
            };

            yield return MakeInsertTestWithBindings(new[]
                {"colBoolean", "colNumber", "colBinary", "colJSON"});

            yield return new QTest("simple delete all",
                $"DELETE FROM {Fixture.Table.Name}",
                new[] {new TableField("numRowsDeleted", DataType.Long)},
                null, true)
            {
                ExpectedRows = new[]
                {
                    new RecordValue
                    {
                        ["numRowsDeleted"] = Fixture.RowCount
                    }
                },
                DeletedRowIds = Enumerable.Range(Fixture.RowIdStart,
                    Fixture.RowCount)
            };

            {
                var retCols = new[]
                    {"shardId", "pkString", "colEnum", "colMap", "colJSON"};
                var rowIds =
                    (from id in Enumerable.Range(Fixture.RowIdStart,
                            Fixture.RowCount)
                        where id % 4 != 0 && id % 5 >= 2
                        select id).ToList();

                yield return new QTest(
                    "delete with where and returning clause",
                    $"DELETE FROM {Fixture.Table.Name} t WHERE " +
                    "t.colRecord.fldString > 'a' RETURNING " +
                    string.Join(FieldSeparator, retCols),
                    (from retCol in retCols
                        select Fixture.Table.GetField(retCol)).ToList(),
                    isUpdate: true)
                {
                    DeletedRowIds = rowIds,
                    ExpectedRows = from rowId in rowIds
                        select ProjectRecord(Fixture.GetRow(rowId), retCols)
                };
            }

            {
                var nowTime = RowFactory.ReferenceTime;
                var rowIdsTestCase2 =
                    (from row in Fixture.Rows
                        where row["colDouble"].DbType == DbType.Double &&
                              double.IsPositiveInfinity(
                                  row["colDouble"].AsDouble)
                        select row.Id).ToList();
                var rowIdsTestCase3 =
                    (from row in Fixture.Rows
                        where row.Id > 10 &&
                              row["colDouble"].DbType == DbType.Double &&
                              double.IsNaN(row["colDouble"].AsDouble)
                        select row.Id).ToList();

                yield return new QTest("delete with bindings",
                    "DECLARE $fldDouble DOUBLE; $fldDate TIMESTAMP; DELETE " +
                    $"FROM {Fixture.Table.Name} t WHERE " +
                    "t.colDouble = $fldDouble AND " +
                    "t.colArray[] >ANY $fldDate RETURNING colFixedBinary",
                    new[] {new TableField("colFixedBinary", DataType.Binary)},
                    new[]
                    {
                        new QTestCase("no rows deleted", new MapValue
                        {
                            ["$fldDouble"] = 1,
                            ["$fldDate"] = nowTime
                        }),
                        new QTestCase("all rows with colDouble=Infinity",
                            new MapValue
                            {
                                ["$fldDouble"] = double.PositiveInfinity,
                                ["$fldDate"] = nowTime
                            })
                        {
                            DeletedRowIds = rowIdsTestCase2,
                            ExpectedRows =
                                from rowId in rowIdsTestCase2
                                select new RecordValue
                                {
                                    ["colFixedBinary"] =
                                        Fixture.GetRow(rowId)["colFixedBinary"]
                                }
                        },
                        new QTestCase(
                            "all rows with colDouble = NaN and id > 10",
                            new MapValue
                            {
                                ["$fldDouble"] = double.NaN,
                                ["$fldDate"] = nowTime + TimeSpan.FromSeconds(10)
                            })
                        {
                            DeletedRowIds = rowIdsTestCase3,
                            ExpectedRows =
                                from rowId in rowIdsTestCase3
                                select new RecordValue
                                {
                                    ["colFixedBinary"] =
                                        Fixture.GetRow(rowId)["colFixedBinary"]
                                }
                        }
                    },
                    true);
            }
        }
    }
}
