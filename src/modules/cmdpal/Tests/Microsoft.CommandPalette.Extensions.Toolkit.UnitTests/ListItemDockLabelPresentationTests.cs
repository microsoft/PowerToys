// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[OSCondition(OperatingSystems.Windows)]
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

        item.SetDockLabelTabularDigits(false);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTabularDigits));
        Assert.IsTrue((bool)properties[WellKnownExtensionAttributes.DockLabelTrailingAlignment]);

        Assert.HasCount(3, notifications);
        Assert.AreSequenceEqual(
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
        item.SetDockLabelTabularDigits(false);
        item.SetDockLabelTrailingAlignment(false);

        Assert.AreSequenceEqual(
            new[]
            {
                WellKnownExtensionAttributes.DockLabelTabularDigitsPropertyName,
                WellKnownExtensionAttributes.DockLabelTrailingAlignmentPropertyName,
            },
            notifications);
        Assert.IsEmpty(item.GetProperties());
    }

    [TestMethod]
    public void PresentationHints_RequireWritableExtendedAttributes()
    {
        var item = new ReadOnlyPropertiesItem();
        var notifications = 0;
        item.PropChanged += (_, _) => notifications++;

        Assert.ThrowsExactly<InvalidOperationException>(() => item.SetDockLabelTabularDigits());
        Assert.ThrowsExactly<InvalidOperationException>(() => item.SetDockLabelTabularDigits(false));
        Assert.ThrowsExactly<InvalidOperationException>(() => item.SetDockLabelTrailingAlignment());
        Assert.ThrowsExactly<InvalidOperationException>(() => item.SetDockLabelTrailingAlignment(false));

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
