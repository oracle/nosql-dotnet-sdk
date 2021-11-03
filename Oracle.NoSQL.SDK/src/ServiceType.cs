/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Specifies the type of Oracle NoSQL service that is used by
    /// a <see cref="NoSQLClient"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently there are 3 supported types of service: Oracle NoSQL Cloud
    /// Service, On-premise KVStore and Cloud Simulator.  The service type is
    /// heavily linked to an authorization type used by the service indicated
    /// by <see cref="NoSQLConfig.AuthorizationProvider"/> property. If
    /// specifying both <see cref="NoSQLConfig.ServiceType"/> and
    /// <see cref="NoSQLConfig.AuthorizationProvider"/>, they must be
    /// compatible, e.g. you may not specify
    /// <see cref="NoSQLConfig.ServiceType"/> as <see cref="Cloud"/> and
    /// using <see cref="KVStoreAuthorizationProvider"/>.
    /// </para>
    /// <para>
    /// For service types <see cref="Cloud"/> and <see cref="KVStore"/> is is
    /// possible to specify a custom authorization provider as an instance of
    /// a class implementing <see cref="IAuthorizationProvider"/> interface.
    /// </para>
    /// <para>
    /// Although it is advisable to specify the service type as
    /// <see cref="NoSQLConfig.ServiceType"/> when creating
    /// <see cref="NoSQLClient"/> instance, the driver may also be able to
    /// determine the service type from the authorization type as follows:
    /// <list type="number">
    /// <item>
    /// <description>
    /// If <see cref="NoSQLConfig.AuthorizationProvider"/> is not set, then
    /// the service type is <see cref="Cloud"/> if
    /// <see cref="NoSQLConfig.Region"/> is set, otherwise it is
    /// <see cref="CloudSim"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="NoSQLConfig.AuthorizationProvider"/> is an instance of
    /// <see cref="IAMAuthorizationProvider"/> or its subclass, the service
    /// type is <see cref="Cloud"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="NoSQLConfig.AuthorizationProvider"/> is an instance of
    /// <see cref="KVStoreAuthorizationProvider"/> or its subclass, the
    /// service type is <see cref="KVStore"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If none of the above, an <see cref="ArgumentException"/> is thrown.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public enum ServiceType
    {
        /// <summary>
        /// The service type is not specified.  This is the default value if
        /// you don't specify <see cref="NoSQLConfig.ServiceType"/>, in which
        /// case the driver will try to determine the service type as
        /// described.  If the service type cannot be determined, an
        /// <see cref="ArgumentException"/> is thrown.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Cloud Simulator.  No authorization required.
        /// </summary>
        CloudSim = 1,

        /// <summary>
        /// Oracle NoSQL Cloud Service.  Authorization is managed by IAM.  You
        /// must set <see cref="NoSQLConfig.AuthorizationProvider"/> to an
        /// instance of <see cref="IAMAuthorizationProvider"/> unless using
        /// OCI configuration file with the specified region (see
        /// <see cref="NoSQLClient(NoSQLConfig)"/>), in which case the driver
        /// will instantiate a default instance of
        /// <see cref="IAMAuthorizationProvider"/>.
        /// </summary>
        Cloud = 2,

        /// <summary>
        /// On-Premise Oracle NoSQL Database.  This includes both secure and
        /// non-secure stores.  For secure store, the authorization is
        /// required and <see cref="NoSQLConfig.AuthorizationProvider"/> must
        /// be set to an instance of
        /// <see cref="KVStoreAuthorizationProvider"/>.
        /// For non-secure store, authorization is not required but you must
        /// set, <see cref="NoSQLConfig.ServiceType"/> to
        /// <see cref="KVStore"/>.
        /// </summary>
        KVStore = 3
    }

}
