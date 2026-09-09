// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public partial class ListItemDockLabelPresentationTests
{
    [TestMethod]
    public void PresentationHints_CanBeEnabledAndClearedIndependently()
    {
        var item = new ListItem();
        var properties = item.GetProperties();
        properties["Unrelated"] = "Keep me";
        var notifications = CaptureNotifications(item);

        Assert.AreSame(item, item.SetDockLabelTabularDigits());
        Assert.IsTrue((bool)properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTrailingAlignment));

        Assert.AreSame(item, item.SetDockLabelTrailingAlignment());
        Assert.IsTrue((bool)properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
        Assert.IsTrue((bool)properties[WellKnownExtensionAttributes.DockLabelTrailingAlignment]);

        item.ClearDockLabelTabularDigits();
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTabularDigits));
        Assert.IsTrue((bool)properties[WellKnownExtensionAttributes.DockLabelTrailingAlignment]);

        Assert.AreEqual(3, notifications.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                WellKnownExtensionAttributes.DockLabelTabularDigitsPropertyName,
                WellKnownExtensionAttributes.DockLabelTrailingAlignmentPropertyName,
                WellKnownExtensionAttributes.DockLabelTabularDigitsPropertyName,
            },
            notifications);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
    }

    [TestMethod]
    public void PresentationHints_ReapplyingAndDisablingNotifyOnlyForChanges()
    {
        var item = new ListItem()
            .SetDockLabelTabularDigits()
            .SetDockLabelTrailingAlignment();
        var notifications = CaptureNotifications(item);

        item.SetDockLabelTabularDigits();
        item.SetDockLabelTrailingAlignment();
        item.SetDockLabelTabularDigits(false);
        item.SetDockLabelTrailingAlignment(false);
        item.ClearDockLabelTabularDigits();
        item.ClearDockLabelTrailingAlignment();

        CollectionAssert.AreEqual(
            new[]
            {
                WellKnownExtensionAttributes.DockLabelTabularDigitsPropertyName,
                WellKnownExtensionAttributes.DockLabelTrailingAlignmentPropertyName,
            },
            notifications);
        Assert.AreEqual(0, item.GetProperties().Count);
    }

    [TestMethod]
    public void PresentationHints_RequireWritableExtendedAttributes()
    {
        var item = new ReadOnlyPropertiesItem();
        var notifications = 0;
        item.PropChanged += (_, _) => notifications++;

        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelTabularDigits());
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelTabularDigits());
        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelTrailingAlignment());
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelTrailingAlignment());

        Assert.AreEqual(0, notifications);
    }

    private static List<string> CaptureNotifications(CommandItem item)
    {
        List<string> notifications = [];
        item.PropChanged += (_, args) => notifications.Add(args.PropertyName);
        return notifications;
    }

    private sealed partial class ReadOnlyPropertiesItem : CommandItem, IExtendedAttributesProvider
    {
        IDictionary<string, object> IExtendedAttributesProvider.GetProperties() =>
            new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    }
}
