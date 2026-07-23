// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayIconRegistrationBackoffTests
{
    [TestMethod]
    public void NextDelay_UsesCappedRecoverySequence()
    {
        var backoff = new TrayIconRegistrationBackoff();

        Assert.AreEqual(250, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(500, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(1000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(2000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(5000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(5000, backoff.NextDelay().TotalMilliseconds);
    }

    [TestMethod]
    public void Reset_RestartsAtFirstDelay()
    {
        var backoff = new TrayIconRegistrationBackoff();
        _ = backoff.NextDelay();
        _ = backoff.NextDelay();

        backoff.Reset();

        Assert.AreEqual(250, backoff.NextDelay().TotalMilliseconds);
    }
}
