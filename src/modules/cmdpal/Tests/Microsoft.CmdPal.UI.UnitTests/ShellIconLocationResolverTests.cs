// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellIconLocationResolverTests
{
    [TestMethod]
    public void StaleSuppliedLocationIsNotReusedAfterInvalidation()
    {
        var cache = new ShellIconLocationCache();
        var locator = new FixedLocator(systemImageListIndex: 73);
        var resolver = new ShellIconLocationResolver(locator, cache);
        var request = new ShellItemIconRequest("C:\\Files\\report.txt", jumbo: false);
        var supplied = new LocatedShellIcon(
            request,
            ShellIconIdentity.FromSystemImageList(42, request.Jumbo));
        Assert.IsTrue(cache.TryAdd(request, supplied, cache.Generation, out var cached));

        cache.Clear();

        Assert.IsNull(resolver.GetCurrentOrCached(request, cached));
        var refreshed = resolver.Resolve(request);
        Assert.AreEqual(1, locator.CallCount);
        Assert.AreEqual(73, refreshed.Identity.SystemImageListIndex);
        Assert.AreEqual(cache.Generation, refreshed.Identity.CacheGeneration);
    }

    [TestMethod]
    public void ResolverBoundsRetriesWhenEveryLocationAttemptIsInvalidated()
    {
        var cache = new ShellIconLocationCache();
        var locator = new InvalidatingLocator(cache);
        var resolver = new ShellIconLocationResolver(locator, cache);
        var request = new ShellItemIconRequest("C:\\Files\\report.txt", jumbo: false);

        var locatedIcon = resolver.Resolve(request);

        Assert.AreEqual(3, locator.CallCount);
        Assert.AreEqual(ShellIconIdentityKind.ItemPath, locatedIcon.Identity.Kind);
        Assert.AreEqual(request.ItemPath, locatedIcon.Identity.ItemPath);
        Assert.AreEqual(cache.Generation, locatedIcon.Identity.CacheGeneration);
        Assert.IsFalse(cache.TryGet(request, out _));
    }

    [TestMethod]
    public void SyntheticTypeLocationsAreNotCachedAsRawAliases()
    {
        var cache = new ShellIconLocationCache();
        var locator = new FixedLocator(systemImageListIndex: 73, cacheRawRequestAlias: false);
        var resolver = new ShellIconLocationResolver(locator, cache);
        var request = new ShellItemIconRequest("C:\\Files\\missing.txt", jumbo: false);

        var first = resolver.Resolve(request);
        var second = resolver.Resolve(request);

        Assert.AreEqual(2, locator.CallCount);
        Assert.AreEqual(first.Identity, second.Identity);
        Assert.AreEqual(cache.Generation, first.Identity.CacheGeneration);
        Assert.IsFalse(first.CacheRawRequestAlias);
        Assert.IsFalse(cache.TryGet(request, out _));
    }

    [TestMethod]
    public void SyntheticTypeLocationRetriesWhenGenerationChanges()
    {
        var cache = new ShellIconLocationCache();
        var locator = new InvalidatingOnceSyntheticLocator(cache);
        var resolver = new ShellIconLocationResolver(locator, cache);
        var request = new ShellItemIconRequest("C:\\Files\\missing.txt", jumbo: false);

        var locatedIcon = resolver.Resolve(request);

        Assert.AreEqual(2, locator.CallCount);
        Assert.AreEqual(73, locatedIcon.Identity.SystemImageListIndex);
        Assert.AreEqual(cache.Generation, locatedIcon.Identity.CacheGeneration);
        Assert.IsFalse(locatedIcon.CacheRawRequestAlias);
        Assert.IsFalse(cache.TryGet(request, out _));
    }

    private sealed class FixedLocator(
        int systemImageListIndex,
        bool cacheRawRequestAlias = true) : IShellItemIconLocator
    {
        public int CallCount { get; private set; }

        public bool TryLocate(ShellItemIconRequest request, out LocatedShellIcon locatedIcon)
        {
            CallCount++;
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromSystemImageList(systemImageListIndex, request.Jumbo),
                cacheRawRequestAlias);
            return true;
        }
    }

    private sealed class InvalidatingLocator(ShellIconLocationCache cache) : IShellItemIconLocator
    {
        public int CallCount { get; private set; }

        public bool TryLocate(ShellItemIconRequest request, out LocatedShellIcon locatedIcon)
        {
            CallCount++;
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromSystemImageList(42, request.Jumbo));
            cache.Clear();
            return true;
        }
    }

    private sealed class InvalidatingOnceSyntheticLocator(ShellIconLocationCache cache) : IShellItemIconLocator
    {
        public int CallCount { get; private set; }

        public bool TryLocate(ShellItemIconRequest request, out LocatedShellIcon locatedIcon)
        {
            CallCount++;
            locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromSystemImageList(CallCount == 1 ? 42 : 73, request.Jumbo),
                CacheRawRequestAlias: false);
            if (CallCount == 1)
            {
                cache.Clear();
            }

            return true;
        }
    }
}
