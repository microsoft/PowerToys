// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for <see cref="EquatableList{T}"/> — the value-equality wrapper used as the backing
/// field for record list properties so record equality compares list contents, not references.
/// </summary>
[TestClass]
public class EquatableListTests
{
    private static EquatableList<string> Of(params string[] items) =>
        new(ImmutableList.Create(items));

    [TestMethod]
    public void Equal_WhenSameContentDifferentInstances()
    {
        var a = Of("x", "y", "z");
        var b = Of("x", "y", "z");

        Assert.AreNotSame(a.List, b.List);
        Assert.IsTrue(a.Equals(b));
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equal_WhenSameUnderlyingReference()
    {
        var shared = ImmutableList.Create("a", "b");
        var a = new EquatableList<string>(shared);
        var b = new EquatableList<string>(shared);

        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void NotEqual_WhenAnElementDiffers()
    {
        Assert.AreNotEqual(Of("a", "b"), Of("a", "c"));
    }

    [TestMethod]
    public void NotEqual_WhenOrderDiffers()
    {
        Assert.AreNotEqual(Of("a", "b"), Of("b", "a"));
    }

    [TestMethod]
    public void NotEqual_WhenCountsDiffer()
    {
        Assert.AreNotEqual(Of("a"), Of("a", "b"));
    }

    [TestMethod]
    public void EmptyLists_AreEqual()
    {
        var fromEmpty = new EquatableList<string>(ImmutableList<string>.Empty);
        var fromNull = new EquatableList<string>(null);

        Assert.IsTrue(fromEmpty.Equals(fromNull));
        Assert.AreEqual(fromEmpty.GetHashCode(), fromNull.GetHashCode());
    }

    [TestMethod]
    public void Default_ExposesEmptyListAndEqualsEmpty()
    {
        EquatableList<string> defaulted = default;

        // A default(struct) has a null inner list; List must still surface an empty list
        // (never null) and compare equal to an explicitly-empty instance.
        Assert.IsNotNull(defaulted.List);
        Assert.AreEqual(0, defaulted.List.Count);
        Assert.IsTrue(defaulted.Equals(new EquatableList<string>(ImmutableList<string>.Empty)));
    }

    [TestMethod]
    public void List_IsNeverNull()
    {
        Assert.IsNotNull(new EquatableList<string>(null).List);
    }

    [TestMethod]
    public void EqualsObject_ReturnsFalse_ForOtherTypes()
    {
        object other = "not an equatable list";

        Assert.IsFalse(Of("a").Equals(other));
        Assert.IsFalse(Of("a").Equals(null!));
    }

    [TestMethod]
    public void EqualsObject_ReturnsTrue_ForBoxedEqualValue()
    {
        object boxed = Of("a", "b");

        Assert.IsTrue(Of("a", "b").Equals(boxed));
    }
}
