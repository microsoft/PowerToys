// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class DockBandViewModelLifecycleTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed partial class BlockingListPage : ListPage, IDisposable
    {
        private readonly ManualResetEventSlim _getItemsEntered = new();
        private readonly ManualResetEventSlim _releaseGetItems = new();
        private int _blockNextGetItems;
        private int _getItemsCallCount;

        public int GetItemsCallCount => Volatile.Read(ref _getItemsCallCount);

        public override IListItem[] GetItems()
        {
            Interlocked.Increment(ref _getItemsCallCount);
            if (Interlocked.Exchange(ref _blockNextGetItems, 0) != 0)
            {
                _getItemsEntered.Set();
                if (!_releaseGetItems.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to resume Dock band initialization.");
                }
            }

            return [];
        }

        public void BlockNextGetItems()
        {
            _getItemsEntered.Reset();
            _releaseGetItems.Reset();
            Interlocked.Exchange(ref _blockNextGetItems, 1);
        }

        public bool WaitForGetItems() => _getItemsEntered.Wait(TimeSpan.FromSeconds(5));

        public void ReleaseGetItems() => _releaseGetItems.Set();

        public void TriggerItemsChanged() => RaiseItemsChanged();

        public void Dispose()
        {
            _getItemsEntered.Dispose();
            _releaseGetItems.Dispose();
        }
    }

    [TestMethod]
    public async Task CleanupDuringInitialization_DoesNotSubscribeAfterCleanup()
    {
        var context = new TestPageContext();
        var page = new BlockingListPage
        {
            Id = "test.dock.lifecycle",
            Name = "Lifecycle test",
            Title = "Lifecycle test",
        };
        var root = new CommandItemViewModel(
            new(new CommandItem(page) { Title = page.Title }),
            new(context),
            DefaultContextMenuFactory.Instance);
        root.SlowInitializeProperties();

        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(new SettingsModel());
        var band = new DockBandViewModel(
            root,
            new(context),
            new DockBandSettings { ProviderId = "test", CommandId = page.Id },
            settingsService.Object,
            DefaultContextMenuFactory.Instance);

        try
        {
            page.BlockNextGetItems();
            var initialization = Task.Run(band.InitializeProperties);

            Assert.IsTrue(page.WaitForGetItems(), "Dock band initialization did not reach GetItems().");
            band.SafeCleanup();
            page.ReleaseGetItems();

            var completed = await Task.WhenAny(initialization, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(initialization, completed, "Dock band initialization did not finish.");
            await initialization;

            var callsAfterInitialization = page.GetItemsCallCount;
            page.TriggerItemsChanged();

            Assert.AreEqual(callsAfterInitialization, page.GetItemsCallCount);
        }
        finally
        {
            page.ReleaseGetItems();
            band.SafeCleanup();
            root.SafeCleanup();
            page.Dispose();
        }
    }
}
