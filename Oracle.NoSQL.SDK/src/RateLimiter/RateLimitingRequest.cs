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
    using System.Threading;
    using System.Threading.Tasks;

    internal class RateLimitingRequest
    {
        private readonly RateLimitingHandler handler;
        private readonly Request request;
        private RateLimiterEntry entry;
        private readonly DateTime endTime;
        private TimeSpan readDelay;
        private TimeSpan writeDelay;
        private bool doesReads;
        private bool doesWrites;

        internal RateLimitingRequest(RateLimitingHandler handler,
            Request request)
        {
            this.handler = handler;
            this.request = request;
            entry = handler.InitRateLimiterEntry(request);
            endTime = DateTime.UtcNow + request.Timeout;
            doesReads = request.DoesReads;
            doesWrites = request.DoesWrites;
        }

        internal async Task Start(CancellationToken cancellationToken)
        {
            // This is the case for PrepareRequest and un-prepared
            // QueryRequest where the table name is not known until the
            // request is executed.
            if (entry == null)
            {
                return;
            }

            if (doesReads)
            {
                readDelay += await entry.ReadRateLimiter.ConsumeUnitsAsync(0,
                    endTime - DateTime.UtcNow, false, cancellationToken);
            }

            if (doesWrites)
            {
                writeDelay += await entry.WriteRateLimiter.ConsumeUnitsAsync(0,
                    endTime - DateTime.UtcNow, false, cancellationToken);
            }
        }

        internal async Task Finish(object result,
            CancellationToken cancellationToken)
        {
            // For PrepareRequest and un-prepared QueryRequest the table name
            // is not known until the request is executed, so we try to get
            // RateLimiterEntry again (note that this must be done after
            // Request.ApplyResult() is called).  This may also need to be
            // done if table limits were just obtained (after Start() was
            // called).
            entry ??= handler.InitRateLimiterEntry(request);

            if (entry == null)
            {
                return;
            }

            // Recompute doesReads and doesWrites since their values could
            // change as a result of operation completion (currently this is
            // the case for doesWrites for unprepared query).
            doesReads = doesReads || request.DoesReads;
            doesWrites = doesWrites || request.DoesWrites;

            Debug.Assert(result is IDataResult);
            var consumedCapacity = ((IDataResult)result).ConsumedCapacity;
            Debug.Assert(consumedCapacity != null);

            if (request.DoesReads)
            {
                readDelay += await entry.ReadRateLimiter.ConsumeUnitsAsync(
                    consumedCapacity.ReadUnits, endTime - DateTime.UtcNow,
                    true, cancellationToken);
            }

            if (request.DoesWrites)
            {
                writeDelay += await entry.WriteRateLimiter.ConsumeUnitsAsync(
                    consumedCapacity.WriteUnits, endTime - DateTime.UtcNow,
                    true, cancellationToken);
            }

            consumedCapacity.ReadRateLimitDelay = readDelay;
            consumedCapacity.WriteRateLimitDelay = writeDelay;
        }

        internal void HandleException(Exception ex)
        {
            if (entry == null)
            {
                return;
            }

            if (ex is ReadThrottlingException rex)
            {
                // Correct any error in value determined by Request.DoesReads.
                doesReads = true;
                entry.ReadRateLimiter.HandleThrottlingException(rex);
            }
            else if (ex is WriteThrottlingException wex)
            {
                // Correct any error in value determined by
                // Request.DoesWrites.
                doesWrites = true;
                entry.WriteRateLimiter.HandleThrottlingException(wex);
            }
        }

    }

}
