/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Represents options for table DDL passed to methods
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>,
    /// <see cref="NoSQLClient.SetTableLimitsAsync"/>,
    /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>,
    /// <see cref="NoSQLClient.SetTableTagsAsync"/> and
    /// <see cref="NoSQLClient.SetTableTagsWithCompletionAsync"/>,
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <example>
    /// Executing table DDL with provided <see cref="TableDDLOptions"/>.
    /// <code>
    /// var result = await client.ExecuteTableDDLWithCompletionAsync(
    ///     "CREATE TABLE MyTable(id LONG, name STRING, PRIMARY KEY(id)",
    ///     new TableDDLOptions
    ///     {
    ///         Compartment = "my_compartment",
    ///         Timeout = TimeSpan.FromSeconds(30),
    ///         TableLimits = new TableLimits(100, 200, 100)
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    public class TableDDLOptions : ITableCompletionOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Namespace"/>
        public string Namespace
        {
            get => Compartment;
            set => Compartment = value;
        }

        /// <summary>
        /// Gets or sets the timeout for the operation.
        /// </summary>
        /// <remarks>
        /// For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// and
        /// <see cref="NoSQLClient.SetTableLimitsAsync"/> it defaults to
        /// <see cref="NoSQLConfig.TableDDLTimeout"/>.  For
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// and
        /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
        /// separate default timeouts are used for issuing the DDL operation
        /// and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// </remarks>
        /// <value>
        /// Operation timeout.  If set, must be a positive value.
        /// </value>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Cloud Service/Cloud Simulator only.  Gets or sets the table limits
        /// for the operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Valid only for <em>CREATE TABLE</em> operation
        /// executed via
        /// <see cref="NoSQLClient.ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        /// or
        /// <see cref="NoSQLClient.ExecuteTableDDLWithCompletionAsync(string, TableDDLOptions, CancellationToken)"/>.
        /// For <see cref="NoSQLClient.SetTableLimitsAsync"/> or
        /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>, supply
        /// table limits as separate parameter.
        /// </para>
        /// <para>
        /// Note that you may not specify table limits when creating a child
        /// table, because child/descendant tables share the table limits with
        /// their parent/top level ancestor table.
        /// </para>
        /// </remarks>
        /// <value>
        /// The table limits.
        /// </value>
        /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
        public TableLimits TableLimits { get; set; }

        /// <summary>
        /// Gets or sets the poll delay for polling when asynchronously
        /// waiting for operation completion.
        /// </summary>
        /// <remarks>
        /// Applies only to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// and <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
        /// methods.  Defaults to <see cref="NoSQLConfig.TablePollDelay"/>
        /// </remarks>
        /// <value>
        /// Poll delay.  If set, must be a positive value and not greater than
        /// the timeout.
        /// </value>
        public TimeSpan? PollDelay { get; set; }

        /// <summary>
        /// Cloud Service only.
        /// Gets or sets the entity tag that must be matched for operation to
        /// proceed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ETag is an opaque value that represents the current version of
        /// the table itself and can be used in table modification operations
        /// to only perform them if the ETag for the table has not changed.
        /// This is an optimistic concurrency control mechanism allowing
        /// an application to ensure no unexpected modifications have been
        /// made to the table.
        /// </para>
        /// <para>
        /// The value of the ETag must be the value returned in a previous
        /// <see cref="TableResult"></see>.  If set for on-premises service,
        /// the ETag is silently ignored.
        /// </para>
        /// </remarks>
        /// <value>The value of the entity tag for the table.</value>
        /// <seealso cref="TableResult.ETag"/>
        public string MatchETag { get; set; }

        /// <summary>
        /// Cloud Service Only.
        /// Gets or sets defined tags to use for the operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// See chapter <em>Tagging Overview</em> in Oracle Cloud
        /// Infrastructure documentation. Defined tags represent metadata
        /// managed by an administrator.  Users can apply these tags to a
        /// table by identifying the tag and supplying its value.
        /// </para>
        /// <para>
        /// Each defined tag belongs to a namespace, where a namespace
        /// serves as a container for tag keys.  The type of
        /// <see cref="DefinedTags"/> is a compound dictionary, with outer
        /// dictionary keys representing tag namespaces and the inner
        /// dictionary representing tag keys and values for a particular
        /// namespace.
        /// </para>
        /// <para>
        /// Defined tags are used only in these cases: table creation
        /// operations executed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/> or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// with <em>CREATE TABLE</em> SQL statement and table tag
        /// modification operations executed by
        /// <see cref="NoSQLClient.SetTableTagsAsync"/> or
        /// <see cref="NoSQLClient.SetTableTagsWithCompletionAsync"/>.  They
        /// are not used for other table DDL operations.
        /// </para>
        /// </remarks>
        /// <example>
        /// Specifying defined tags.
        /// <code>
        /// var options = new TableDDLOptions
        /// {
        ///     DefinedTags = new Dictionary&lt;string, IDictionary&lt;string, string&gt;&gt;
        ///     {
        ///         ["Oracle-Tags"] = new Dictionary&lt;string, string&gt;
        ///         {
        ///             ["CreatedBy"] = "NosqlUser",
        ///             ["CreatedOn"] = "2023-01-01T00:00:00Z"
        ///         }
        ///     }
        /// };
        /// </code>
        /// </example>
        /// <value>
        /// Namespace-scoped dictionary of defined tags.  If set for an
        /// on-premises service, they are silently ignored.
        /// </value>
        public IDictionary<string, IDictionary<string, string>> DefinedTags
        {
            get;
            internal set;
        }

        /// <summary>
        /// Cloud Service Only.
        /// Gets or sets free-form tags to use for the operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// See chapter <em>Tagging Overview</em> in Oracle Cloud
        /// Infrastructure documentation. Free-form tags represent an
        /// unmanaged metadata created and applied by the user. Free-form tags
        /// do not use namespaces.  <see cref="FreeFormTags"/> is a dictionary
        /// representing tag keys and values.
        /// </para>
        /// <para>
        /// Free-form tags are used only in these cases: table creation
        /// operations executed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/> or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// with <em>CREATE TABLE</em> SQL statement and table tag
        /// modification operations executed by
        /// <see cref="NoSQLClient.SetTableTagsAsync"/> or
        /// <see cref="NoSQLClient.SetTableTagsWithCompletionAsync"/>.  They
        /// are not used for other table DDL operations.
        /// </para>
        /// </remarks>
        /// <example>
        /// Specifying free-form tags.
        /// <code>
        /// var options = new TableDDLOptions
        /// {
        ///     FreeFormTags = new Dictionary&lt;string, string&gt;
        ///     {
        ///         ["createdBy"] = "NosqlUser",
        ///         ["purpose"] = "MyApp"
        ///     }
        /// };
        /// </code>
        /// </example>
        /// <value>
        /// Dictionary of free-form tags.  If set for an on-premises service,
        /// they are silently ignored.
        /// </value>
        public IDictionary<string, string> FreeFormTags { get; internal set; }

        void IOptions.Validate()
        {
            CheckPollParameters(Timeout, PollDelay, nameof(Timeout),
                nameof(PollDelay));
            TableLimits?.Validate();
        }
    }

}
