// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public partial class ListItemDockLabelWidthTests
{
    [TestMethod]
    [DataRow(80d)]
    [DataRow(0d)]
    public void Dips_StoresADouble(double value)
    {
        var item = new ListItem().SetDockLabelReservations(DockLabelWidth.Dips(value), null);

        Assert.AreEqual(value, item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.IsFalse(item.GetProperties().ContainsKey(WellKnownExtensionAttributes.DockSubtitleWidth));
    }

    [TestMethod]
    [DataRow(12d, "12ch")]
    [DataRow(4.6d, "4.6ch")]
    [DataRow(0d, "0ch")]
    [DataRow(double.Epsilon, "5E-324ch")]
    public void Characters_StoresAnInvariantUnitString(double value, string expected)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
            var item = new ListItem().SetDockLabelReservations(DockLabelWidth.Characters(value), null);

            Assert.AreEqual(expected, item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("100%")]
    [DataRow("12ch")]
    [DataRow("text:CPU")]
    [DataRow("\u010cas aktivity")]
    public void Sample_StoresALiteralWithAnUnambiguousPrefix(string sample)
    {
        var item = new ListItem().SetDockLabelReservations(null, DockLabelWidth.Sample(sample));

        Assert.AreEqual("text:" + sample, item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(double.MaxValue)]
    public void NumericFactories_RejectUnsupportedValues(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => DockLabelWidth.Dips(value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => DockLabelWidth.Characters(value));
    }

    [TestMethod]
    public void Sample_RejectsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => DockLabelWidth.Sample(null!));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SetWidths_ReplacesBothValuesBeforeOneNotification(bool limits)
    {
        var item = new CustomPropertiesItem();
        var (firstKey, secondKey) = Keys(limits);
        var properties = ((IExtendedAttributesProvider)item).GetProperties();
        properties[firstKey] = 80d;
        properties[secondKey] = 80d;
        var notifications = CaptureWidthNotifications(item, limits);

        CustomPropertiesItem configured = SetWidths(item, DockLabelWidth.Sample("100%"), DockLabelWidth.Characters(12), limits);
        SetWidths(item, DockLabelWidth.Sample("100%"), DockLabelWidth.Characters(12), limits);

        Assert.AreSame(item, configured);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)"text:100%", (object?)"12ch"), notifications[0]);
        Assert.AreEqual(0, item.GetProperties().Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SetWidths_NullRemovesOnlyTheSpecifiedValue(bool limits)
    {
        var item = new ListItem();
        SetWidths(item, DockLabelWidth.Dips(20), DockLabelWidth.Dips(80), limits);
        var notifications = CaptureWidthNotifications(item, limits);

        SetWidths(item, null, DockLabelWidth.Dips(80), limits);
        SetWidths(item, null, DockLabelWidth.Dips(80), limits);
        SetWidths(item, DockLabelWidth.Sample(string.Empty), null, limits);
        SetWidths(item, DockLabelWidth.Sample(string.Empty), null, limits);

        Assert.AreEqual(2, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)80d), notifications[0]);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)"text:", (object?)null), notifications[1]);
    }

    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, true)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void ClearWidths_RemovesThePairBeforeNotifyingAndPreservesOtherAttributes(bool limits, bool hasFirst, bool hasSecond)
    {
        var item = new CustomPropertiesItem();
        var properties = ((IExtendedAttributesProvider)item).GetProperties();
        var (firstKey, secondKey) = Keys(limits);
        var (otherFirst, otherSecond) = Keys(!limits);
        properties[otherFirst] = "text:CPU";
        properties[otherSecond] = "12ch";
        properties["Unrelated"] = "Keep me";
        if (hasFirst)
        {
            properties[firstKey] = 20d;
        }

        if (hasSecond)
        {
            properties[secondKey] = 80d;
        }

        var notifications = CaptureWidthNotifications(item, limits);

        CustomPropertiesItem cleared = limits ? item.ClearDockLabelWidthLimits() : item.ClearDockLabelReservations();
        SetWidths(item, null, null, limits);

        Assert.AreSame(item, cleared);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual((WellKnownExtensionAttributes.DockLabelWidthPropertyName, (object?)null, (object?)null), notifications[0]);
        Assert.AreEqual("text:CPU", properties[otherFirst]);
        Assert.AreEqual("12ch", properties[otherSecond]);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
        Assert.AreEqual(0, item.GetProperties().Count);
    }

    [TestMethod]
    public void ReservationsAndLimits_CanBeChangedIndependently()
    {
        var item = new ListItem()
            .SetDockLabelWidthLimits(DockLabelWidth.Dips(20), DockLabelWidth.Characters(12))
            .SetDockLabelReservations(DockLabelWidth.Sample("100%"), DockLabelWidth.Sample("CPU"));
        var properties = item.GetProperties();

        item.SetDockLabelWidthLimits(null, DockLabelWidth.Dips(80));

        Assert.AreEqual("text:100%", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("text:CPU", properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
        item.SetDockLabelReservations(DockLabelWidth.Characters(5), null);

        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockMinLabelWidth));
        Assert.AreEqual(80d, properties[WellKnownExtensionAttributes.DockMaxLabelWidth]);
        Assert.AreEqual("5ch", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockSubtitleWidth));
    }

    [TestMethod]
    public void InvalidFactoryArgument_LeavesBothAttributesUnchanged()
    {
        var item = new ListItem().SetDockLabelReservations(DockLabelWidth.Dips(20), DockLabelWidth.Dips(80));
        var notifications = CaptureWidthNotifications(item, limits: false);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => item.SetDockLabelReservations(DockLabelWidth.Dips(40), DockLabelWidth.Dips(-1)));

        Assert.AreEqual(20d, item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual(80d, item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.AreEqual(0, notifications.Count);
    }

    [TestMethod]
    public void WidthHelpers_RejectNullItems()
    {
        ListItem item = null!;

        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelReservations(null, null));
        Assert.ThrowsException<ArgumentNullException>(() => item.SetDockLabelWidthLimits(null, null));
        Assert.ThrowsException<ArgumentNullException>(() => item.ClearDockLabelReservations());
        Assert.ThrowsException<ArgumentNullException>(() => item.ClearDockLabelWidthLimits());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WidthHelpers_RequireWritableExtendedAttributes(bool nullProperties)
    {
        var item = new CustomPropertiesItem
        {
            Attributes = nullProperties ? null : new ReadOnlyDictionary<string, object>(new Dictionary<string, object>()),
        };
        var notifications = 0;
        item.PropChanged += (_, _) => notifications++;

        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelReservations(DockLabelWidth.Dips(80), null));
        Assert.ThrowsException<InvalidOperationException>(() => item.SetDockLabelWidthLimits(null, DockLabelWidth.Characters(12)));
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelReservations());
        Assert.ThrowsException<InvalidOperationException>(() => item.ClearDockLabelWidthLimits());

        Assert.AreEqual(0, notifications);
    }

    private static TItem SetWidths<TItem>(TItem item, DockLabelWidth? first, DockLabelWidth? second, bool limits)
        where TItem : CommandItem, IExtendedAttributesProvider =>
        limits ? item.SetDockLabelWidthLimits(first, second) : item.SetDockLabelReservations(first, second);

    private static (string First, string Second) Keys(bool limits) => limits
        ? (WellKnownExtensionAttributes.DockMinLabelWidth, WellKnownExtensionAttributes.DockMaxLabelWidth)
        : (WellKnownExtensionAttributes.DockTitleWidth, WellKnownExtensionAttributes.DockSubtitleWidth);

    private static List<(string Name, object? FirstWidth, object? SecondWidth)> CaptureWidthNotifications(CommandItem item, bool limits)
    {
        List<(string Name, object? FirstWidth, object? SecondWidth)> notifications = [];
        var (firstKey, secondKey) = Keys(limits);
        item.PropChanged += (_, args) =>
        {
            var properties = ((IExtendedAttributesProvider)item).GetProperties();
            properties.TryGetValue(firstKey, out var firstWidth);
            properties.TryGetValue(secondKey, out var secondWidth);
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
