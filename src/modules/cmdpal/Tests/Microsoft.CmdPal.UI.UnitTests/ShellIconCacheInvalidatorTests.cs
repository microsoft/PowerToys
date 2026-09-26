// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellIconCacheInvalidatorTests
{
    private const uint NotificationMessage = 0x8001;

    [TestMethod]
    public void DifferentWindowMessageIsIgnored()
    {
        var cache = new ShellIconLocationCache();
        using var invalidator = new ShellIconCacheInvalidator(
            windowHandle: 0,
            NotificationMessage,
            cache);
        var generation = cache.Generation;

        Assert.IsFalse(invalidator.TryHandleMessage(NotificationMessage + 1, 0, 0));
        Assert.AreEqual(generation, cache.Generation);
    }

    [TestMethod]
    public void MatchingWindowMessageInvalidatesLocations()
    {
        var cache = new ShellIconLocationCache();
        using var invalidator = new ShellIconCacheInvalidator(
            windowHandle: 0,
            NotificationMessage,
            cache);
        var generation = cache.Generation;

        Assert.IsTrue(invalidator.TryHandleMessage(NotificationMessage, 0, 0));
        Assert.AreEqual(generation + 1, cache.Generation);
    }

    [TestMethod]
    public void ExplicitAndShellRestartInvalidationsAdvanceGeneration()
    {
        var cache = new ShellIconLocationCache();
        using var invalidator = new ShellIconCacheInvalidator(
            windowHandle: 0,
            NotificationMessage,
            cache);
        var generation = cache.Generation;

        invalidator.Invalidate(ShellIconCacheInvalidationReason.AssociationChanged);
        Assert.AreEqual(generation + 1, cache.Generation);

        invalidator.OnShellRestarted();
        Assert.AreEqual(generation + 2, cache.Generation);
    }
}
