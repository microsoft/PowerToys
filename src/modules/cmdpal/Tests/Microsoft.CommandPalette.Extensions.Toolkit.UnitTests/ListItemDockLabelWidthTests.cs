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
    [DataRow(30d, 72d)]
    [DataRow("5ch", "12ch")]
    [DataRow("500sqh", "1200sqh")]
    public void SetDockLabelWidths_UpdatesBothRowsBeforeNotifyingAndPreservesSharedBounds(object titleWidth, object subtitleWidth)
    {
        var item = new CustomPropertiesItem().SetDockLabelWidth("12ch");
        var properties = ((IExtendedAttributesProvider)item).GetProperties();
        var notifications = CaptureWidthNotifications(item, perRow: true);

        CustomPropertiesItem configured = titleWidth is string text
            ? item.SetDockLabelWidths(text, (string)subtitleWidth)
            : item.SetDockLabelWidths((double)titleWidth, (double)subtitleWidth);

        Assert.AreSame(item, configured);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)titleWidth, (object?)subtitleWidth), notifications[0]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMinLabelWidth]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMaxLabelWidth]);
        Assert.AreEqual(0, item.GetProperties().Count);

        if (titleWidth is string sameText)
        {
            item.SetDockLabelWidths(sameText, (string)subtitleWidth);
        }
        else
        {
            item.SetDockLabelWidths((double)titleWidth, (double)subtitleWidth);
        }

        Assert.AreEqual(1, notifications.Count);

        Assert.AreSame(item, item.ClearDockLabelWidths());
        Assert.AreEqual(2, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)null), notifications[1]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMinLabelWidth]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMaxLabelWidth]);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void ClearDockLabelWidths_RemovesOnlyRowHintsBeforeNotifying(bool hasTitle, bool hasSubtitle)
    {
        var item = new ListItem().SetDockLabelWidth("12ch");
        var properties = item.GetProperties();
        properties["Unrelated"] = "Keep me";
        if (hasTitle)
        {
            properties[WellKnownExtensionAttributes.DockTitleWidth] = "5ch";
        }

        if (hasSubtitle)
        {
            properties[WellKnownExtensionAttributes.DockSubtitleWidth] = "12ch";
        }

        var notifications = CaptureWidthNotifications(item, perRow: true);

        Assert.AreSame(item, item.ClearDockLabelWidths());
        item.ClearDockLabelWidths();

        Assert.AreEqual(hasTitle || hasSubtitle ? 1 : 0, notifications.Count);
        if (notifications.Count > 0)
        {
            Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)null), notifications[0]);
        }

        Assert.AreEqual(3, properties.Count);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMinLabelWidth]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockMaxLabelWidth]);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
    }

    [TestMethod]
    public void SharedWidthHelpers_PreserveRowReservations()
    {
        var item = new ListItem().SetDockLabelWidths(30, 72);

        item.SetDockLabelWidth("12ch");
        item.ClearDockLabelWidth();

        Assert.AreEqual(2, item.GetProperties().Count);
        Assert.AreEqual(30d, item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual(72d, item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    [DataRow(null, "12ch")]
    [DataRow("5ch", null)]
    public void SetDockLabelWidths_NullStringDoesNotMutateOrNotify(string? titleWidth, string? subtitleWidth)
    {
        var item = new ListItem().SetDockLabelWidths("5ch", "12ch");
        var notifications = CaptureWidthNotifications(item, perRow: true);

        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidths(titleWidth!, subtitleWidth!));

        Assert.AreEqual(0, notifications.Count);
        Assert.AreEqual("5ch", item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("12ch", item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SetDockLabelWidths_RepairsAMissingRowReservation(bool removeTitle)
    {
        var item = new ListItem().SetDockLabelWidths("5ch", "12ch");
        item.GetProperties().Remove(removeTitle ? WellKnownExtensionAttributes.DockTitleWidth : WellKnownExtensionAttributes.DockSubtitleWidth);
        var notifications = CaptureWidthNotifications(item, perRow: true);

        item.SetDockLabelWidths("5ch", "12ch");

        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)"5ch", (object?)"12ch"), notifications[0]);
    }

    [TestMethod]
    public void SetDockLabelWidths_ChangingOnlyTheSubtitleNotifies()
    {
        var item = new ListItem().SetDockLabelWidths("5ch", "12ch");
        var notifications = CaptureWidthNotifications(item, perRow: true);

        item.SetDockLabelWidths("5ch", "8ch");

        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)"5ch", (object?)"8ch"), notifications[0]);
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
        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidths(30, 72));
        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidths("5ch", "12ch"));
        Assert.ThrowsException<ArgumentNullException>(() => item.ClearDockLabelWidths());
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
        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelWidths(30, 72));
        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelWidths("5ch", "12ch"));
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelWidths());

        Assert.AreEqual(0, notifications);
    }

    private static ListItem SetWidth(ListItem item, object width) =>
        width is string text ? item.SetDockLabelWidth(text) : item.SetDockLabelWidth((double)width);

    private static List<(string Name, object? FirstWidth, object? SecondWidth)> CaptureWidthNotifications(CommandItem item, bool perRow = false)
    {
        List<(string Name, object? FirstWidth, object? SecondWidth)> notifications = [];
        item.PropChanged += (_, args) =>
        {
            var properties = ((IExtendedAttributesProvider)item).GetProperties();
            properties.TryGetValue(perRow ? WellKnownExtensionAttributes.DockTitleWidth : WellKnownExtensionAttributes.DockMinLabelWidth, out var firstWidth);
            properties.TryGetValue(perRow ? WellKnownExtensionAttributes.DockSubtitleWidth : WellKnownExtensionAttributes.DockMaxLabelWidth, out var secondWidth);
            notifications.Add((args.PropertyName, firstWidth, secondWidth));
        };
        return notifications;
    }

    private sealed partial class CustomPropertiesItem : CommandItem, IExtendedAttributesProvider
    {
        public IDictionary<string, object>? Attributes { get; set; } = new PropertySet();

        IDictionary<string, object> IExtendedAttributesProvider.GetProperties() => Attributes!;
    }
}
