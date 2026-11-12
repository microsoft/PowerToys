// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorIdComparerTests
{
    private const string Upper = @"\\?\DISPLAY#BOE0900#4&ABC&0&UID111";
    private const string Lower = @"\\?\display#boe0900#4&abc&0&uid111";
    private const string DifferentUid = @"\\?\DISPLAY#BOE0900#4&ABC&0&UID222";

    [TestMethod]
    public void Equal_IdsDifferingOnlyByCase_AreEqual()
    {
        Assert.IsTrue(MonitorIdComparer.Equal(Upper, Lower));
    }

    [TestMethod]
    public void Equal_DistinctMonitors_AreNotEqual()
    {
        Assert.IsFalse(MonitorIdComparer.Equal(Upper, DifferentUid));
    }

    [TestMethod]
    public void Equal_BothNull_AreEqual()
    {
        Assert.IsTrue(MonitorIdComparer.Equal(null, null));
    }

    [TestMethod]
    public void Equal_NullVersusValue_AreNotEqual()
    {
        Assert.IsFalse(MonitorIdComparer.Equal(null, Upper));
    }

    [TestMethod]
    public void Instance_IsCaseInsensitive_ForDictionaryKeys()
    {
        var set = new System.Collections.Generic.HashSet<string>(MonitorIdComparer.Instance) { Upper };

        Assert.IsTrue(set.Contains(Lower), "A monitor-Id-keyed set must match regardless of casing");
        Assert.IsFalse(set.Contains(DifferentUid));
    }
}
