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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public partial class RateLimitingTests
    {
        private static readonly Random SeedRandom = new Random();

        private static int GetRandomSeed()
        {
            lock (SeedRandom)
            {
                return SeedRandom.Next();
            }
        }

        public class SimpleTestCase
        {
            internal double Limit { get; set; }
            internal int Seconds { get; set; }
            internal int MinUnits { get; set; }
            internal int MaxUnits { get; set; }
            internal TimeSpan Timeout { get; set; }
            internal bool ConsumeOnTimeout { get; set; }
            internal CancellationToken CancellationToken { get; set; }
            internal int LoopCount { get; set; } = 1;
        }

        private static async Task<(long, TimeSpan)> SimpleLoopAsync(
            IRateLimiter rateLimiter, SimpleTestCase testCase)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(
                testCase.Seconds);
            var random = new Random(GetRandomSeed());
            long totalUnits = 0;
            var totalDelay = TimeSpan.Zero;

            do
            {
                var units = random.Next(testCase.MinUnits,
                    testCase.MaxUnits + 1); // inclusive upper-bound
                var delay = await rateLimiter.ConsumeUnitsAsync(units,
                    testCase.Timeout, testCase.ConsumeOnTimeout,
                    testCase.CancellationToken);
                totalUnits += units;
                totalDelay += delay;
            } while(DateTime.UtcNow < endTime);

            return (totalUnits, totalDelay);
        }

        public class DriverLikeTestCase : SimpleTestCase
        {
            internal int MinOpMillis { get; set; }
            internal int MaxOpMillis { get; set; }
        }

        private static async Task<(long, TimeSpan)> DriverLikeLoopAsync(
            IRateLimiter rateLimiter, DriverLikeTestCase testCase)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(
                testCase.Seconds);
            var random = new Random(GetRandomSeed());
            long totalUnits = 0;
            var totalDelay = TimeSpan.Zero;

            do
            {
                totalDelay += await rateLimiter.ConsumeUnitsAsync(0,
                    testCase.Timeout, false, testCase.CancellationToken);
                var opMillis = random.Next(testCase.MinOpMillis,
                    testCase.MaxOpMillis + 1);
                await Task.Delay(opMillis);
                var units = random.Next(testCase.MinUnits,
                    testCase.MaxUnits + 1);
                totalDelay += await rateLimiter.ConsumeUnitsAsync(units,
                    testCase.Timeout - TimeSpan.FromMilliseconds(opMillis),
                    true, testCase.CancellationToken);
                totalUnits += units;
            } while(DateTime.UtcNow < endTime);

            return (totalUnits, totalDelay);
        }

        private const int BurstSeconds = 1;
        private const double LoopTestMaxDelta = 0.05;

        private static async Task TestLoopAsync<TTestCase>(
            Func<IRateLimiter, TTestCase, Task<(long, TimeSpan)>> loop,
            TTestCase testCase) where TTestCase : SimpleTestCase
        {
            var rateLimiter = new NoSQLRateLimiter(
                TimeSpan.FromSeconds(BurstSeconds));
            rateLimiter.SetLimit(testCase.Limit);
            var startTime = DateTime.UtcNow;

            var tasks = from _ in Enumerable.Range(0, testCase.LoopCount)
                select Task.Run(() => loop(rateLimiter, testCase));
            var results = await Task.WhenAll(tasks);

            var totalTime = DateTime.UtcNow - startTime;
            var totalUnits = results.Select(result => result.Item1).Sum();
            var totalDelayMillis = results.Select(
                result => result.Item2.TotalMilliseconds).Sum();
            var unitsPerSecond = totalUnits / totalTime.TotalSeconds;

            Debug.WriteLine(
                $"Total time: {totalTime.TotalMilliseconds} ms, " +
                $"Total units: {totalUnits}, Units per second: " +
                $"{unitsPerSecond}, total delay: {totalDelayMillis} ms");
            Assert.IsTrue(
                Math.Abs(unitsPerSecond - testCase.Limit) / testCase.Limit <=
                LoopTestMaxDelta);
        }

        private static readonly SimpleTestCase[] SimpleTestCases =
        {
            new SimpleTestCase
            {
                Limit = 100,
                Seconds = 5,
                MinUnits = 0,
                MaxUnits = 5,
                Timeout = TimeSpan.FromSeconds(10),
                LoopCount = 3
            }
        };

        private static readonly DriverLikeTestCase[] DriverLikeTestCases =
        {
            new DriverLikeTestCase
            {
                Limit = 50,
                Seconds = 30,
                MinUnits = 0,
                MaxUnits = 5,
                MinOpMillis = 50,
                MaxOpMillis = 100,
                Timeout = TimeSpan.FromSeconds(10),
                LoopCount = 8
            }
        };

        private static IEnumerable<object[]> SimpleLoopDataSource =>
            from testCase in SimpleTestCases
            select new object[] { testCase };

        private static IEnumerable<object[]> DriverLikeLoopDataSource =>
            from testCase in DriverLikeTestCases
            select new object[] { testCase };

        // For some reason MSTest executes tests even if ClassInitialize
        // fails. Instead we disable them here if we are running on-prem.
        [TestInitialize]
        public void TestInitialize()
        {
            CheckNotOnPrem();
        }

        [DataTestMethod]
        [DynamicData(nameof(SimpleLoopDataSource))]
        public async Task TestSimpleLoopAsync(SimpleTestCase testCase) =>
            await TestLoopAsync(SimpleLoopAsync, testCase);

        [DataTestMethod]
        [DynamicData(nameof(DriverLikeLoopDataSource))]
        public async Task TestDriverLikeLoopAsync(DriverLikeTestCase testCase) =>
            await TestLoopAsync(DriverLikeLoopAsync, testCase);
    }

}
