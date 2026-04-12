// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct AdaptiveCacheStatistics(
    int Count,
    int Capacity,
    int PoolCount,
    long HitCount,
    long MissCount,
    long AddCount,
    long RemoveCount,
    long ClearCount,
    long CleanupCount,
    long CleanupEvictionCount,
    long CurrentTick,
    TimeSpan DecayInterval,
    double DecayFactor);
