/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    internal static class DateTimeUtils
    {
        internal static long GetUnixMillis(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - DateTime.UnixEpoch)
                .TotalMilliseconds;
        }

        internal static DateTime UnixMillisToDateTime(long millis)
        {
            return DateTime.UnixEpoch + TimeSpan.FromMilliseconds(millis);
        }

    }

}
