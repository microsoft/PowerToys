// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class PlaceholderInfoNameEqualityComparerTests
{
    [TestMethod]
    public void Equals_BothNull_ReturnsTrue()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;

        var result = comparer.Equals(null, null);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Equals_OneNull_ReturnsFalse()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        var p = new PlaceholderInfo("name", 0);

        Assert.IsFalse(comparer.Equals(p, null));
        Assert.IsFalse(comparer.Equals(null, p));
    }

    [TestMethod]
    public void Equals_SameNameDifferentIndex_ReturnsTrue()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        var p1 = new PlaceholderInfo("name", 0);
        var p2 = new PlaceholderInfo("name", 10);

        Assert.IsTrue(comparer.Equals(p1, p2));
    }

    [TestMethod]
    public void Equals_DifferentNameSameIndex_ReturnsFalse()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        var p1 = new PlaceholderInfo("first", 3);
        var p2 = new PlaceholderInfo("second", 3);

        Assert.IsFalse(comparer.Equals(p1, p2));
    }

    [TestMethod]
    public void Equals_CaseInsensitive_ReturnsTrue()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        var p1 = new PlaceholderInfo("Name", 0);
        var p2 = new PlaceholderInfo("name", 5);

        Assert.IsTrue(comparer.Equals(p1, p2));
        Assert.AreEqual(comparer.GetHashCode(p1), comparer.GetHashCode(p2));
    }

    [TestMethod]
    public void GetHashCode_SameNameDifferentIndex_SameHash()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        var p1 = new PlaceholderInfo("same", 1);
        var p2 = new PlaceholderInfo("same", 99);

        Assert.AreEqual(comparer.GetHashCode(p1), comparer.GetHashCode(p2));
    }

    [TestMethod]
    public void GetHashCode_Null_ThrowsArgumentNullException()
    {
        var comparer = PlaceholderInfoNameEqualityComparer.Instance;
        Assert.ThrowsException<ArgumentNullException>(() => comparer.GetHashCode(null!));
    }

    [TestMethod]
    public void Instance_ReturnsSingleton()
    {
        var a = PlaceholderInfoNameEqualityComparer.Instance;
        var b = PlaceholderInfoNameEqualityComparer.Instance;

        Assert.IsNotNull(a);
        Assert.AreSame(a, b);
    }

    [TestMethod]
    public void HashSet_UsesNameEquality_IgnoresIndex()
    {
        var set = new HashSet<PlaceholderInfo>(PlaceholderInfoNameEqualityComparer.Instance)
        {
            new("dup", 0),
            new("DUP", 10),
            new("unique", 0),
        };

        Assert.AreEqual(2, set.Count);
        Assert.IsTrue(set.Contains(new PlaceholderInfo("dup", 123)));
        Assert.IsTrue(set.Contains(new PlaceholderInfo("UNIQUE", 999)));
        Assert.IsFalse(set.Contains(new PlaceholderInfo("missing", 0)));
    }
}
