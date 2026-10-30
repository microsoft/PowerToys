// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for <see cref="ExtensionObject{T}"/> equality, which is the identity of the wrapped
/// reference and must never call into the wrapped (extension-provided) object.
/// </summary>
[TestClass]
public sealed class ExtensionObjectTests
{
    [TestMethod]
    public void Equals_EmptyWrapper_EqualsItself()
    {
        var wrapper = new ExtensionObject<object>(null);

        Assert.IsTrue(wrapper.Equals(wrapper));
        Assert.IsTrue(wrapper.Equals((object)wrapper));
    }

    [TestMethod]
    public void Equals_TwoEmptyWrappers_AreEqualWithSameHashCode()
    {
        var a = new ExtensionObject<object>(null);
        var b = new ExtensionObject<object>(null);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(b.Equals(a));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_SameTarget_AreEqualWithSameHashCode()
    {
        var target = new object();
        var a = new ExtensionObject<object>(target);
        var b = new ExtensionObject<object>(target);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(b.Equals(a));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentTargets_AreNotEqual()
    {
        var a = new ExtensionObject<object>(new object());
        var b = new ExtensionObject<object>(new object());

        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(b.Equals(a));
    }

    [TestMethod]
    public void Equals_EmptyAndNonEmpty_AreNotEqualInBothDirections()
    {
        var empty = new ExtensionObject<object>(null);
        var nonEmpty = new ExtensionObject<object>(new object());

        Assert.IsFalse(empty.Equals(nonEmpty));
        Assert.IsFalse(nonEmpty.Equals(empty));
    }

    [TestMethod]
    public void Equals_DistinctTargetsThatClaimEquality_AreNotEqual()
    {
        var a = new ExtensionObject<object>(new AlwaysEqual());
        var b = new ExtensionObject<object>(new AlwaysEqual());

        Assert.IsTrue(a.Unsafe!.Equals(b.Unsafe));
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void EqualsAndGetHashCode_DoNotCallIntoTarget()
    {
        var target = new ThrowingEquality();
        var a = new ExtensionObject<object>(target);
        var b = new ExtensionObject<object>(target);
        var other = new ExtensionObject<object>(new ThrowingEquality());

        Assert.IsTrue(a.Equals(b));
        Assert.IsFalse(a.Equals(other));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

        var set = new HashSet<ExtensionObject<object>> { a };
        Assert.IsTrue(set.Contains(b));
        Assert.IsFalse(set.Contains(other));
    }

    [TestMethod]
    public void Equals_NullOrOtherType_IsFalse()
    {
        var target = new object();
        var wrapper = new ExtensionObject<object>(target);

        Assert.IsFalse(wrapper.Equals(null));
        Assert.IsFalse(wrapper.Equals((object?)null));
        Assert.IsFalse(wrapper.Equals(target));
        Assert.IsFalse(wrapper.Equals(new ExtensionObject<string>(null)));
    }

    [TestMethod]
    public void HasValue_ReflectsWhetherTargetIsSet()
    {
        var empty = new ExtensionObject<object>(null);
        var wrapper = new ExtensionObject<object>(new object());

        Assert.IsFalse(empty.HasValue);
        Assert.IsTrue(wrapper.HasValue);

        // Compiles without a nullable warning only if [MemberNotNullWhen] reaches callers.
        if (wrapper.HasValue)
        {
            Assert.AreEqual(typeof(object), wrapper.Unsafe.GetType());
        }
    }

    [TestMethod]
    public void ReadOnlyInterface_PreservesPresenceAndTargetWithoutCallingIntoExtension()
    {
        var target = new ThrowingEquality();
        IExtensionObject<ThrowingEquality> wrapper = new ExtensionObject<ThrowingEquality>(target);
        IExtensionObject<ThrowingEquality> empty = new ExtensionObject<ThrowingEquality>(null);

        Assert.IsFalse(empty.HasValue);
        Assert.IsNull(empty.Unsafe);
        Assert.IsTrue(wrapper.HasValue);
        if (wrapper.HasValue)
        {
            Assert.AreSame(target, wrapper.Unsafe);
            Assert.AreEqual(typeof(ThrowingEquality), wrapper.Unsafe.GetType());
        }
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
