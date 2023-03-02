/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;
    using static Utils;

    [TestClass]
    public class NsonTests : TestBase
    {
        private static readonly (string nson, FieldValue value)[]
            NsonCompatData =
        {
            // ReSharper disable StringLiteralTypo
            (
                @"BgAAAlEAAAAZiGludF92YWx1ZQT5BFmGaW50X21heAT7f///hoZpbnRfbWlu
BASAAAB3iWxvbmdfdmFsdWUF/By+mRmbh2xvbmdfbWF4Bf9/////////hodsb25nX21pbgUAgAAAAA
AAAHeLZG91YmxlX3ZhbHVlAz/zwIMSbpeNiWRvdWJsZV9tYXgDf+////////+JZG91YmxlX21pbgMA
AAAAAAAAAYpkb3VibGVfemVybwMAAAAAAAAAAIlkb3VibGVfTmFOA3/4AAAAAAAAi251bWJlcl92YW
x1ZQmJMjE0NzQ4MzY0N4tzdHJpbmdfdmFsdWUHhmFiY2RlZmeJdGltZV92YWx1ZQiaMjAxNy0wNy0x
NVQxNToxODo1OS4xMjM0NTZainRpbWVfdmFsdWUxCJoyMDE3LTA3LTE1VDE1OjE4OjU5LjEyMzQ1Nl
qKdGltZV92YWx1ZTIIlTE5MjctMDctMDVUMTU6MDg6MDkuMVqKdGltZV92YWx1ZTMIkzE5MjctMDct
MDVUMDA6MDA6MDBainRpbWVfdmFsdWU0CJMxOTI3LTA3LTA1VDAwOjAwOjAwWol0cnVlX3ZhbHVlAg
GKZmFsc2VfdmFsdWUCAIludWxsX3ZhbHVlC4plbXB0eV92YWx1ZQyLYmluYXJ5X3ZhbHVlAZthYmNk
ZWZnQUJDREVGR2FiY2RlZmdBQkNERUZHiG1hcF92YWx1ZQYAAAAPAAAAAoBhBICAYgeCZGVmimFycm
F5X3ZhbHVlAAAAAA4AAAAFBIAEgQSCBIMEhA==",
                new RecordValue
                {
                    ["int_value"] = 1234,
                    ["int_max"] = 2147483647,
                    ["int_min"] = -2147483648,
                    ["long_value"] = 123456789012,
                    ["long_max"] = 9223372036854775807,
                    ["long_min"] = -9223372036854775808,
                    ["double_value"] = 1.2345,
                    ["double_max"] = 1.7976931348623157E308,
                    ["double_min"] = 4.9E-324,
                    ["double_zero"] = 0.0,
                    ["double_NaN"] = double.NaN,
                    ["number_value"] = 2147483647m,
                    ["string_value"] = "abcdefg",
                    ["time_value"] = DateTime.Parse(
                        "2017-07-15T15:18:59.123456Z").ToUniversalTime(),
                    ["time_value1"] = DateTime.Parse(
                        "2017-07-15T15:18:59.123456Z").ToUniversalTime(),
                    ["time_value2"] = DateTime.Parse(
                        "1927-07-05T15:08:09.1Z").ToUniversalTime(),
                    ["time_value3"] = DateTime.Parse(
                        "1927-07-05T00:00:00Z").ToUniversalTime(),
                    ["time_value4"] = DateTime.Parse(
                        "1927-07-05T00:00:00Z").ToUniversalTime(),
                    ["true_value"] = true,
                    ["false_value"] = false,
                    ["null_value"] = FieldValue.Null,
                    ["empty_value"] = FieldValue.Empty,
                    ["binary_value"] = Convert.FromBase64String(
                        "YWJjZGVmZ0FCQ0RFRkdhYmNkZWZnQUJDREVGRw=="),
                    ["map_value"] = new RecordValue
                    {
                        ["a"] = 1,
                        ["b"] = "def"
                    },
                    ["array_value"] = new ArrayValue { 1, 2, 3, 4, 5 }
                }
            )
            // ReSharper restore StringLiteralTypo
        };

        private static IEnumerable<object[]> NsonCompatDataSource =>
            from data in NsonCompatData
            select new object[] { data.nson, data.value };

        public static string GetNsonCompatDisplayNames(
            MethodInfo methodInfo, object[] values)
        {
            var nson = (string)values[0];
            return nson[0..32];
        }

        [DataTestMethod]
        [DynamicData(nameof(NsonCompatDataSource),
            DynamicDataDisplayName = nameof(GetNsonCompatDisplayNames))]
        public void TestNsonJsonCompatibility(string nson, FieldValue value)
        {
            nson = Regex.Replace(nson, @"\s", "");
            var nsonBytes = Convert.FromBase64String(nson);
            // We have to create MemoryStream with visible buffer.
            var nr = new NsonReader(new MemoryStream(nsonBytes, 0,
                nsonBytes.Length, false, true));

            nr.Next();
            var valueFromNson = Protocol.ReadFieldValue(nr);

            // This is even better than FieldValue.Equals and ensures all
            // types are the same.
            AssertDeepEqual(value, valueFromNson);

            var ms = new MemoryStream();
            Protocol.WriteFieldValue(new NsonWriter(ms), valueFromNson);
            var valueToNson = Convert.ToBase64String(
                ms.GetBuffer()[..(Index)ms.Position]);
            Assert.AreEqual(nson, valueToNson);
        }

    }

}
