// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class KnownGoodVcpFeatureTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    [TestMethod]
    public void IsFresh_RecentObservation_ReturnsTrue()
    {
        Assert.IsTrue(Observed(Now.AddDays(-1)).IsFresh(Now, MaxAge));
    }

    [TestMethod]
    public void IsFresh_ObservationExactlyAtMaxAge_ReturnsTrue()
    {
        Assert.IsTrue(Observed(Now - MaxAge).IsFresh(Now, MaxAge));
    }

    [TestMethod]
    public void IsFresh_ObservationOlderThanMaxAge_ReturnsFalse()
    {
        // Nothing in discovery can contradict a cached entry, so a code that has not been
        // re-proven within the window must stop advertising support on cache evidence alone.
        Assert.IsFalse(Observed(Now - MaxAge - TimeSpan.FromSeconds(1)).IsFresh(Now, MaxAge));
    }

    [TestMethod]
    public void IsFresh_ObservationInTheFuture_ReturnsTrue()
    {
        // A clock moved backwards must not invalidate every cached observation at once.
        Assert.IsTrue(Observed(Now.AddDays(5)).IsFresh(Now, MaxAge));
    }

    private static KnownGoodVcpFeature Observed(DateTime lastSuccessfulUtc) => new()
    {
        Code = 0x10,
        Current = 45,
        Maximum = 100,
        Source = VcpObservationSource.MaximumCompatibilityProbe,
        LastSuccessfulUtc = lastSuccessfulUtc,
    };
}
