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

    public partial class WriteManyTests
    {
        private static IEnumerable<TestCase> TestCases()
        {
            yield return new WriteManyTestCase(
                "put even, delete odd, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePut(fromStart)
                    : MakeDelete(fromStart));

            yield return new WriteManyTestCase(
                "put even, delete odd, returnExisting, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePut(fromStart, null,
                        new PutOptions { ReturnExisting = true })
                    : MakeDelete(fromStart, null,
                        new DeleteOptions { ReturnExisting = true }));

            yield return new WriteManyTestCase("one put, new row, success",
                new[]
                {
                    MakePut(1, options: new PutOptions
                    {
                        ExactMatch = true
                    })
                });

            yield return new WriteManyTestCase(
                "one put, new row, returnExisting, success",
                new[]
                {
                    MakePut(1, options: new PutOptions
                    {
                        ExactMatch = true,
                        ReturnExisting = true
                    })
                });

            yield return new WriteManyTestCase(
                "one delete, existing row, success",
                new[]
                {
                    MakeDelete(1)
                });

            yield return new WriteManyTestCase(
                "one delete, existing row, returnExisting, success",
                new[]
                {
                    MakeDelete(1, options: new DeleteOptions
                    {
                        ReturnExisting = true
                    })
                });

            yield return new WriteManyTestCase(
                "two puts, one ifAbsent and abortOnFail, fail",
                new[]
                {
                    MakePutIfAbsent(0, abortIfUnsuccessful: true),
                    MakePut(fromEnd: 0)
                }, false);

            yield return new WriteManyTestCase(
                "two puts, returnExisting, one ifAbsent and abortOnFail, " +
                "fail",
                new[]
                {
                    MakePutIfAbsent(0, abortIfUnsuccessful: true,
                        options: new PutOptions { ReturnExisting = true }),
                    MakePut(fromEnd: 0)
                }, false);

            yield return new PutManyTestCase(
                "put 10 new, abortOnFail in opt, success",
                from fromEnd in Enumerable.Range(0, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    AbortIfUnsuccessful = true,
                    Timeout = TimeSpan.FromSeconds(20),
                    Compartment = Compartment,
                    Durability = Durability.CommitWriteNoSync
                });

            yield return new DeleteManyTestCase(
                "delete 10, abortOnFail in opt, success",
                from fromStart in Enumerable.Range(0, 10)
                select MakePK(fromStart),
                true,
                new DeleteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new DeleteManyTestCase(
                "delete 10 past the end, abortOnFail in opt, fail",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd),
                false,
                new DeleteManyOptions
                {
                    Compartment = Compartment,
                    AbortIfUnsuccessful = true
                });

            yield return new DeleteManyTestCase(
                "delete 10 past the end, returnExisting, abortOnFail in " +
                "opt, fail",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd),
                false,
                new DeleteManyOptions
                {
                    Compartment = Compartment,
                    AbortIfUnsuccessful = true,
                    ReturnExisting = true
                });

            yield return new DeleteManyTestCase(
                "delete 10 past the end, abortOnFail not set, success",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd), true, new DeleteManyOptions
                {
                    Timeout = TimeSpan.FromSeconds(15),
                    Durability = Durability.CommitNoSync
                },
                idx => idx >= 4);

            yield return new DeleteManyTestCase(
                "delete 10 past the end, returnExisting, abortOnFail " +
                "not set, success",
                from fromEnd in Enumerable.Range(-4, 10)
                select MakePK(null, fromEnd), true, new DeleteManyOptions
                {
                    Timeout = TimeSpan.FromSeconds(15),
                    Durability = Durability.CommitNoSync,
                    ReturnExisting = true
                },
                idx => idx >= 4);

            yield return new WriteManyTestCase(
                "ifPresent: true, no updates, success, returnExisting",
                from fromEnd in Enumerable.Range(0, 5)
                select MakePut(null, fromEnd, new PutOptions
                {
                    IfPresent = true,
                    ReturnExisting = true
                }), true, new WriteManyOptions(),
                idx => true);

            yield return new WriteManyTestCase(
                "PutIfPresent, abortOnFail overrides opt, fail, " +
                "returnExisting: true",
                from fromEnd in Enumerable.Range(0, 5)
                select MakePutIfPresent(null, fromEnd, new PutOptions
                {
                    ReturnExisting = true
                }, fromEnd >= 3),
                false,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = false,
                    Timeout = TimeSpan.FromSeconds(30),
                    Durability = Durability.CommitSync
                });

            yield return new WriteManyTestCase(
                "PutIfPresent, abortOnFail true on last, fail, " +
                "returnExisting: true",
                from fromEnd in Enumerable.Range(0, 7)
                select MakePut(null, fromEnd, new PutOptions
                {
                    IfPresent = fromEnd == 6,
                    ReturnExisting = true
                }, fromEnd == 6),
                false);

            yield return new PutManyTestCase(
                "putMany, ifPresent: true in opt, no updates, success",
                from fromEnd in Enumerable.Range(0, 5)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfPresent = true,
                    Compartment = Compartment
                },
                idx => true);

            yield return new PutManyTestCase(
                "putMany, ifPresent: true in opt, over rowIdEnd boundary, " +
                "some updates, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfPresent = true,
                    ExactMatch = false
                },
                idx => idx >= 5);

            yield return new PutManyTestCase(
                "putMany, ifPresent, returnExisting: true in opt, over " +
                "rowIdEnd boundary, some updates, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfPresent = true,
                    ExactMatch = false,
                    ReturnExisting = true
                },
                idx => idx >= 5);

            yield return new PutManyTestCase(
                "putMany, ifAbsent and returnExisting are true in opt, " +
                "over rowIdEnd boundary, some updates, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    IfAbsent = true,
                    ReturnExisting = true
                },
                idx => idx < 5);

            yield return new WriteManyTestCase(
                "put even, delete odd with correct matchVersion, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePutIfVersion(GetMatchVersion(fromStart), fromStart)
                    : MakeDeleteIfVersion(GetMatchVersion(fromStart),
                        fromStart));

            yield return new WriteManyTestCase(
                "put even, delete odd with correct matchVersion, " +
                "returnExisting, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePutIfVersion(GetMatchVersion(fromStart), fromStart)
                    : MakeDeleteIfVersion(GetMatchVersion(fromStart),
                        fromStart,
                        options: new DeleteOptions { ReturnExisting = true }));

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion of row 5 in opt, " +
                "returnExisting: true, 1 update, success",
                from fromStart in Enumerable.Range(0, 8)
                select MakeRow(fromStart),
                true,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(5),
                    ReturnExisting = true
                },
                idx => idx != 5);

            yield return new DeleteManyTestCase(
                "deleteMany with incorrect matchVersion of row 5 and " +
                "returnExisting in opt, 1 delete, success",
                from fromStart in Enumerable.Range(0, 8)
                select MakePK(fromStart),
                true,
                new DeleteManyOptions
                {
                    MatchVersion = GetMatchVersion(5),
                    ReturnExisting = true
                },
                idx => idx != 5);

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion and returnExisting in " +
                "opt, no updates, success",
                from fromStart in Enumerable.Range(1, 7)
                select MakeRow(fromStart),
                true,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(0),
                    ReturnExisting = true
                },
                idx => true);

            yield return new PutManyTestCase(
                "putMany with incorrect matchVersion, returnExisting and " +
                "abortOnFail in opt, no updates, fail",
                from fromStart in Enumerable.Range(1, 7)
                select MakeRow(fromStart),
                false,
                new PutManyOptions
                {
                    MatchVersion = GetMatchVersion(0),
                    ReturnExisting = true,
                    AbortIfUnsuccessful = true
                },
                idx => true);

            yield return new DeleteManyTestCase(
                "putMany with incorrect matchVersion, returnExisting and " +
                "abortOnFail in opt, no deletes, fail",
                from fromStart in Enumerable.Range(1, 7)
                select MakePK(fromStart),
                false,
                new DeleteManyOptions
                {
                    MatchVersion = GetMatchVersion(0),
                    ReturnExisting = true,
                    AbortIfUnsuccessful = true
                },
                idx => true);

            yield return new WriteManyTestCase(
                "put with different TTLs followed by delete, success",
                (from fromStart in Enumerable.Range(0, 5)
                    select MakePut(fromStart, options: new PutOptions
                    {
                        TTL = TimeToLive.OfDays(fromStart + 1)
                    }))
                .Concat(
                    from fromStart in Enumerable.Range(5, 5)
                    select MakeDelete(fromStart, options: new DeleteOptions
                    {
                        MatchVersion = GetMatchVersion(fromStart)
                    })),
                true,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new PutManyTestCase(
                "putMany, across rowIdEnd, same TTL in opt, success",
                from fromEnd in Enumerable.Range(-5, 10)
                select MakeRow(null, fromEnd),
                true,
                new PutManyOptions
                {
                    TTL = TimeToLive.OfHours(10),
                    Durability = new Durability(SyncPolicy.Sync,
                        SyncPolicy.Sync, ReplicaAckPolicy.All)
                });
        }

        private static IEnumerable<TestCase> MultiTableTestCases()
        {
            yield return new WriteManyTestCase(
                "put even, delete odd from child table, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePut(ParentFixture, fromStart)
                    : MakeDelete(ChildFixture, fromStart));

            yield return new WriteManyTestCase(
                "put even, delete odd from child table, returnExisting, " +
                "success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePut(ParentFixture, fromStart,
                        options: new PutOptions { ReturnExisting = true })
                    : MakeDelete(ChildFixture, fromStart,
                        options: new DeleteOptions
                            { ReturnExisting = true }));

            yield return new WriteManyTestCase(
                "put odd for child table, delete even, success",
                from fromStart in Enumerable.Range(0, 15)
                select fromStart % 2 != 0 || fromStart >= 10
                    ? MakePut(ChildFixture, fromStart)
                    : MakeDelete(ParentFixture, fromStart));


            yield return new WriteManyTestCase("two puts, one ifAbsent, fail",
                new[]
                {
                    MakePutIfAbsent(ParentFixture, 0,
                        abortIfUnsuccessful: true),
                    MakePut(ChildFixture, 0,
                        options: new PutOptions { ReturnExisting = true })
                }, false, null, idx => idx == 0);

            yield return new WriteManyTestCase(
                "put new rows for parent and child tables, abortOnFail in opt, success",
                (from fromEnd in Enumerable.Range(0, 5)
                    select MakePutIfAbsent(ParentFixture, null, fromEnd))
                .Concat(from fromEnd in Enumerable.Range(0, 17)
                    select MakePutIfAbsent(ChildFixture, null, fromEnd)),
                true,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true,
                    Timeout = TimeSpan.FromSeconds(30),
                    Compartment = Compartment
                });

            yield return new WriteManyTestCase(
                "delete from parent and child tables, abortOnFail in opt, success",
                (from fromStart in Enumerable.Range(0, 5)
                    select MakeDelete(ParentFixture, fromStart))
                .Concat(from fromStart in Enumerable.Range(0, 17)
                    select MakeDelete(ChildFixture, fromStart)),
                true,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new WriteManyTestCase(
                "PutIfPresent, abortOnFail for child table overrides opt, fail",
                (from fromEnd in Enumerable.Range(0, 5)
                    select MakePutIfPresent(ParentFixture, null, fromEnd))
                .Concat(from fromEnd in Enumerable.Range(0, 5)
                    select MakePutIfPresent(ChildFixture, null, fromEnd, null,
                        fromEnd >= 3)),
                false,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = false,
                    Timeout = TimeSpan.FromSeconds(30)
                });

            yield return new WriteManyTestCase(
                "PutIfPresent, no updates, success",
                (from fromEnd in Enumerable.Range(0, 5)
                    select MakePutIfPresent(ParentFixture, null, fromEnd))
                .Concat(from fromEnd in Enumerable.Range(0, 5)
                    select MakePutIfPresent(ChildFixture, null, fromEnd)),
                true,
                null,
                idx => true);

            yield return new WriteManyTestCase(
                "PutIfPresent, over rowIdEnd boundary, some updates, success",
                (from fromEnd in Enumerable.Range(-5, 10)
                    select MakePutIfPresent(ParentFixture, null, fromEnd))
                .Concat(from fromEnd in Enumerable.Range(-5, 10)
                    select MakePutIfPresent(ChildFixture, null, fromEnd)),
                true,
                null,
                idx => (idx >= 5 && idx < 10) || idx >= 15);

            yield return new WriteManyTestCase(
                "put even, delete odd from child table with correct matchVersion, success",
                from fromStart in Enumerable.Range(0, 20)
                select fromStart % 2 == 0
                    ? MakePutIfVersion(ParentFixture,
                        GetMatchVersion(ParentFixture, fromStart), fromStart)
                    : MakeDeleteIfVersion(ChildFixture,
                        GetMatchVersion(ChildFixture, fromStart), fromStart));

            yield return new WriteManyTestCase(
                "put with incorrect matchVersion for child table, " +
                "returnExisting and abortOnFail in opt, no updates, fail",
                (from fromStart in Enumerable.Range(1, 7)
                    select MakePutIfVersion(ChildFixture,
                        // incorrect match version
                        GetMatchVersion(ChildFixture, 0), fromStart, null,
                        new PutOptions { ReturnExisting = true }))
                .Concat(from fromStart in Enumerable.Range(1, 2)
                    select MakePutIfVersion(ParentFixture,
                        // correct match version
                        GetMatchVersion(ParentFixture, fromStart), fromStart, null,
                        new PutOptions { ReturnExisting = true })),
                false,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true
                });

            yield return new WriteManyTestCase(
                "put to child table with different TTLs followed by delete, success",
                (from fromStart in Enumerable.Range(0, 5)
                    select MakePut(ChildFixture, fromStart, null,
                        new PutOptions
                            { TTL = TimeToLive.OfDays(fromStart + 1) }))
                .Concat(from fromStart in Enumerable.Range(5, 5)
                    select MakeDeleteIfVersion(ParentFixture,
                        // correct match version
                        GetMatchVersion(ParentFixture, fromStart), fromStart)),
                true,
                new WriteManyOptions
                {
                    AbortIfUnsuccessful = true
                });
        }

    }

}
