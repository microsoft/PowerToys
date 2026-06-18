// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class BuiltInMonitorBlacklistTests
{
    [TestMethod]
    public void Entries_LoadsWithoutThrowing()
    {
        var entries = BuiltInMonitorBlacklist.Entries;

        Assert.IsNotNull(entries);
    }

    [TestMethod]
    public void Entries_AreNormalizedToUpperCase()
    {
        foreach (var entry in BuiltInMonitorBlacklist.Entries)
        {
            Assert.AreEqual(
                entry.EdidId,
                entry.EdidId.ToUpperInvariant(),
                $"Entry '{entry.EdidId}' is not normalized to uppercase.");
            Assert.AreEqual(
                entry.EdidId.Trim(),
                entry.EdidId,
                $"Entry '{entry.EdidId}' has untrimmed whitespace.");
        }
    }

    [TestMethod]
    public void Entries_ContainNoEmptyEdidIds()
    {
        Assert.IsFalse(
            BuiltInMonitorBlacklist.Entries.Any(e => string.IsNullOrWhiteSpace(e.EdidId)),
            "Built-in list should never contain blank EdidId entries.");
    }

    [TestMethod]
    public void Entries_AreCached()
    {
        var first = BuiltInMonitorBlacklist.Entries;
        var second = BuiltInMonitorBlacklist.Entries;

        Assert.AreSame(first, second, "Entries should be returned from a cached Lazy<>.");
    }
}
