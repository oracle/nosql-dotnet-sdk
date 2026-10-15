/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;

    [TestClass]
    public class QueryFieldStepTests
    {
        [TestMethod]
        public void TestCaseProjectionDistinguishesJsonNullAndSqlNull()
        {
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });
            var runtime = new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = 4,
                DriverQueryPlan = new ReceiveStep()
            });
            var selected = new ConstStep { ResultPosition = 3 };
            var iterator = new FieldStep
            {
                ResultPosition = 0,
                FieldName = "key",
                InputStep = new CaseStep
                {
                    ResultPosition = 1,
                    ConditionSteps = new PlanStep[] { new ConstStep
                        { ResultPosition = 2, Value = BooleanValue.True } },
                    ThenSteps = new PlanStep[] { selected }
                }
            }.CreateSyncIterator(runtime);

            foreach (var value in new[] { FieldValue.JsonNull, FieldValue.Null })
            {
                selected.Value = value;
                iterator.Reset(true);
                var hasResult = iterator.Next();
                Assert.AreEqual(value == FieldValue.Null, hasResult);
                if (hasResult) Assert.AreSame(FieldValue.Null, iterator.Result);
                Assert.IsFalse(iterator.Next(), "Exhaustion must be stable");
                iterator.Reset(true);
                Assert.AreEqual(hasResult, iterator.Next());
            }
        }
    }
}
