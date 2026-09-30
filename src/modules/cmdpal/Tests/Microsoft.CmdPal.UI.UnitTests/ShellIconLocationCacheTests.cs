// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellIconLocationCacheTests
{
    [TestMethod]
    public void ClearInvalidatesAliasesAndMaterializedIdentityGeneration()
    {
        var cache = new ShellIconLocationCache();
        var request = new ShellItemIconRequest("C:\\Files\\report.txt", jumbo: false);
        var resolved = new LocatedShellIcon(
            request,
            ShellIconIdentity.FromSystemImageList(42, jumbo: false));

        Assert.IsTrue(cache.TryAdd(request, resolved, cache.Generation, out var first));
        Assert.IsTrue(cache.TryGet(request, out var cached));
        Assert.AreEqual(first, cached);

        var staleGeneration = cache.Generation;
        cache.Clear();

        Assert.IsFalse(cache.TryGet(request, out _));
        Assert.IsFalse(cache.TryAdd(request, resolved, staleGeneration, out _));
        Assert.IsTrue(cache.TryAdd(request, resolved, cache.Generation, out var refreshed));
        Assert.AreNotEqual(first.Identity, refreshed.Identity);
        Assert.AreEqual(first.Identity.SystemImageListIndex, refreshed.Identity.SystemImageListIndex);
    }

    [TestMethod]
    public void IsCurrentRejectsLocationFromPreviousGeneration()
    {
        var cache = new ShellIconLocationCache();
        var request = new ShellItemIconRequest("C:\\Files\\report.txt", jumbo: false);
        var resolved = new LocatedShellIcon(
            request,
            ShellIconIdentity.FromSystemImageList(42, jumbo: false));

        Assert.IsTrue(cache.TryAdd(request, resolved, cache.Generation, out var current));
        Assert.IsTrue(cache.IsCurrent(current));

        cache.Clear();

        Assert.IsFalse(cache.IsCurrent(current));
    }
}
