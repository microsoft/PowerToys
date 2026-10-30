// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class InProcObjectTests
{
    [TestMethod]
    public void Safe_PreservesOriginalObject()
    {
        var item = new ListItem { Title = "Original" };
        var wrapped = new InProcObject<IListItem>(item);

        Assert.AreSame(item, wrapped.Safe);
        Assert.IsTrue(wrapped.HasValue);
        item.Title = "Updated";
        Assert.AreEqual("Updated", wrapped.Safe.Title);
    }

    [TestMethod]
    public void Equality_UsesWrappedValueAndMatchingWrapperType()
    {
        var item = new ListItem();
        var wrapped = new InProcObject<IListItem>(item);
        var same = new InProcObject<IListItem>(item);

        Assert.IsTrue(wrapped.Equals(same));
        Assert.AreEqual(wrapped.GetHashCode(), same.GetHashCode());
        Assert.IsFalse(wrapped.Equals(new InProcObject<IListItem>(new ListItem())));
        Assert.IsFalse(wrapped.Equals(new ExtensionObject<IListItem>(item)));
        Assert.IsFalse(wrapped.Equals(item));
        Assert.IsFalse(wrapped.Equals(null));
    }

    [TestMethod]
    public void NullableValue_MatchesExtensionObjectSemantics()
    {
        var wrapped = new InProcObject<IListItem?>(null);
        var same = new InProcObject<IListItem?>(null);

        Assert.IsNull(wrapped.Safe);
        Assert.IsFalse(wrapped.HasValue);
        Assert.IsTrue(wrapped.Equals(wrapped));
        Assert.IsTrue(wrapped.Equals((object)same));
        Assert.IsTrue(wrapped.Equals(same));
        Assert.IsTrue(same.Equals(wrapped));
        Assert.AreEqual(wrapped.GetHashCode(), same.GetHashCode());
        Assert.AreEqual(0, wrapped.GetHashCode());
        Assert.IsFalse(wrapped.Equals(new InProcObject<IListItem?>(new ListItem())));

        var set = new HashSet<InProcObject<IListItem?>> { wrapped };
        Assert.IsTrue(set.Contains(same));
    }

    [TestMethod]
    public void Equality_DistinctTargetsThatClaimEquality_AreNotEqual()
    {
        var first = new InProcObject<object>(new AlwaysEqual());
        var second = new InProcObject<object>(new AlwaysEqual());

        Assert.IsTrue(first.Safe.Equals(second.Safe));
        Assert.IsFalse(first.Equals(second));
    }

    [TestMethod]
    public void EqualityAndHashCode_UseReferenceIdentityWithoutCallingTarget()
    {
        var target = new ThrowingEquality();
        var wrapped = new InProcObject<object>(target);
        var same = new InProcObject<object>(target);
        var different = new InProcObject<object>(new ThrowingEquality());

        Assert.IsTrue(wrapped.Equals(same));
        Assert.IsFalse(wrapped.Equals(different));
        Assert.AreEqual(RuntimeHelpers.GetHashCode(target), wrapped.GetHashCode());
        Assert.AreEqual(wrapped.GetHashCode(), same.GetHashCode());

        var set = new HashSet<InProcObject<object>> { wrapped };
        Assert.IsTrue(set.Contains(same));
        Assert.IsFalse(set.Contains(different));
    }

    private sealed class AlwaysEqual
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 0;
    }

    private sealed class ThrowingEquality
    {
        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals must not be called on the wrapped object.");

        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode must not be called on the wrapped object.");
    }
}
