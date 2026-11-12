// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class SystemClockTests
{
    [TestMethod]
    public void UtcNow_ReturnsValueCloseToDateTimeUtcNow()
    {
        ISystemClock clock = new SystemClock();
        var before = DateTime.UtcNow;

        var actual = clock.UtcNow;

        var after = DateTime.UtcNow;
        Assert.IsTrue(
            actual >= before && actual <= after,
            $"Expected clock.UtcNow ({actual:o}) to be between {before:o} and {after:o}");
    }
}
