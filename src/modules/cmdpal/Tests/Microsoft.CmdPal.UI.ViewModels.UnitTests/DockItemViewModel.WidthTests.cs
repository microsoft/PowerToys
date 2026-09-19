// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class DockItemViewModelTests
{
    private sealed partial class WidthTestItem : ListItem, IExtendedAttributesProvider
    {
        public int PropertyReads { get; private set; }

        IDictionary<string, object> IExtendedAttributesProvider.GetProperties()
        {
            PropertyReads++;
            return GetProperties();
        }

        public void NotifyPropertiesChanged() => OnPropertyChanged("Properties");
    }

    [TestMethod]
    public void WidthHints_InitializeFromTheItem()
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var item = new WidthTestItem { Title = "Clock" };
        item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = "10ch";
        item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d;
        var viewModel = new DockItemViewModel(new(item), new(context), true, true, DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();

            Assert.AreEqual(new DockLabelLength(10, true), viewModel.LabelWidthConstraints.Minimum);
            Assert.AreEqual(new DockLabelLength(80, false), viewModel.LabelWidthConstraints.Maximum);
            Assert.AreEqual(1, item.PropertyReads);
        }
        finally
        {
            viewModel.SafeCleanup();
            scheduler.ExecuteAllAvailable();
        }
    }

    [TestMethod]
    public void WidthHints_PropertiesNotificationRefreshesAndClearsTheSnapshot()
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var item = new WidthTestItem { Title = "Clock" };
        var viewModel = new DockItemViewModel(new(item), new(context), true, true, DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var notified = false;
            viewModel.PropertyChanged += (_, args) => notified |= args.PropertyName == nameof(viewModel.LabelWidthConstraints);

            item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = "10ch";
            item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth] = "10ch";
            item.NotifyPropertiesChanged();

            scheduler.ExecuteUntil(() => notified);
            Assert.AreEqual((60d, 60d), viewModel.LabelWidthConstraints.Resolve(6, 24, 100));

            notified = false;
            item.GetProperties().Clear();
            item.NotifyPropertiesChanged();

            scheduler.ExecuteUntil(() => notified);
            Assert.AreSame(DockLabelWidthConstraints.Default, viewModel.LabelWidthConstraints);
        }
        finally
        {
            viewModel.SafeCleanup();
            scheduler.ExecuteAllAvailable();
        }
    }

    [TestMethod]
    [DataRow(80d, 80d)]
    [DataRow(0d, 0d)]
    [DataRow("12ch", 72d)]
    [DataRow("1200sqh", 72d)]
    public void WidthHints_ToolkitHelpersUpdateAndClearTheSnapshot(object width, double expectedWidth)
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var item = new ListItem { Title = "CPU" };
        var viewModel = new DockItemViewModel(new(item), new(context), true, true, DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var notified = false;
            viewModel.PropertyChanged += (_, args) => notified |= args.PropertyName == nameof(viewModel.LabelWidthConstraints);

            if (width is string text)
            {
                item.SetDockLabelWidth(text);
            }
            else
            {
                item.SetDockLabelWidth((double)width);
            }

            scheduler.ExecuteUntil(() => notified);
            Assert.AreEqual((expectedWidth, expectedWidth), viewModel.LabelWidthConstraints.Resolve(6, 24, 100));

            notified = false;
            item.ClearDockLabelWidth();

            scheduler.ExecuteUntil(() => notified);
            Assert.AreSame(DockLabelWidthConstraints.Default, viewModel.LabelWidthConstraints);
        }
        finally
        {
            viewModel.SafeCleanup();
            scheduler.ExecuteAllAvailable();
        }
    }

    [TestMethod]
    public void WidthHints_LabelUpdatesDoNotReadPropertiesOrReplaceTheSnapshot()
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var item = new WidthTestItem { Title = "Clock" };
        item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = "10ch";
        var viewModel = new DockItemViewModel(new(item), new(context), true, true, DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var original = viewModel.LabelWidthConstraints;

            for (var i = 0; i < 100; i++)
            {
                item.Title = i.ToString(CultureInfo.InvariantCulture);
                item.Subtitle = $"Value: {i}";
            }

            Assert.AreEqual(1, item.PropertyReads);
            Assert.AreSame(original, viewModel.LabelWidthConstraints);
        }
        finally
        {
            viewModel.SafeCleanup();
            scheduler.ExecuteAllAvailable();
        }
    }

    [TestMethod]
    public void PresentationHints_TargetedAndFullBagNotificationsRefreshIndependently()
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var item = new WidthTestItem { Title = "1.00%" };
        var viewModel = new DockItemViewModel(new(item), new(context), true, true, DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            Assert.IsFalse(viewModel.UseTabularDigits);
            Assert.IsFalse(viewModel.UseTrailingLabelAlignment);

            var tabularDigitsNotified = false;
            var trailingAlignmentNotified = false;
            viewModel.PropertyChanged += (_, args) =>
            {
                tabularDigitsNotified |= args.PropertyName == nameof(viewModel.UseTabularDigits);
                trailingAlignmentNotified |= args.PropertyName == nameof(viewModel.UseTrailingLabelAlignment);
            };

            item.SetDockLabelTabularDigits();

            scheduler.ExecuteUntil(() => tabularDigitsNotified);
            Assert.IsTrue(viewModel.UseTabularDigits);
            Assert.IsFalse(viewModel.UseTrailingLabelAlignment);
            Assert.IsFalse(trailingAlignmentNotified);

            tabularDigitsNotified = false;
            item.GetProperties().Remove(WellKnownExtensionAttributes.DockLabelTabularDigits);
            item.GetProperties()[WellKnownExtensionAttributes.DockLabelTrailingAlignment] = true;
            item.NotifyPropertiesChanged();

            scheduler.ExecuteUntil(() => tabularDigitsNotified && trailingAlignmentNotified);
            Assert.IsFalse(viewModel.UseTabularDigits);
            Assert.IsTrue(viewModel.UseTrailingLabelAlignment);
        }
        finally
        {
            viewModel.SafeCleanup();
            scheduler.ExecuteAllAvailable();
        }
    }

    [TestMethod]
    public void WidthHints_ListBandUsesTheChildItemAndRefreshesItInPlace()
    {
        var fixture = CreateBandFixture();
        try
        {
            var item = new WidthTestItem { Title = "Counter" };
            item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = 80d;
            item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d;
            fixture.Page.SetItem(item);
            fixture.Page.TriggerItemsChanged();
            fixture.Scheduler.ExecuteUntil(() => fixture.Band.Items[0].Title == "Counter");

            var dockItem = fixture.Band.Items[0];
            Assert.AreEqual((80d, 80d), dockItem.LabelWidthConstraints.Resolve(6, 24, 100));

            item.GetProperties()[WellKnownExtensionAttributes.DockMinLabelWidth] = 120d;
            item.GetProperties()[WellKnownExtensionAttributes.DockMaxLabelWidth] = 120d;
            item.NotifyPropertiesChanged();

            Assert.AreSame(dockItem, fixture.Band.Items[0]);
            Assert.AreEqual((120d, 120d), dockItem.LabelWidthConstraints.Resolve(6, 24, 100));
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }
}
