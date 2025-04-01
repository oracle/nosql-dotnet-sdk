/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    internal static class ValidateUtils
    {
        private static void ThrowMustBePositive(string name)
        {
            throw new ArgumentOutOfRangeException(name,
                $"{name} must be positive");
        }

        private static void ThrowMustBeNonNegative(string name)
        {
            throw new ArgumentOutOfRangeException(name,
                $"{name} may not be negative");
        }

        internal static void CheckNotNull<T>(T value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        internal static void CheckNotNullOrEmpty(string value, string name)
        {
            CheckNotNull(value, name);
            if (value.Length == 0)
            {
                throw new ArgumentException(
                    $"{name} must be non-empty string");
            }
        }

        internal static void CheckPositiveInt32(int? value, string name)
        {
            if (value.HasValue && value.Value <= 0)
            {
                ThrowMustBePositive(name);
            }
        }

        internal static void CheckPositiveInt64(long value, string name)
        {
            if (value <= 0)
            {
                ThrowMustBePositive(name);
            }
        }

        internal static void CheckNonNegativeInt32(int? value, string name)
        {
            if (value.HasValue && value.Value < 0)
            {
                ThrowMustBeNonNegative(name);
            }
        }

        internal static void CheckNotAboveLimit<T>(T? value, T limit, string name)
            where T : struct, IComparable<T>
        {
            if (value.HasValue && value.Value.CompareTo(limit) > 0)
            {
                throw new ArgumentException(
                    $"{name} value {value.Value} may not exceed " +
                    $"limit {limit}");
            }
        }

        internal static void CheckPositiveTimeSpan(TimeSpan value,
            string name)
        {
            if (value <= TimeSpan.Zero)
            {
                ThrowMustBePositive(name);
            }
        }

        internal static void CheckPositiveTimeSpan(TimeSpan? value,
            string name)
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
            {
                ThrowMustBePositive(name);
            }
        }

        internal static void CheckTimeout(TimeSpan timeout,
            string name = "Timeout")
        {
            CheckPositiveTimeSpan(timeout, name);
        }

        internal static void CheckTimeout(TimeSpan? timeout,
            string name = "Timeout")
        {
            CheckPositiveTimeSpan(timeout, name);
        }

        internal static void CheckPollParameters(TimeSpan? timeout,
            TimeSpan? pollDelay, string timeoutName, string delayName)
        {
            CheckPositiveTimeSpan(timeout, timeoutName);
            CheckPositiveTimeSpan(pollDelay, delayName);

            if (timeout.HasValue && pollDelay.HasValue &&
                pollDelay.Value > timeout.Value)
            {
                throw new ArgumentException(
                    $"{delayName} cannot be greater than {timeoutName}");
            }
        }

        internal static bool IsEnumDefined<T>(T value) where T : struct, Enum
        {
            // Non-generic IsDefined method has bad performance so we should
            // use generic one when available.
#if NET5_0_OR_GREATER
            return Enum.IsDefined<T>(value);
#else
            return Enum.IsDefined(typeof(T), value);
#endif
        }

        internal static void CheckEnumValue<T>(T value) where T : struct, Enum
        {
            if (!IsEnumDefined(value))
            {
                throw new ArgumentException(
                    $"Invalid value for {typeof(T).Name}: {value}");
            }
        }

        internal static void CheckEnumValue<T>(T? value) where T : struct,
            Enum
        {
            if (value.HasValue)
            {
                CheckEnumValue(value.Value);
            }
        }

        internal static void CheckReceivedEnumValue<T>(T value) where T :
            struct, Enum
        {
            if (!IsEnumDefined(value))
            {
                throw new BadProtocolException(
                    $"Received invalid value for {typeof(T).Name}: {value}");
            }
        }
        
        internal static void CheckTableName(string tableName)
        {
            CheckNotNullOrEmpty(tableName, "table name");
        }
    }

}
