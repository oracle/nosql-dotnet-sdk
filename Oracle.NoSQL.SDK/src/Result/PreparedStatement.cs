/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;
    using Query;
    using static ValidateUtils;

    internal class TopologyInfo
    {
        internal int SequenceNumber { get; }

        internal int[] ShardIds { get; }

        internal TopologyInfo(int sequenceNumber, int[] shardIds)
        {
            SequenceNumber = sequenceNumber;
            ShardIds = shardIds;
        }
    }

    /// <summary>
    /// Represents a prepared query statement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An object of this class is returned as a result of
    /// <see cref="NoSQLClient.PrepareAsync"/>. It includes state that can be
    /// sent to a server and executed without re-parsing the  query. It also
    /// includes bind variables which may be set for each successive use
    /// of the query. It can be passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// methods for the execution of the query and be reused for multiple
    /// queries, potentially with different values of bind variables.
    /// </para>
    /// <para>
    /// An instance of <see cref="PreparedStatement"/> is thread-safe if bind
    /// variables are <em>not</em> used.  If bind variables are used, it is
    /// not thread-safe.  In this case, you can construct additional instances
    /// of <see cref="PreparedStatement"/> using
    /// <see cref="PreparedStatement.CopyStatement"/> in order to share the
    /// prepared statement among threads.
    /// </para>
    /// </remarks>
    /// <example>
    /// Using prepared queries.
    /// <code>
    /// // Prepare the query.
    /// var preparedStatement = await client.PrepareAsync(
    ///     "DECLARE $id INTEGER; $salary DOUBLE; " +
    ///     "SELECT id, firstName, lastName FROM Employees WHERE " +
    ///     "id &lt;= $id AND salary &lt;= $salary");
    ///
    /// // Set bind variables.
    /// preparedStatement.Variables["$id"] = 1100;
    /// preparedStatement.Variables["$salary"] = 100500;
    ///
    /// // Execute the query.
    /// await foreach(var result in
    ///     client.GetQueryAsyncEnumerable(preparedStatement))
    /// {
    ///     foreach(var row in result.Rows)
    ///     {
    ///         // Display the results.
    ///     }
    /// }
    ///
    /// // Change the value of the bind variable.
    /// preparedStatement.variables["$id"] = 2000;
    ///
    /// // Execute the query again.
    /// await foreach(var result in
    ///     client.GetQueryAsyncEnumerable(preparedStatement))
    /// {
    ///     // .....
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.PrepareAsync"/>
    /// <seealso cref="NoSQLClient.QueryAsync"/>
    /// <seealso cref="NoSQLClient.GetQueryAsyncEnumerable"/>
    public class PreparedStatement : IDataResult
    {
        private TopologyInfo topologyInfo;

        private object statementLock = new object();

        internal IDictionary<string, FieldValue> variables;

        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

        /// <summary>
        /// Cloud Service/Cloud Simulator only.  Gets the capacity consumed by
        /// the call to <see cref="NoSQLClient.PrepareAsync"/> that created
        /// this prepared statement.
        /// </summary>
        /// <value>
        /// Consumed capacity.  For on-premise NoSQL Database, this value is
        /// always <c>null</c>.
        /// </value>
        /// <seealso cref="ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets the SQL text of this prepared statement.
        /// </summary>
        /// <value>
        /// SQL text of this prepared statement.
        /// </value>
        public string SQLText { get; internal set; }

        /// <summary>
        /// Gets the query execution plan printout if it was requested.
        /// </summary>
        /// <value>
        /// The query execution plan printout if
        /// <see cref="NoSQLClient.PrepareAsync"/> was called with
        /// <see cref="PrepareOptions.GetQueryPlan"/> set to <c>true</c>,
        /// otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="PrepareOptions.GetQueryPlan"/>
        public string QueryPlan { get; internal set; }

        /// <summary>
        /// Gets the bind variables.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The bind variables are represented as a dictionary with
        /// <c>string</c> keys and values of type <see cref="FieldValue"/>.
        /// You may use methods of <see cref="IDictionary{TKey,TValue}"/> on
        /// this property together with facilities provided by
        /// <see cref="FieldValue"/> and its subclasses to get, set, add and
        /// remove bind variables.
        /// </para>
        /// <para>
        /// Note that the bind variables are not cleared after a query execution.
        /// If you wish to remove all bind variables, call
        /// <see cref="ICollection{T}.Clear"/> on this property.
        /// </para>
        /// </remarks>
        /// <value>
        /// The collection of bind variables represented as
        /// <see cref="IDictionary{TKey,TValue}"/>.
        /// </value>
        /// <example>
        /// Accessing bind variables.
        /// <code>
        /// // .....
        /// // Clear bind variables after this prepared statement was used
        /// // in a query execution.
        /// preparedStatement.Variables.Clear();
        ///
        /// // Setting variables of different types.
        /// preparedStatement.Variables["$var1"] = 10;
        /// preparedStatement.Variables["$var2"] = "abc";
        /// preparedStatement.Variables["$var3"] = new DateTime(2021, 05, 18);
        ///
        /// // Remove a variable previously set.
        /// preparedStatement.Variables.Remove("$var4");
        /// </code>
        /// </example>
        /// <seealso cref="FieldValue"/>
        public IDictionary<string, FieldValue> Variables =>
            variables ??= new Dictionary<string, FieldValue>();

        internal byte[] ProxyStatement { get; set; }

        internal PlanStep DriverQueryPlan { get; set; }

        internal int RegisterCount { get; set; }

        internal string[] VariableNames { get; set; }

        internal TopologyInfo TopologyInfo
        {
            get
            {
                lock (statementLock)
                {
                    return topologyInfo;
                }
            }
        }

        internal void SetTopologyInfo(TopologyInfo info)
        {
            lock (statementLock)
            {
                if (topologyInfo == null ||
                    (info != null && info.SequenceNumber >
                        topologyInfo.SequenceNumber))
                {
                    topologyInfo = info;
                }
            }
        }

        internal void Validate()
        {
            // Currently proxy returns BadProtocolException on these, so
            // we check them here so that ArgumentException can be thrown.
            if (variables != null)
            {
                foreach (var name in variables.Keys)
                {
                    CheckNotNullOrEmpty(name, "Variable");
                }
            }
        }

        /// <summary>
        /// Returns a copy of this prepared statement without its variables.
        /// </summary>
        /// <remarks>
        /// This method returns a new instance of
        /// <see cref="PreparedStatement"/> that shares this object's prepared
        /// query, which is immutable, but does not share its variables.  Use
        /// this method when you need to execute the same prepared query in
        /// different threads (call this method to create a new copy for each
        /// additional thread).
        /// </remarks>
        /// <returns>A copy of this prepared statement without its variables.
        /// </returns>
        public PreparedStatement CopyStatement()
        {
            lock (statementLock)
            {
                var result = (PreparedStatement)MemberwiseClone();
                result.statementLock = new object();
                result.variables = null;
                return result;
            }
        }
    }

}
