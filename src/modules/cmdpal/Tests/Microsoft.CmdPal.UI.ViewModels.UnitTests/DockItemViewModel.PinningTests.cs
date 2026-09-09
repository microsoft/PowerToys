// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class DockItemViewModelTests
{
    [TestMethod]
    [DataRow(80d, 80d)]
    [DataRow("12ch", 72d)]
    [DataRow("1200sqh", 72d)]
    public void WidthHints_PinnedTopLevelItemInitializesFromTheSource(object width, double expectedWidth)
    {
        var item = new ListItem { Title = "1%", DataPackage = new DataPackage() };
        if (width is string text)
        {
            item.SetDockLabelWidth(text);
        }
        else
        {
            item.SetDockLabelWidth((double)width);
        }

        var fixture = CreatePinnedBandFixture(item);
        try
        {
            var dockItem = fixture.Band.Items[0];
            Assert.AreSame(fixture.TopLevel, dockItem.Model.Unsafe);
            Assert.AreEqual((expectedWidth, expectedWidth), dockItem.LabelWidthConstraints.Resolve(6, 24, 100));
            Assert.IsNotNull(dockItem.DataPackage);
            Assert.AreSame(fixture.TopLevel.ItemViewModel.DataPackage, dockItem.DataPackage);
        }
        finally
        {
            CleanupPinnedBandFixture(fixture);
        }
    }

    [TestMethod]
    public void WidthHints_PinnedTopLevelItemRefreshesAndClearsInPlace()
    {
        var item = new WidthTestItem { Title = "1%" };
        var fixture = CreatePinnedBandFixture(item);
        try
        {
            var dockItem = fixture.Band.Items[0];
            Assert.AreSame(DockLabelWidthConstraints.Default, dockItem.LabelWidthConstraints);
            fixture.Scheduler.ExecuteAllAvailable();
            var constraintsNotified = false;
            var targetedNotificationForwarded = false;
            var propertiesInvalidated = false;
            dockItem.PropertyChanged += (_, args) =>
            {
                constraintsNotified |= args.PropertyName == nameof(dockItem.LabelWidthConstraints);
                targetedNotificationForwarded |= args.PropertyName == WellKnownExtensionAttributes.DockLabelWidthPropertyName;
                propertiesInvalidated |= args.PropertyName == "Properties";
            };

            item.SetDockLabelWidth("12ch");

            fixture.Scheduler.ExecuteUntil(() => constraintsNotified && targetedNotificationForwarded);
            Assert.AreEqual((72d, 72d), dockItem.LabelWidthConstraints.Resolve(6, 24, 100));
            Assert.IsFalse(propertiesInvalidated);

            constraintsNotified = false;
            targetedNotificationForwarded = false;
            propertiesInvalidated = false;
            item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = 80d;
            item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth] = 120d;
            item.NotifyPropertiesChanged();

            fixture.Scheduler.ExecuteUntil(() => constraintsNotified && propertiesInvalidated);
            Assert.AreEqual((80d, 120d), dockItem.LabelWidthConstraints.Resolve(6, 24, 100));
            Assert.IsFalse(targetedNotificationForwarded);

            constraintsNotified = false;
            targetedNotificationForwarded = false;
            propertiesInvalidated = false;
            item.ClearDockLabelWidth();

            fixture.Scheduler.ExecuteUntil(() => constraintsNotified && targetedNotificationForwarded);
            Assert.AreSame(DockLabelWidthConstraints.Default, dockItem.LabelWidthConstraints);
            Assert.AreSame(dockItem, fixture.Band.Items[0]);
            Assert.IsFalse(propertiesInvalidated);
            Assert.IsFalse(fixture.TopLevel.GetProperties().ContainsKey(WellKnownExtensionAttributes.DockMinLabelWidth));
            Assert.IsFalse(fixture.TopLevel.GetProperties().ContainsKey(WellKnownExtensionAttributes.DockMaxLabelWidth));
        }
        finally
        {
            CleanupPinnedBandFixture(fixture);
        }
    }

    [TestMethod]
    public void PresentationHints_PinnedTopLevelItemInitializeAndRefreshIndependently()
    {
        var item = new ListItem { Title = "1.00%" }.SetDockLabelTabularDigits();
        var fixture = CreatePinnedBandFixture(item);
        try
        {
            var dockItem = fixture.Band.Items[0];
            Assert.IsTrue(dockItem.UseTabularDigits);
            Assert.IsFalse(dockItem.UseTrailingLabelAlignment);
            fixture.Scheduler.ExecuteAllAvailable();

            var trailingAlignmentNotified = false;
            dockItem.PropertyChanged += (_, args) => trailingAlignmentNotified |= args.PropertyName == nameof(dockItem.UseTrailingLabelAlignment);

            item.SetDockLabelTrailingAlignment();

            fixture.Scheduler.ExecuteUntil(() => trailingAlignmentNotified);
            Assert.IsTrue(dockItem.UseTabularDigits);
            Assert.IsTrue(dockItem.UseTrailingLabelAlignment);
            Assert.AreSame(dockItem, fixture.Band.Items[0]);
            Assert.AreEqual(true, fixture.TopLevel.GetProperties()[WellKnownExtensionAttributes.DockLabelTabularDigits]);
            Assert.AreEqual(true, fixture.TopLevel.GetProperties()[WellKnownExtensionAttributes.DockLabelTrailingAlignment]);
        }
        finally
        {
            CleanupPinnedBandFixture(fixture);
        }
    }

    [TestMethod]
    public void ExtendedAttributes_PinnedTopLevelItemForwardsTheProvidersBagAndNotifications()
    {
        const string attributeName = "Test.CustomAttribute";
        var initialProperties = new Dictionary<string, object> { [attributeName] = "Initial" };
        var item = new PropertiesTestItem { Title = "1%", Properties = initialProperties };
        var fixture = CreatePinnedBandFixture(item);
        try
        {
            Assert.AreSame<object>(initialProperties, fixture.TopLevel.GetProperties());
            var notified = false;
            fixture.Band.Items[0].PropertyChanged += (_, args) => notified |= args.PropertyName == "Properties";

            var updatedProperties = new Dictionary<string, object> { [attributeName] = "Updated" };
            item.Properties = updatedProperties;
            item.NotifyPropertiesChanged();

            fixture.Scheduler.ExecuteUntil(() => notified);
            Assert.AreSame<object>(updatedProperties, fixture.TopLevel.GetProperties());
            Assert.AreEqual("Updated", fixture.TopLevel.GetProperties()[attributeName]);

            notified = false;
            updatedProperties.Remove(attributeName);
            item.NotifyPropertiesChanged();

            fixture.Scheduler.ExecuteUntil(() => notified);
            Assert.IsFalse(fixture.TopLevel.GetProperties().ContainsKey(attributeName));
        }
        finally
        {
            CleanupPinnedBandFixture(fixture);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExtendedAttributes_PinnedTopLevelItemWithoutPropertiesReturnsEmptyBag(bool hasProvider)
    {
        ICommandItem item = hasProvider ? new PropertiesTestItem { Title = "Empty" } : new CommandItem { Title = "Empty" };
        var fixture = CreatePinnedBandFixture(item);
        try
        {
            Assert.AreEqual(0, fixture.TopLevel.GetProperties().Count);
            Assert.AreSame(DockLabelWidthConstraints.Default, fixture.Band.Items[0].LabelWidthConstraints);
            Assert.IsNull(fixture.Band.Items[0].DataPackage);
        }
        finally
        {
            CleanupPinnedBandFixture(fixture);
        }
    }

    private static PinnedBandFixture CreatePinnedBandFixture(ICommandItem item)
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(new SettingsModel());
        var services = new Mock<IServiceProvider>();
        services.Setup(service => service.GetService(typeof(ISettingsService))).Returns(settingsService.Object);

        var itemViewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        var topLevel = new TopLevelViewModel(
            itemViewModel,
            TopLevelType.Normal,
            CommandPaletteHost.Instance,
            context.ProviderContext,
            new ProviderSettings(),
            services.Object,
            item,
            DefaultContextMenuFactory.Instance);
        topLevel.InitializeProperties();

        var pinnedItem = topLevel.ToPinnedDockBandItem();
        var root = new CommandItemViewModel(new(pinnedItem), new(context), DefaultContextMenuFactory.Instance);
        root.SlowInitializeProperties();
        var band = new DockBandViewModel(
            root,
            new(context),
            new DockBandSettings { ProviderId = context.ProviderContext.ProviderId, CommandId = topLevel.Id, ShowTitles = true },
            settingsService.Object,
            DefaultContextMenuFactory.Instance);
        band.InitializeProperties();
        scheduler.ExecuteUntil(() => band.Items.Count == 1);

        return new(band, root, topLevel, context, scheduler);
    }

    private static void CleanupPinnedBandFixture(PinnedBandFixture fixture)
    {
        fixture.Band.SafeCleanup();
        fixture.Root.SafeCleanup();
        fixture.TopLevel.Cleanup();
        fixture.Scheduler.ExecuteAllAvailable();
    }

    private sealed partial class PropertiesTestItem : CommandItem, IExtendedAttributesProvider
    {
        public IDictionary<string, object>? Properties { get; set; }

        IDictionary<string, object> IExtendedAttributesProvider.GetProperties() => Properties!;

        public void NotifyPropertiesChanged() => OnPropertyChanged(nameof(Properties));
    }

    private sealed record PinnedBandFixture(
        DockBandViewModel Band,
        CommandItemViewModel Root,
        TopLevelViewModel TopLevel,
        TestPageContext Context,
        QueuedTaskScheduler Scheduler);
}
