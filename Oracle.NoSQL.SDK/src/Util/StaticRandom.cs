/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    internal static class StaticRandom
    {
        private static readonly Random Random = new Random();

        internal static int Next()
        {
            lock (Random)
            {
                return Random.Next();
            }
        }

        internal static int Next(int maxValue)
        {
            return Next() % maxValue;
        }
    }

}
