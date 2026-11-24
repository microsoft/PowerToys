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
    public void NonClientMetricsChangeInvalidatesCachedImageListIndices()
    {
        _ = ShellIconCacheInvalidator.InitializeShellIconCache();

        var cache = new ShellIconLocationCache();
        using var invalidator = new ShellIconCacheInvalidator(
            windowHandle: 0,
            NotificationMessage,
            cache);
        var request = new ShellItemIconRequest(@"C:\Apps\Example.exe", jumbo: false);
        var location = new LocatedShellIcon(request, ShellIconIdentity.FromSystemImageList(42, request.Jumbo));
        var generation = cache.Generation;
        Assert.IsTrue(cache.TryAdd(request, location, generation, out var cached));

        invalidator.OnNonClientMetricsChanged();

        Assert.AreEqual(generation + 1, cache.Generation);
        Assert.IsFalse(cache.TryGet(request, out _));
        Assert.IsFalse(cache.IsCurrent(cached));
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
