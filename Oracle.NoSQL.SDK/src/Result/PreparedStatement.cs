/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
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
    /// of <see cref="PreparedStatement"/> using <see cref="CopyStatement"/>
    /// in order to share the prepared statement among threads.
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
    /// preparedStatement
    ///     .SetVariable("$id", 1100)
    ///     .SetVariable("$salary", 100500);
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
    /// preparedStatement.SetVariable("$id", 2000);
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
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
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
        /// This method returns bind variables as a dictionary with
        /// <c>string</c> keys and values of type <see cref="FieldValue"/>.
        /// You may use it to access bind variables and set their values.
        /// This is an alternative to using
        /// <see cref="SetVariable(string,Oracle.NoSQL.SDK.FieldValue)"/>
        /// method.  To set bind variable by its position, use
        /// <see cref="SetVariable(int,Oracle.NoSQL.SDK.FieldValue)"/> method.
        /// </para>
        /// </remarks>
        /// <value>
        /// The collection of bind variables represented as
        /// <see cref="IDictionary{TKey,TValue}"/>.
        /// </value>
        /// <example>
        /// Setting bind variables.
        /// <code>
        /// // Setting variables of different types.
        /// preparedStatement.Variables["$var1"] = 10;
        /// preparedStatement.Variables["$var2"] = "abc";
        /// preparedStatement.Variables["$var3"] = new DateTime(2021, 05, 18);
        /// </code>
        /// </example>
        /// <seealso cref="FieldValue"/>
        public IDictionary<string, FieldValue> Variables =>
            variables ??= new Dictionary<string, FieldValue>();

        /// <summary>
        /// Binds a variable to a given value.  The variable is identified by
        /// its name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The values of bind variables are set as instances of
        /// <see cref="FieldValue"/>.  Thus you may pass different types of
        /// values to this method via using implicit conversions provided by
        /// <see cref="FieldValue"/> (see example below).  This method returns
        /// this instance <see cref="PreparedStatement"/> to enable chaining.
        /// </para>
        /// <para>
        /// Note that the bind variables are not cleared after a query
        /// execution. If you wish to remove all bind variables, call
        /// <see cref="ClearVariables"/> method.
        /// </para>
        /// </remarks>
        /// <param name="name">Name of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>This instance.</returns>
        /// <example>
        /// Setting bind variables by name.
        /// <code>
        /// var preparedStatement = await client.PrepareAsync(
        ///     "SELECT * FROM orders WHERE quantity > $qty AND " +
        ///     "city = $city AND date = $date");
        ///
        /// // Set variables of different types.
        /// preparedStatement
        ///     .SetVariable("$qty", 1000)
        ///     .SetVariable("$city", "New York")
        ///     .SetVariable("$date", new DateTime(2021, 05, 18));
        ///
        /// // Execute the query.
        /// await foreach(var result in
        ///     client.GetQueryAsyncEnumerable(preparedStatement))
        /// {
        ///     // .....
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="FieldValue"/>
        public PreparedStatement SetVariable(string name, FieldValue value)
        {
            Variables[name] = value;
            return this;
        }

        /// <summary>
        /// Binds an external variable to a given value.  The variable is
        /// identified by its position within the query string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is useful for queries where bind variables identified
        /// by "?" are used instead of named variables (but it can be used for
        /// both types of variables).
        /// </para>
        /// <para>
        /// The positions start at <c>1</c>. The variable that appears first
        /// in the query text has position 1, the variable that appears second
        /// has position 2 and so on.
        /// </para>
        /// <para>
        /// If the provided position exceeds the number of variables in the
        /// query string (and thus does not refer to any existing variable in
        /// the query), this method will throw
        /// <exception cref="ArgumentOutOfRangeException"/> if the driver has
        /// access to the variables used in the query (otherwise, such
        /// exception would be thrown when the query is executed).
        /// </para>
        /// </remarks>
        /// <param name="position">The position of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>This instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException"> If position is
        /// negative or zero or greater than the total number of external
        /// variables in the query.</exception>
        /// <example>
        /// Setting bind variables by position.
        /// <code>
        /// var preparedStatement = await client.PrepareAsync(
        ///     "SELECT * FROM users u where u.firstName = ? AND " +
        ///     "u.address.city = ?");
        ///
        /// preparedStatement
        ///     .SetVariable(1, "John")
        ///     .SetVariable(2, "Redwood City");
        ///
        /// // Execute the query.
        /// await foreach(var result in
        ///     client.GetQueryAsyncEnumerable(preparedStatement))
        /// {
        ///     // .....
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="FieldValue"/>
        public PreparedStatement SetVariable(int position, FieldValue value)
        {
            if (position < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Position must be positive");
            }

            if (VariableNames == null)
            {
                return SetVariable("#" + position, value);
            }

            if (position > VariableNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"There is no variable at position {position}");
            }

            return SetVariable(VariableNames[position - 1], value);
        }

        /// <summary>
        /// Clears all bind variables from the statement.
        /// </summary>
        /// <remarks>
        /// This operation is equivalent to calling
        /// <see cref="ICollection{T}.Clear"/> on <see cref="Variables"/>.
        /// </remarks>
        public void ClearVariables() => Variables.Clear();

        internal byte[] ProxyStatement { get; set; }

        internal PlanStep DriverQueryPlan { get; set; }

        internal int RegisterCount { get; set; }

        internal string[] VariableNames { get; set; }

        // The following 3 properties are read from the binary form of
        // prepared statement (ProxyStatement) that is normally opaque.  They
        // are currently used for rate limiting.

        internal string Namespace { get; set; }

        internal string TableName { get; set; }

        internal sbyte OperationCode { get; set; }

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
