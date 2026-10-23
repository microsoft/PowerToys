// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        Assert.IsNull(wrapped.Safe);
        Assert.IsFalse(wrapped.Equals(new InProcObject<IListItem?>(null)));
        Assert.AreEqual(wrapped.GetHashCode(), wrapped.GetHashCode());
    }
}
