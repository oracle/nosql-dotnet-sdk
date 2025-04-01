/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    // Names of regions are the same as in OCI C# SDK.

    /// <summary>
    /// Cloud Service only.  Represents the region in the Oracle Cloud
    /// Infrastructure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The class contains the regions in the Oracle Cloud Infrastructure at
    /// the time of this release.  The Oracle NoSQL Database Cloud Service is
    /// not available in all of these regions.  For a definitive list of regions
    /// in which the Oracle NoSQL Database Cloud Service is available see
    /// <see href="https://www.oracle.com/cloud/data-regions.html">
    /// Data Regions for Platform and Infrastructure Services
    /// </see>.
    /// </para>
    /// <para>
    /// The applications cannot construct instances of this class and instead
    /// should use one of predefined read-only instances such as
    /// <see cref="Region.AP_MUMBAI_1"/>, <see cref="Region.US_PHOENIX_1"/>,
    /// etc.
    /// </para>
    /// <para>
    /// The <see cref="Region"/> instance can be provided as
    /// <see cref="NoSQLConfig.Region"/> property when creating
    /// <see cref="NoSQLClient"/> instance.  The region determines the
    /// endpoint with the <see cref="NoSQLClient"/> instance will
    /// communicate.  Note that you may not specify both
    /// <see cref="NoSQLConfig.Region"/> and
    /// <see cref="NoSQLConfig.Endpoint"/> properties.
    /// </para>
    /// <para>
    /// The string-based endpoints associated with the regions for the Oracle
    /// NoSQL Database Cloud Service are in the format
    /// <em>https://nosql.{regionId}.oci.{secondLevelDomain}</em>.
    /// </para>
    /// <para>
    /// The region id is defined for each region and is available via
    /// <see cref="RegionId"/> property.  The value of the region id can be
    /// also determined from its field name by lower-casing it and replacing
    /// <em>_</em> with <em>-</em>.  For example, the region id for
    /// <see cref="Region.AP_SEOUL_1"/> is <em>ap-seoul-1</em>.
    /// </para>
    /// <para>
    /// The examples of known second level
    /// domains include:
    /// <list type="bullet">
    /// <item><description><em>oraclecloud.com</em></description></item>
    /// <item><description><em>oraclegovcloud.com</em></description></item>
    /// <item><description><em>oraclegovcloud.uk</em></description></item>
    /// <item><description><em>oraclecloud8.com</em></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For example, the endpoint for <see cref="Region.US_ASHBURN_1"/> region
    /// would be <em>https://nosql.us-ashburn-1.oci.oraclecloud.com</em>.
    /// </para>
    /// <para>
    /// If the Oracle NoSQL Database Cloud Service becomes available in a
    /// region not yet defined here, you may connect to that region by using
    /// its endpoint constructed by the rules above and setting it as
    /// <see cref="NoSQLConfig.Endpoint"/>.
    /// </para>
    /// <para>
    /// For more information about Oracle Cloud Infrastructure regions see
    /// <see href="https://docs.cloud.oracle.com/en-us/iaas/Content/General/Concepts/regions.htm">
    /// Regions and Availability Domains
    /// </see>
    /// </para>
    /// </remarks>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Region
    {
        private class Realm
        {
            internal Realm(string realmId, string secondLevelDomain)
            {
                RealmId = realmId;
                SecondLevelDomain = secondLevelDomain;
            }

            internal string RealmId { get; }

            internal string SecondLevelDomain { get; }
        }

        // === Begin autogenerated realms ===
        private static readonly Realm OC1 = new Realm("oc1",
            "oraclecloud.com");

        private static readonly Realm OC2 = new Realm("oc2",
            "oraclegovcloud.com");

        private static readonly Realm OC3 = new Realm("oc3",
            "oraclegovcloud.com");

        private static readonly Realm OC4 = new Realm("oc4",
            "oraclegovcloud.uk");

        private static readonly Realm OC5 = new Realm("oc5",
            "oraclecloud5.com");

        private static readonly Realm OC8 = new Realm("oc8",
            "oraclecloud8.com");

        private static readonly Realm OC9 = new Realm("oc9",
            "oraclecloud9.com");

        private static readonly Realm OC10 = new Realm("oc10",
            "oraclecloud10.com");

        private static readonly Realm OC14 = new Realm("oc14",
            "oraclecloud14.com");

        private static readonly Realm OC15 = new Realm("oc15",
            "oraclecloud15.com");

        private static readonly Realm OC16 = new Realm("oc16",
            "oraclecloud16.com");

        private static readonly Realm OC17 = new Realm("oc17",
            "oraclecloud17.com");

        private static readonly Realm OC19 = new Realm("oc19",
            "oraclecloud.eu");

        private static readonly Realm OC20 = new Realm("oc20",
            "oraclecloud20.com");

        private static readonly Realm OC21 = new Realm("oc21",
            "oraclecloud21.com");

        private static readonly Realm OC22 = new Realm("oc22",
            "psn-pco.it");

        private static readonly Realm OC23 = new Realm("oc23",
            "oraclecloud23.com");

        private static readonly Realm OC24 = new Realm("oc24",
            "oraclecloud24.com");

        private static readonly Realm OC25 = new Realm("oc25",
            "nricloud.jp");

        private static readonly Realm OC26 = new Realm("oc26",
            "oraclecloud26.com");

        private static readonly Realm OC27 = new Realm("oc27",
            "oraclecloud27.com");

        private static readonly Realm OC28 = new Realm("oc28",
            "oraclecloud28.com");

        private static readonly Realm OC29 = new Realm("oc29",
            "oraclecloud29.com");

        private static readonly Realm OC31 = new Realm("oc31",
            "sovereigncloud.nz");

        private static readonly Realm OC35 = new Realm("oc35",
            "oraclecloud35.com");

        // === End autogenerated realms ===

        private readonly Realm realm;

        private Region(string regionId, string regionCode, Realm realm)
        {
            RegionId = regionId;
            RegionCode = regionCode;
            this.realm = realm;
        }

        /// <summary>
        /// Gets the region id.
        /// </summary>
        /// <value>
        /// The region id as described in the <see cref="Region"/> remarks
        /// section.
        /// </value>
        public string RegionId { get; }

        /// <summary>
        /// Gets the region code.
        /// </summary>
        /// <value>
        /// Region code (which may also be referred to as region key), which
        /// is a 3-letter identifier for each region.  Note that the this
        /// value is in lower case.
        /// </value>
        public string RegionCode { get; }

        /// <summary>
        /// Get the region second level domain.
        /// </summary>
        /// <value>
        /// The second level domain as described in the <see cref="Region"/>
        /// remarks section.
        /// </value>
        public string SecondLevelDomain => realm.SecondLevelDomain;

        /// <summary>
        /// Gets the region endpoint.
        /// </summary>
        /// <value>
        /// The region endpoint constructed as described in the
        /// <see cref="Region"/> remarks section.
        /// </value>
        public string Endpoint =>
            $"https://nosql.{RegionId}.oci.{realm.SecondLevelDomain}";

        /// <summary>
        /// Converts the value of this instance to string.
        /// </summary>
        /// <returns>The region id.</returns>
        public override string ToString() => RegionId;

        /// <summary>
        /// Returns the region associated with the specified region id.
        /// </summary>
        /// <param name="regionId">The region id.</param>
        /// <returns>The region that has its region id equal to
        /// <paramref name="regionId"/>, case-insensitive.</returns>
        /// <exception cref="ArgumentException">If the region with the
        /// specified <paramref name="regionId"/> could not be found.
        /// </exception>
        /// <seealso cref="RegionId"/>
        public static Region FromRegionId(string regionId)
        {
            if (regionId == null)
            {
                throw new ArgumentNullException(nameof(regionId));
            }

            string fieldName = regionId.Replace('-', '_').ToUpper();
            var field = typeof(Region).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);

            if (field == null || field.FieldType != typeof(Region))
            {
                throw new ArgumentException(
                    $"Could not find region with region id {regionId}",
                    nameof(regionId));
            }

            var region = (Region)field.GetValue(null);
            Debug.Assert(region != null);
            return region;
        }

        /// <summary>
        /// Returns the region associated with the specified region code or
        /// region id.
        /// </summary>
        /// <param name="regionCodeOrId">The region code or region id used to
        /// search for the region.</param>
        /// <returns>The region that has its region code or region id equal to
        /// <paramref name="regionCodeOrId"/>, case-insensitive.</returns>
        /// <exception cref="ArgumentException">If the region with the
        /// specified code or id equal to <paramref name="regionCodeOrId"/>
        /// could not be found.
        /// </exception>
        /// <seealso cref="RegionId"/>
        /// <seealso cref="RegionCode"/>
        public static Region FromRegionCodeOrId(string regionCodeOrId)
        {
            var fields = typeof(Region).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(Region))
                {
                    continue;
                }

                var region = (Region)field.GetValue(null);
                Debug.Assert(region != null);

                if (string.Equals(region.RegionCode, regionCodeOrId,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(region.RegionId, regionCodeOrId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return region;
                }
            }

            throw new ArgumentException(
                "Could not find region from region code or region id " +
                regionCodeOrId, nameof(regionCodeOrId));
        }

        /// <summary>
        /// Returns the collection of all regions defined in this class.
        /// </summary>
        /// <value>
        /// All regions defined in this class as a read-only collection.
        /// </value>
        public static IReadOnlyCollection<Region> Values
        {
            get
            {
                var result = new List<Region>();
                var fields = typeof(Region).GetFields(
                    BindingFlags.Public | BindingFlags.Static);

                foreach (var field in fields)
                {
                    if (field.FieldType != typeof(Region))
                    {
                        continue;
                    }

                    var region = (Region)field.GetValue(null);
                    Debug.Assert(region != null);

                    result.Add(region);
                }

                return result;
            }
        }

        // === Begin autogenerated regions ===
        /// <summary>
        /// Realm: OC1, South Africa (Johannesburg)
        /// </summary>
        public static readonly Region AF_JOHANNESBURG_1 = new Region(
            "af-johannesburg-1", "jnb", OC1);

        /// <summary>
        /// Realm: OC1, South Korea North (Chuncheon)
        /// </summary>
        public static readonly Region AP_CHUNCHEON_1 = new Region(
            "ap-chuncheon-1", "yny", OC1);

        /// <summary>
        /// Realm: OC1, India South (Hyderabad)
        /// </summary>
        public static readonly Region AP_HYDERABAD_1 = new Region(
            "ap-hyderabad-1", "hyd", OC1);

        /// <summary>
        /// Realm: OC1, Australia Southeast (Melbourne)
        /// </summary>
        public static readonly Region AP_MELBOURNE_1 = new Region(
            "ap-melbourne-1", "mel", OC1);

        /// <summary>
        /// Realm: OC1, India West (Mumbai)
        /// </summary>
        public static readonly Region AP_MUMBAI_1 = new Region(
            "ap-mumbai-1", "bom", OC1);

        /// <summary>
        /// Realm: OC1, Japan Central (Osaka)
        /// </summary>
        public static readonly Region AP_OSAKA_1 = new Region(
            "ap-osaka-1", "kix", OC1);

        /// <summary>
        /// Realm: OC1, South Korea Central (Seoul)
        /// </summary>
        public static readonly Region AP_SEOUL_1 = new Region(
            "ap-seoul-1", "icn", OC1);

        /// <summary>
        /// Realm: OC1, Singapore (Singapore)
        /// </summary>
        public static readonly Region AP_SINGAPORE_1 = new Region(
            "ap-singapore-1", "sin", OC1);

        /// <summary>
        /// Realm: OC1, Singapore2 (Singapore)
        /// </summary>
        public static readonly Region AP_SINGAPORE_2 = new Region(
            "ap-singapore-2", "xsp", OC1);

        /// <summary>
        /// Realm: OC1, Australia East (Sydney)
        /// </summary>
        public static readonly Region AP_SYDNEY_1 = new Region(
            "ap-sydney-1", "syd", OC1);

        /// <summary>
        /// Realm: OC1, Japan East (Tokyo)
        /// </summary>
        public static readonly Region AP_TOKYO_1 = new Region(
            "ap-tokyo-1", "nrt", OC1);

        /// <summary>
        /// Realm: OC1, Canada Southeast (Montreal)
        /// </summary>
        public static readonly Region CA_MONTREAL_1 = new Region(
            "ca-montreal-1", "yul", OC1);

        /// <summary>
        /// Realm: OC1, Canada Southeast (Toronto)
        /// </summary>
        public static readonly Region CA_TORONTO_1 = new Region(
            "ca-toronto-1", "yyz", OC1);

        /// <summary>
        /// Realm: OC1, Netherlands Northwest (Amsterdam)
        /// </summary>
        public static readonly Region EU_AMSTERDAM_1 = new Region(
            "eu-amsterdam-1", "ams", OC1);

        /// <summary>
        /// Realm: OC1, Germany Central (Frankfurt)
        /// </summary>
        public static readonly Region EU_FRANKFURT_1 = new Region(
            "eu-frankfurt-1", "fra", OC1);

        /// <summary>
        /// Realm: OC1, Spain (Madrid)
        /// </summary>
        public static readonly Region EU_MADRID_1 = new Region(
            "eu-madrid-1", "mad", OC1);

        /// <summary>
        /// Realm: OC1, France (Marseille)
        /// </summary>
        public static readonly Region EU_MARSEILLE_1 = new Region(
            "eu-marseille-1", "mrs", OC1);

        /// <summary>
        /// Realm: OC1, Italy (Milan)
        /// </summary>
        public static readonly Region EU_MILAN_1 = new Region(
            "eu-milan-1", "lin", OC1);

        /// <summary>
        /// Realm: OC1, France (Paris)
        /// </summary>
        public static readonly Region EU_PARIS_1 = new Region(
            "eu-paris-1", "cdg", OC1);

        /// <summary>
        /// Realm: OC1, Sweden (Stockholm)
        /// </summary>
        public static readonly Region EU_STOCKHOLM_1 = new Region(
            "eu-stockholm-1", "arn", OC1);

        /// <summary>
        /// Realm: OC1, Switzerland North (Zurich)
        /// </summary>
        public static readonly Region EU_ZURICH_1 = new Region(
            "eu-zurich-1", "zrh", OC1);

        /// <summary>
        /// Realm: OC1, Israel (Jerusalem)
        /// </summary>
        public static readonly Region IL_JERUSALEM_1 = new Region(
            "il-jerusalem-1", "mtz", OC1);

        /// <summary>
        /// Realm: OC1, UAE (Abu Dhabi)
        /// </summary>
        public static readonly Region ME_ABUDHABI_1 = new Region(
            "me-abudhabi-1", "auh", OC1);

        /// <summary>
        /// Realm: OC1, UAE East (Dubai)
        /// </summary>
        public static readonly Region ME_DUBAI_1 = new Region(
            "me-dubai-1", "dxb", OC1);

        /// <summary>
        /// Realm: OC1, Saudi Arabia West (Jeddah)
        /// </summary>
        public static readonly Region ME_JEDDAH_1 = new Region(
            "me-jeddah-1", "jed", OC1);

        /// <summary>
        /// Realm: OC1, RegionID: me-riyadh-1, RegionKey: RUH
        /// </summary>
        public static readonly Region ME_RIYADH_1 = new Region(
            "me-riyadh-1", "ruh", OC1);

        /// <summary>
        /// Realm: OC1, Mexico (Monterrey)
        /// </summary>
        public static readonly Region MX_MONTERREY_1 = new Region(
            "mx-monterrey-1", "mty", OC1);

        /// <summary>
        /// Realm: OC1, Mexico (Queretaro)
        /// </summary>
        public static readonly Region MX_QUERETARO_1 = new Region(
            "mx-queretaro-1", "qro", OC1);

        /// <summary>
        /// Realm: OC1, Colombia (Bogota)
        /// </summary>
        public static readonly Region SA_BOGOTA_1 = new Region(
            "sa-bogota-1", "bog", OC1);

        /// <summary>
        /// Realm: OC1, Chile (Santiago)
        /// </summary>
        public static readonly Region SA_SANTIAGO_1 = new Region(
            "sa-santiago-1", "scl", OC1);

        /// <summary>
        /// Realm: OC1, Brazil East (Sao Paulo)
        /// </summary>
        public static readonly Region SA_SAOPAULO_1 = new Region(
            "sa-saopaulo-1", "gru", OC1);

        /// <summary>
        /// Realm: OC1, Chile (Valparaiso)
        /// </summary>
        public static readonly Region SA_VALPARAISO_1 = new Region(
            "sa-valparaiso-1", "vap", OC1);

        /// <summary>
        /// Realm: OC1, Brazil (Vinhedo)
        /// </summary>
        public static readonly Region SA_VINHEDO_1 = new Region(
            "sa-vinhedo-1", "vcp", OC1);

        /// <summary>
        /// Realm: OC1, UK South (London)
        /// </summary>
        public static readonly Region UK_LONDON_1 = new Region(
            "uk-london-1", "lhr", OC1);

        /// <summary>
        /// Realm: OC1, UK West (Newport)
        /// </summary>
        public static readonly Region UK_CARDIFF_1 = new Region(
            "uk-cardiff-1", "cwl", OC1);

        /// <summary>
        /// Realm: OC1, US West (Phoenix)
        /// </summary>
        public static readonly Region US_PHOENIX_1 = new Region(
            "us-phoenix-1", "phx", OC1);

        /// <summary>
        /// Realm: OC1, US East (Ashburn)
        /// </summary>
        public static readonly Region US_ASHBURN_1 = new Region(
            "us-ashburn-1", "iad", OC1);

        /// <summary>
        /// Realm: OC1, US West (Salt Lake)
        /// </summary>
        public static readonly Region US_SALTLAKE_2 = new Region(
            "us-saltlake-2", "aga", OC1);

        /// <summary>
        /// Realm: OC1, US West (San Jose)
        /// </summary>
        public static readonly Region US_SANJOSE_1 = new Region(
            "us-sanjose-1", "sjc", OC1);

        /// <summary>
        /// Realm: OC1, US Central (Chicago)
        /// </summary>
        public static readonly Region US_CHICAGO_1 = new Region(
            "us-chicago-1", "ord", OC1);

        /// <summary>
        /// Realm: OC2, US Gov East (Ashburn)
        /// </summary>
        public static readonly Region US_LANGLEY_1 = new Region(
            "us-langley-1", "lfi", OC2);

        /// <summary>
        /// Realm: OC2, US Gov West (Phoenix)
        /// </summary>
        public static readonly Region US_LUKE_1 = new Region(
            "us-luke-1", "luf", OC2);

        /// <summary>
        /// Realm: OC3, US DoD East (Ashburn)
        /// </summary>
        public static readonly Region US_GOV_ASHBURN_1 = new Region(
            "us-gov-ashburn-1", "ric", OC3);

        /// <summary>
        /// Realm: OC3, US DoD North (Chicago)
        /// </summary>
        public static readonly Region US_GOV_CHICAGO_1 = new Region(
            "us-gov-chicago-1", "pia", OC3);

        /// <summary>
        /// Realm: OC3, US DoD West (Phoenix)
        /// </summary>
        public static readonly Region US_GOV_PHOENIX_1 = new Region(
            "us-gov-phoenix-1", "tus", OC3);

        /// <summary>
        /// Realm: OC4, UK Gov South (London)
        /// </summary>
        public static readonly Region UK_GOV_LONDON_1 = new Region(
            "uk-gov-london-1", "ltn", OC4);

        /// <summary>
        /// Realm: OC4, UK Gov West (Cardiff)
        /// </summary>
        public static readonly Region UK_GOV_CARDIFF_1 = new Region(
            "uk-gov-cardiff-1", "brs", OC4);

        /// <summary>
        /// Realm: OC5, US West (Tacoma)
        /// </summary>
        public static readonly Region US_TACOMA_1 = new Region(
            "us-tacoma-1", "tiw", OC5);

        /// <summary>
        /// Realm: OC8, Japan East (Chiyoda)
        /// </summary>
        public static readonly Region AP_CHIYODA_1 = new Region(
            "ap-chiyoda-1", "nja", OC8);

        /// <summary>
        /// Realm: OC8, Japan East (Ibaraki) (Note: OCI uses 'ukb' instead of 'ibr')
        /// </summary>
        public static readonly Region AP_IBARAKI_1 = new Region(
            "ap-ibaraki-1", "ukb", OC8);

        /// <summary>
        /// Realm: OC9, Muscat (Dedicated DataCenter)
        /// </summary>
        public static readonly Region ME_DCC_MUSCAT_1 = new Region(
            "me-dcc-muscat-1", "mct", OC9);

        /// <summary>
        /// Realm: OC10, Canberra (Dedicated DataCenter)
        /// </summary>
        public static readonly Region AP_DCC_CANBERRA_1 = new Region(
            "ap-dcc-canberra-1", "wga", OC10);

        /// <summary>
        /// Realm: OC14, Dublin 1 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_DUBLIN_1 = new Region(
            "eu-dcc-dublin-1", "ork", OC14);

        /// <summary>
        /// Realm: OC14, Dublin 2 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_DUBLIN_2 = new Region(
            "eu-dcc-dublin-2", "snn", OC14);

        /// <summary>
        /// Realm: OC14, Milan 1 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_MILAN_1 = new Region(
            "eu-dcc-milan-1", "bgy", OC14);

        /// <summary>
        /// Realm: OC14, Milan 2 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_MILAN_2 = new Region(
            "eu-dcc-milan-2", "mxp", OC14);

        /// <summary>
        /// Realm: OC14, Rating 1 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_RATING_1 = new Region(
            "eu-dcc-rating-1", "dus", OC14);

        /// <summary>
        /// Realm: OC14, Rating 2 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region EU_DCC_RATING_2 = new Region(
            "eu-dcc-rating-2", "dtm", OC14);

        /// <summary>
        /// Realm: OC15, Bangladesh
        /// </summary>
        public static readonly Region AP_DCC_GAZIPUR_1 = new Region(
            "ap-dcc-gazipur-1", "dac", OC15);

        /// <summary>
        /// Realm: OC16, US West (Utah)
        /// </summary>
        public static readonly Region US_WESTJORDAN_1 = new Region(
            "us-westjordan-1", "sgu", OC16);

        /// <summary>
        /// Realm: OC17, Phoenix 1 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region US_DCC_PHOENIX_1 = new Region(
            "us-dcc-phoenix-1", "ifp", OC17);

        /// <summary>
        /// Realm: OC17, Phoenix 2 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region US_DCC_PHOENIX_2 = new Region(
            "us-dcc-phoenix-2", "gcn", OC17);

        /// <summary>
        /// Realm: OC17, Phoenix 4 (Dedicated DataCenter)
        /// </summary>
        public static readonly Region US_DCC_PHOENIX_4 = new Region(
            "us-dcc-phoenix-4", "yum", OC17);

        /// <summary>
        /// Realm: OC19, Frankfurt (Germany)
        /// </summary>
        public static readonly Region EU_FRANKFURT_2 = new Region(
            "eu-frankfurt-2", "str", OC19);

        /// <summary>
        /// Realm: OC19, Madrid (Spain)
        /// </summary>
        public static readonly Region EU_MADRID_2 = new Region(
            "eu-madrid-2", "vll", OC19);

        /// <summary>
        /// Realm: OC20, Jovanovac (Serbia)
        /// </summary>
        public static readonly Region EU_JOVANOVAC_1 = new Region(
            "eu-jovanovac-1", "beg", OC20);

        /// <summary>
        /// Realm: OC21, Doha (Qatar)
        /// </summary>
        public static readonly Region ME_DCC_DOHA_1 = new Region(
            "me-dcc-doha-1", "doh", OC21);

        /// <summary>
        /// Realm: OC22, Italy dedicated (Rome)
        /// </summary>
        public static readonly Region EU_DCC_ROME_1 = new Region(
            "eu-dcc-rome-1", "nap", OC22);

        /// <summary>
        /// Realm: OC23, RegionID: us-somerset-1, RegionKey: EBB
        /// </summary>
        public static readonly Region US_SOMERSET_1 = new Region(
            "us-somerset-1", "ebb", OC23);

        /// <summary>
        /// Realm: OC23, RegionID: us-thames-1, RegionKey: EBL
        /// </summary>
        public static readonly Region US_THAMES_1 = new Region(
            "us-thames-1", "ebl", OC23);

        /// <summary>
        /// Realm: OC24, Switzerland dedicated (Zurich)
        /// </summary>
        public static readonly Region EU_DCC_ZURICH_1 = new Region(
            "eu-dcc-zurich-1", "avz", OC24);

        /// <summary>
        /// Realm: OC24, RegionID: eu-crissier-1, RegionKey: AVF
        /// </summary>
        public static readonly Region EU_CRISSIER_1 = new Region(
            "eu-crissier-1", "avf", OC24);

        /// <summary>
        /// Realm: OC25, Japan dedicated (Osaka)
        /// </summary>
        public static readonly Region AP_DCC_OSAKA_1 = new Region(
            "ap-dcc-osaka-1", "uky", OC25);

        /// <summary>
        /// Realm: OC25, Japan dedicated (Tokyo)
        /// </summary>
        public static readonly Region AP_DCC_TOKYO_1 = new Region(
            "ap-dcc-tokyo-1", "tyo", OC25);

        /// <summary>
        /// Realm: OC26, Abudhabi
        /// </summary>
        public static readonly Region ME_ABUDHABI_3 = new Region(
            "me-abudhabi-3", "ahu", OC26);

        /// <summary>
        /// Realm: OC26, RegionID: me-alain-1, RegionKey: RBA
        /// </summary>
        public static readonly Region ME_ALAIN_1 = new Region(
            "me-alain-1", "rba", OC26);

        /// <summary>
        /// Realm: OC27, SWJordan dedicated (Utah USA)
        /// </summary>
        public static readonly Region US_DCC_SWJORDAN_1 = new Region(
            "us-dcc-swjordan-1", "ozz", OC27);

        /// <summary>
        /// Realm: OC28, SWJordan dedicated (Utah USA)
        /// </summary>
        public static readonly Region US_DCC_SWJORDAN_2 = new Region(
            "us-dcc-swjordan-2", "drs", OC28);

        /// <summary>
        /// Realm: OC29, Abudhabi 2
        /// </summary>
        public static readonly Region ME_ABUDHABI_2 = new Region(
            "me-abudhabi-2", "rkt", OC29);

        /// <summary>
        /// Realm: OC29, Abudhabi 4
        /// </summary>
        public static readonly Region ME_ABUDHABI_4 = new Region(
            "me-abudhabi-4", "shj", OC29);

        /// <summary>
        /// Realm: OC31, Hobsonville dedicated (New Zealand)
        /// </summary>
        public static readonly Region AP_HOBSONVILLE_1 = new Region(
            "ap-hobsonville-1", "izq", OC31);

        /// <summary>
        /// Realm: OC35, RegionID: ap-suwon-1, RegionKey: DLN
        /// </summary>
        public static readonly Region AP_SUWON_1 = new Region(
            "ap-suwon-1", "dln", OC35);

        // === End autogenerated regions ===

    }
}
