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
    public class QueryV6CompatibilityTests
    {
        [TestMethod]
        public void TestPreparedStatementUsesEachUnionBranch()
        {
            var statement = new PreparedStatement();
            var firstProxy = new byte[] { 1 };
            var secondProxy = new byte[] { 2 };

            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = firstProxy,
                Namespace = "firstNamespace",
                TableName = "firstTable"
            });
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = secondProxy,
                Namespace = "secondNamespace",
                TableName = "secondTable"
            });

            CollectionAssert.AreEqual(firstProxy,
                statement.GetProxyStatement(0));
            CollectionAssert.AreEqual(secondProxy,
                statement.GetProxyStatement(1));
            Assert.AreEqual("firstNamespace", statement.GetNamespace(0));
            Assert.AreEqual("secondTable", statement.GetTableName(1));
            Assert.ThrowsException<BadProtocolException>(() =>
                statement.GetProxyStatement(2));
        }

        [TestMethod]
        public void TestQueryVersionFallbackFromV6ToV3()
        {
            var handler = new ProtocolHandler();

            Assert.AreEqual(QueryRequestBase.QueryV6, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV6));
            Assert.AreEqual(QueryRequestBase.QueryV5, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV5));
            Assert.AreEqual(QueryRequestBase.QueryV4, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV4));
            Assert.AreEqual(QueryRequestBase.QueryV3, handler.QueryVersion);
            Assert.IsFalse(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV3));
        }

        [TestMethod]
        public void TestArrayCollectRegrouping()
        {
            var normal = new CollectAggregator(false, false);
            normal.Aggregate(new StringValue("one"));
            Assert.AreEqual(1, normal.Result.AsArrayValue.Count);
            Assert.AreEqual("one", normal.Result.AsArrayValue[0].AsString);

            var regrouped = new CollectAggregator(false, true);
            regrouped.Aggregate(new ArrayValue
            {
                new StringValue("one"), new StringValue("two")
            });
            Assert.AreEqual(2, regrouped.Result.AsArrayValue.Count);
            Assert.AreEqual("one", regrouped.Result.AsArrayValue[0].AsString);
            Assert.AreEqual("two", regrouped.Result.AsArrayValue[1].AsString);
        }
    }
}
