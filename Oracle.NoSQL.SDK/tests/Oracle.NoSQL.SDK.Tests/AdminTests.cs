/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;

    [TestClass]
    public class AdminTests : TablesTestBase<AdminTests>
    {
        private static readonly IEnumerable<string> BadStatements =
            BadNonEmptyStrings.Append("blah blah");

        private static readonly IEnumerable<AdminOptions>
            BadAdminOptions =
                (from timeout in BadTimeSpans
                    select new AdminOptions
                    {
                        Timeout = timeout
                    })
                .Concat(
                    from pollDelay in BadTimeSpans
                    select new AdminOptions
                    {
                        PollDelay = pollDelay
                    })
                .Append(new AdminOptions
                {
                    // poll delay greater than timeout
                    Timeout = TimeSpan.FromSeconds(5),
                    PollDelay = TimeSpan.FromMilliseconds(5100)
                });

        private static IEnumerable<object[]> AdminNegativeDataSource =>
            (from badStmt in BadStatements
                select new object[]
                {
                    badStmt,
                    null
                })
            .Concat(from badOpt in BadAdminOptions
                select new object[]
                {
                    "CREATE NAMESPACE IF NOT EXISTS foo",
                    badOpt
                });

        private static IEnumerable<object[]> AdminListNegativeDataSource =>
            from badOpt in BadAdminOptions
            select new object[]
            {
                badOpt
            };

        private static AdminResult goodAdminResult;

        private static readonly IEnumerable<GetAdminStatusOptions>
            BadGetAdminStatusOptions =
                from timeout in BadTimeSpans
                select new GetAdminStatusOptions
                {
                    Timeout = timeout
                };

        private static IEnumerable<object[]>
            GetAdminStatusNegativeDataSource =>
            (from badOpt in BadGetAdminStatusOptions
            select new object[]
            {
                goodAdminResult,
                badOpt
            })
            .Append(new object[]
            {
                null,
                null
            })
            .Append(new object[]
            {
                null,
                new GetAdminStatusOptions
                {
                    Timeout = TimeSpan.FromSeconds(5)
                }
            });

        private static IEnumerable<object[]>
            ForCompletionNegativeDataSource =>
            // This may need to be changed if more options are added to
            // AdminOptions.
            from badOpt in BadAdminOptions
            select new object[]
            {
                badOpt.Timeout,
                badOpt.PollDelay
            };

        private static readonly string[] Namespaces =
        {
            "list_test_n1",
            "list_test_n2"
        };

        private static readonly string[] Users =
        {
            "list_test_user1",
            "list_test_user2"
        };

        private static readonly string[] Roles =
        {
            "list_test_role1",
            "list_test_role2"
        };

        private static async Task CreateListMetaDataAsync()
        {
            foreach(var ns in Namespaces)
            {
                await client.ExecuteAdminWithCompletionAsync(
                    $"CREATE NAMESPACE IF NOT EXISTS {ns}");
            }

            if (!IsSecureOnPrem)
            {
                return;
            }

            foreach (var user in Users)
            {
                try
                {
                    await client.ExecuteAdminWithCompletionAsync(
                        $"DROP USER {user}");
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                try
                {
                    await client.ExecuteAdminWithCompletionAsync(
                        $"CREATE USER {user} IDENTIFIED BY \"OracleP123##45\"");
                }
                catch (UnauthorizedException ex)
                {
                    Assert.Fail(
                        "To run these tests, grant the user sysadmin role " +
                        "or SYSOPER privilege.  You may also run these " +
                        "tests against non-secure kvstore, in which case " +
                        "only a limited subset will be run.  " +
                        $"Error message: {ex.Message}");
                }
            }

            foreach (var role in Roles)
            {
                try
                {
                    await client.ExecuteAdminWithCompletionAsync(
                        $"DROP ROLE {role}");
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                await client.ExecuteAdminWithCompletionAsync(
                    $"CREATE ROLE {role}");
            }
        }

        private static async Task DropListMetaDataAsync()
        {
            foreach (var ns in Namespaces)
            {
                await client.ExecuteAdminWithCompletionAsync(
                    $"DROP NAMESPACE IF EXISTS {ns}");
            }

            if (!IsSecureOnPrem)
            {
                return;
            }

            foreach (var user in Users)
            {
                try
                {
                    await client.ExecuteAdminWithCompletionAsync(
                        $"DROP USER {user}");
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception) { }
            }

            foreach (var role in Roles)
            {
                try
                {
                    await client.ExecuteAdminWithCompletionAsync(
                        $"DROP ROLE {role}");
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception) { }
            }
        }

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            if (!IsOnPrem)
            {
                return;
            }

            await CreateListMetaDataAsync();
            await client.ExecuteAdminWithCompletionAsync(
                "DROP NAMESPACE IF EXISTS NS4TEST12");
            goodAdminResult = await client.ExecuteAdminAsync(
                "CREATE NAMESPACE NS4TEST12");
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            if (IsOnPrem)
            {
                await client.ExecuteAdminWithCompletionAsync(
                    "DROP NAMESPACE IF EXISTS NS4TEST12");
                await DropListMetaDataAsync();
            }

            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminNegativeDataSource))]
        public async Task TestAdminNegativeAsync(string stmt,
            AdminOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteAdminAsync(stmt, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteAdminAsync(stmt?.ToCharArray(), options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteAdminWithCompletionAsync(stmt, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteAdminWithCompletionAsync(stmt?.ToCharArray(),
                    options));
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminListNegativeDataSource))]
        public async Task TestAdminListNegativeAsync(AdminOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ListNamespacesAsync(options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ListUsersAsync(options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ListRolesAsync(options));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetAdminStatusNegativeDataSource))]
        public async Task TestGetAdminStatusNegativeAsync(
            AdminResult adminResult, GetAdminStatusOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetAdminStatusAsync(adminResult, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(ForCompletionNegativeDataSource))]
        public async Task TestForCompletionNegativeAsync(TimeSpan? timeout,
            TimeSpan? pollDelay)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                goodAdminResult.WaitForCompletionAsync(timeout, pollDelay));
        }

        private static void VerifyAdminResult(AdminResult result,
            bool completed, bool hasOutput = false)
        {
            Assert.IsNotNull(result);
            if (completed || result.OperationId == null)
            {
                Assert.AreEqual(AdminState.Complete, result.State);
            }

            if (result.OperationId != null)
            {
                Assert.IsTrue(result.OperationId.Length > 0);
            }

            if (result.Statement != null)
            {
                Assert.IsTrue(result.Statement.Length > 0);
            }

            if (hasOutput)
            {
                Assert.IsFalse(string.IsNullOrEmpty(result.Output));
            }
            else
            {
                Assert.IsNull(result.Output);
            }
        }

        private string cleanupStmt;

        private void SetForCleanup(string cleanup)
        {
            cleanupStmt = cleanup;
        }

        // For some reason MSTest executes tests even if ClassInitialize
        // fails. Instead we disable them here if we are not running on-prem.
        [TestInitialize]
        public void TestInitialize()
        {
            CheckOnPrem();
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (cleanupStmt != null)
            {
                var result = await client.ExecuteAdminWithCompletionAsync(
                    cleanupStmt);
                VerifyAdminResult(result, true);
                cleanupStmt = null;
            }
        }

        private static readonly object[][] AdminTestCasesNonSecure =
        {
            new object[]
            {
                "CREATE NAMESPACE N1DDL2TEST",
                "DROP NAMESPACE IF EXISTS N1DDL2TEST"
            },
            new object[]
            {
                "SHOW NAMESPACES",
                null
            }
        };

        private static readonly object[][] AdminTestCasesSecure =
        {
            new object[]
            {
                "CREATE USER USER1TEST IDENTIFIED BY \"User1Pass@@5555\"",
                "DROP USER USER1TEST"
            },
            new object[]
            {
                "SHOW USERS",
                null
            },            new object[]
            {
                "CREATE ROLE ROLE1TEST",
                "DROP ROLE ROLE1TEST"
            },

            new object[]
            {
                "SHOW AS JSON ROLES",
                null
            }
        };

        private static IEnumerable<object[]> AdminDataSource =>
            IsSecureOnPrem
                ? AdminTestCasesNonSecure.Concat(AdminTestCasesSecure)
                : AdminTestCasesNonSecure;

        private static bool IsShow(string stmt) => stmt.StartsWith("SHOW");

        private static void CheckSecureOnPrem()
        {
            if (!IsSecureOnPrem)
            {
                Assert.Inconclusive(
                    "This test runs only with secure on-prem kvstore");
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminDataSource))]
        public async Task TestAdminWithForCompletionAsync(string stmt,
            string cleanup)
        {
            SetForCleanup(cleanup);
            var isShow = IsShow(stmt);

            var result = await client.ExecuteAdminAsync(stmt);
            VerifyAdminResult(result, isShow, isShow);

            var result2 = await result.WaitForCompletionAsync();
            AssertDeepEqual(result, result2);
            VerifyAdminResult(result, true, isShow);

            result = await client.GetAdminStatusAsync(result);
            VerifyAdminResult(result, true, isShow);

            await result.WaitForCompletionAsync();
            VerifyAdminResult(result, true, isShow);
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminDataSource))]
        public async Task TestCharArrayAdminWithForCompletionAsync(
            string stmt, string cleanup)
        {
            SetForCleanup(cleanup);
            var isShow = IsShow(stmt);
            var charStmt = stmt.ToCharArray();

            var result = await client.ExecuteAdminAsync(charStmt);
            VerifyAdminResult(result, isShow, isShow);

            result = await client.GetAdminStatusAsync(result);
            VerifyAdminResult(result, isShow, isShow);

            await result.WaitForCompletionAsync();
            VerifyAdminResult(result, true, isShow);
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminDataSource))]
        public async Task TestAdminWithForCompletionWithOptionsAsync(
            string stmt, string cleanup)
        {
            SetForCleanup(cleanup);
            var isShow = IsShow(stmt);

            var result = await client.ExecuteAdminAsync(stmt,
                new AdminOptions
                {
                    Timeout = TimeSpan.FromSeconds(15),
                    // should be ignored here
                    PollDelay = TimeSpan.FromSeconds(2)
                });
            VerifyAdminResult(result, isShow, isShow);

            result = await client.GetAdminStatusAsync(result,
                new GetAdminStatusOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(8888)
                });
            VerifyAdminResult(result, isShow, isShow);

            await result.WaitForCompletionAsync(
                TimeSpan.FromMilliseconds(10002),
                TimeSpan.FromMilliseconds(2222));
            // the operation should be completed now
            VerifyAdminResult(result, true, isShow);

            await result.WaitForCompletionAsync(null,
                TimeSpan.FromMilliseconds(100));
            VerifyAdminResult(result, true, isShow);
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminDataSource))]
        public async Task TestAdminWithCompletionAndOptionsAsync(
            string stmt, string cleanup)
        {
            SetForCleanup(cleanup);
            var isShow = IsShow(stmt);

            var result = await client.ExecuteAdminWithCompletionAsync(stmt,
                new AdminOptions
                {
                    Timeout = TimeSpan.FromSeconds(12),
                    PollDelay = TimeSpan.FromMilliseconds(899)
                });
            VerifyAdminResult(result, true, isShow);

            // already completed
            result = await client.GetAdminStatusAsync(result);
            VerifyAdminResult(result, true, isShow);

            var result2 = await result.WaitForCompletionAsync(
                TimeSpan.FromSeconds(10));
            AssertDeepEqual(result, result2);
            VerifyAdminResult(result2, true, isShow);
        }

        [DataTestMethod]
        [DynamicData(nameof(AdminDataSource))]
        public async Task TestCharArrayAdminWithCompletionAsync(
            string stmt, string cleanup)
        {
            SetForCleanup(cleanup);
            var isShow = IsShow(stmt);
            var charStmt = stmt.ToCharArray();

            var result = await client.ExecuteAdminWithCompletionAsync(
                charStmt);
            VerifyAdminResult(result, true, isShow);

            await result.WaitForCompletionAsync();
            VerifyAdminResult(result, true, isShow);
        }

        // Test FromTicks and fractional TimeSpan values
        private static readonly AdminOptions[] AdminListOptions =
        {
            new AdminOptions
            {
                PollDelay = TimeSpan.FromTicks(9999999)
            },
            new AdminOptions
            {
                Timeout = TimeSpan.FromTicks(55555555),
                PollDelay = TimeSpan.FromSeconds(0.5)
            }
        };

        [TestMethod]
        public async Task TestListNamespacesAsync()
        {
            var result = await client.ListNamespacesAsync();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= Namespaces.Length);

            foreach (var value in result)
            {
                Assert.IsFalse(string.IsNullOrEmpty(value));
            }

            foreach (var value in Namespaces)
            {
                Assert.IsTrue(result.Contains(value));
            }

            foreach (var opt in AdminListOptions)
            {
                var result2 = await client.ListNamespacesAsync(opt);
                AssertDeepEqual(result, result2);
            }
        }

        [TestMethod]
        public async Task TestListUsersAsync()
        {
            CheckSecureOnPrem();

            var result = await client.ListUsersAsync();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= Namespaces.Length);

            foreach (var value in result)
            {
                Assert.IsFalse(string.IsNullOrEmpty(value.Name));
                Assert.IsFalse(string.IsNullOrEmpty(value.Id));
            }

            var userNames = (from value in result select value.Name).ToList();
            foreach (var value in Users)
            {
                Assert.IsTrue(userNames.Contains(value));
            }

            foreach (var opt in AdminListOptions)
            {
                var result2 = await client.ListUsersAsync(opt);
                AssertDeepEqual(result, result2);
            }
        }

        [TestMethod]
        public async Task TestListRolesAsync()
        {
            CheckSecureOnPrem();

            var result = await client.ListRolesAsync();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= Roles.Length);

            foreach (var value in result)
            {
                Assert.IsFalse(string.IsNullOrEmpty(value));
            }

            foreach (var value in Roles)
            {
                Assert.IsTrue(result.Contains(value));
            }

            foreach (var opt in AdminListOptions)
            {
                var result2 = await client.ListRolesAsync(opt);
                AssertDeepEqual(result, result2);
            }
        }

    }
}
