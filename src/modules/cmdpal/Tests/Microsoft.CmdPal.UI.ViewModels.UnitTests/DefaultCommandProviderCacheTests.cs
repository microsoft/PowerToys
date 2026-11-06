// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DefaultCommandProviderCacheTests
{
    [TestMethod]
    public async Task DisposeAsync_MemorizedValues_PersistsLatestSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CmdPal-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "cache.json");
        try
        {
            await using var cache = new DefaultCommandProviderCache(path);
            cache.Memorize("one", new CommandProviderCacheItem("Before"));
            cache.Memorize("two", new CommandProviderCacheItem("Other"));
            cache.Memorize("one", new CommandProviderCacheItem("After"));
            await cache.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await cache.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            await using var reloaded = new DefaultCommandProviderCache(path);
            Assert.AreEqual(new CommandProviderCacheItem("After"), reloaded.Recall("one"));
            Assert.AreEqual(new CommandProviderCacheItem("Other"), reloaded.Recall("two"));
            Assert.IsNull(reloaded.Recall("missing"));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task WhenIdleAsync_WriteFails_DrainsAndAllowsLaterSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CmdPal-cache-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "cache.json");
        try
        {
            await using var cache = new DefaultCommandProviderCache(path);
            cache.Memorize("one", new CommandProviderCacheItem("Before"));
            await cache.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(File.Exists(path));
            Directory.CreateDirectory(directory);
            cache.Memorize("one", new CommandProviderCacheItem("After"));
            await cache.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await using var reloaded = new DefaultCommandProviderCache(path);
            Assert.AreEqual(new CommandProviderCacheItem("After"), reloaded.Recall("one"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }
}
