/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RowMetadataValidationTests
    {
        private static readonly string[] ValidMetadata =
        {
            "{}",
            "{\"user\":\"alice\"}",
            "[1, true, null]",
            "\"value\"",
            "123",
            "true",
            "false",
            "null"
        };

        private static readonly string[] InvalidMetadata =
        {
            "",
            " ",
            "custom metadata",
            "{'a':1}",
            "{\"a\":1}{\"b\":2}",
            "{}[]",
            "\"abc\"\"def\""
        };

        private static readonly Func<string, IOptions>[] OptionsFactories =
        {
            metadata => new PutOptions { LastWriteMetadata = metadata },
            metadata => new DeleteOptions { LastWriteMetadata = metadata },
            metadata => new DeleteRangeOptions { LastWriteMetadata = metadata },
            metadata => new QueryOptions { LastWriteMetadata = metadata }
        };

        [TestMethod]
        public void TestValidRowMetadata()
        {
            foreach (var metadata in ValidMetadata)
            {
                foreach (var createOptions in OptionsFactories)
                {
                    createOptions(metadata).Validate();
                }
            }
        }

        [TestMethod]
        public void TestInvalidRowMetadata()
        {
            foreach (var metadata in InvalidMetadata)
            {
                foreach (var createOptions in OptionsFactories)
                {
                    Assert.ThrowsException<ArgumentException>(() =>
                        createOptions(metadata).Validate());
                }
            }
        }
    }
}
