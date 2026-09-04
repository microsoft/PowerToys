// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public partial class ListItemDockLabelWidthTests
{
    [TestMethod]
    public void SetDockLabelWidth_IntegerArgumentStoresDoublesBeforeNotifying()
    {
        var item = new ListItem();
        var properties = item.GetProperties();
        properties["Unrelated"] = "Keep me";
        var notifications = CaptureWidthNotifications(item);

        var result = item.SetDockLabelWidth(80);

        Assert.AreSame(item, result);
        Assert.AreSame(properties, ((IExtendedAttributesProvider)item).GetProperties());
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)80d, (object?)80d), notifications[0]);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
    }

    [TestMethod]
    [DataRow("12ch")]
    [DataRow("1200sqh")]
    [DataRow("2.5ch")]
    [DataRow("2.5sqh")]
    public void SetDockLabelWidth_UnitStringReplacesBothBoundsBeforeNotifying(string width)
    {
        var item = new ListItem().SetDockLabelWidth(80);
        var notifications = CaptureWidthNotifications(item);

        Assert.AreSame(item, item.SetDockLabelWidth(width));

        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)width, (object?)width), notifications[0]);
    }

    [TestMethod]
    [DataRow(80d)]
    [DataRow("12ch")]
    [DataRow("1200sqh")]
    public void SetDockLabelWidth_UnchangedPairDoesNotNotify(object width)
    {
        var item = SetWidth(new ListItem(), width);
        var notifications = CaptureWidthNotifications(item);

        Assert.AreSame(item, SetWidth(item, width));

        Assert.AreEqual(0, notifications.Count);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void SetDockLabelWidth_RepairsAMissingBound(bool hasMinimum, bool hasMaximum)
    {
        var item = new ListItem();
        var properties = item.GetProperties();
        if (hasMinimum)
        {
            properties[WellKnownExtensionAttributes.DockMinLabelWidth] = 80d;
        }

        if (hasMaximum)
        {
            properties[WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d;
        }

        var notifications = CaptureWidthNotifications(item);
        item.SetDockLabelWidth(80);

        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)80d, (object?)80d), notifications[0]);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void ClearDockLabelWidth_RemovesOnlyWidthHintsBeforeNotifying(bool hasMinimum, bool hasMaximum)
    {
        var item = new ListItem();
        var properties = item.GetProperties();
        properties["Unrelated"] = "Keep me";
        if (hasMinimum)
        {
            properties[WellKnownExtensionAttributes.DockMinLabelWidth] = "12ch";
        }

        if (hasMaximum)
        {
            properties[WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d;
        }

        var notifications = CaptureWidthNotifications(item);

        Assert.AreSame(item, item.ClearDockLabelWidth());
        item.ClearDockLabelWidth();

        Assert.AreEqual(hasMinimum || hasMaximum ? 1 : 0, notifications.Count);
        if (notifications.Count > 0)
        {
            Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)null), notifications[0]);
        }

        Assert.AreEqual(1, properties.Count);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
    }

    [TestMethod]
    public void SetDockLabelWidth_NullStringDoesNotMutateOrNotify()
    {
        var item = new ListItem().SetDockLabelWidth(80);
        var notifications = CaptureWidthNotifications(item);

        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidth((string)null!));

        Assert.AreEqual(0, notifications.Count);
        Assert.AreEqual(80d, item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth]);
        Assert.AreEqual(80d, item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth]);
    }

    [TestMethod]
    public void Helpers_PreserveConcreteTypeAndUseTheProvidersPropertyBag()
    {
        var item = new CustomPropertiesItem();
        var notifications = CaptureWidthNotifications(item);

        CustomPropertiesItem configured = item.SetDockLabelWidth("12ch");

        Assert.AreSame(item, configured);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)"12ch", (object?)"12ch"), notifications[0]);
        Assert.AreEqual(0, item.GetProperties().Count);

        CustomPropertiesItem cleared = item.ClearDockLabelWidth();

        Assert.AreSame(item, cleared);
        Assert.AreEqual(2, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)null), notifications[1]);
    }

    [TestMethod]
    public void Helpers_NullReceiverThrows()
    {
        ListItem item = null!;

        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidth(80));
        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidth("12ch"));
        Assert.ThrowsException<ArgumentNullException>(() => item.ClearDockLabelWidth());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Helpers_RequireWritableExtendedAttributes(bool readOnly)
    {
        var item = new CustomPropertiesItem
        {
            Attributes = readOnly ? new ReadOnlyDictionary<string, object>(new Dictionary<string, object>()) : null,
        };
        var notifications = 0;
        item.PropChanged += (_, _) => notifications++;

        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelWidth(80));
        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelWidth("12ch"));
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelWidth());

        Assert.AreEqual(0, notifications);
    }

    private static ListItem SetWidth(ListItem item, object width) =>
        width is string text ? item.SetDockLabelWidth(text) : item.SetDockLabelWidth((double)width);

    private static List<(string Name, object? Minimum, object? Maximum)> CaptureWidthNotifications(CommandItem item)
    {
        List<(string Name, object? Minimum, object? Maximum)> notifications = [];
        item.PropChanged += (_, args) =>
        {
            var properties = ((IExtendedAttributesProvider)item).GetProperties();
            properties.TryGetValue(WellKnownExtensionAttributes.DockMinLabelWidth, out var minimum);
            properties.TryGetValue(WellKnownExtensionAttributes.DockMaxLabelWidth, out var maximum);
            notifications.Add((args.PropertyName, minimum, maximum));
        };
        return notifications;
    }

    private sealed partial class CustomPropertiesItem : CommandItem, IExtendedAttributesProvider
    {
        public IDictionary<string, object>? Attributes { get; set; } = new PropertySet();

        IDictionary<string, object> IExtendedAttributesProvider.GetProperties() => Attributes!;
    }
}
