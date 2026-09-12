// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public sealed class ClipboardHistoryListPageTests
{
    [TestMethod]
    public async Task GetItems_FileChanged_RefreshesDetailsWithoutReloadingClipboardContent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "before");
            File.SetLastWriteTime(path, new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local));
            var settings = new TestSettings();
            var data = CreateTextData(path);
            var item = new ClipboardListItem(new ClipboardItem { Content = path, Settings = settings, Item = null }, settings, data);
            var loads = 0;
            using var source = new TestClipboardHistorySource
            {
                Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>(
                    [new("file", _ =>
                    {
                        loads++;
                        return Task.FromResult<IListItem>(item);
                    })]),
            };
            await using var page = new ClipboardHistoryListPage(settings, source, new ClipboardHistoryWorker());
            await LoadPageAsync(page);
            var original = page.GetItems()[0];
            var originalDetails = original.Details;
            var originalCommand = original.Command;
            Assert.AreEqual(SizeFormatter.FormatSize(6), GetDetail(originalDetails, Resources.metadata_file_system_size_key));
            var originalModified = GetDetail(originalDetails, Resources.metadata_file_system_modified_key);
            var detailsNotifications = 0;
            item.PropChanged += (_, args) => detailsNotifications += args.PropertyName == nameof(item.Details) ? 1 : 0;

            File.WriteAllText(path, new string('x', 8192));
            var modified = new DateTime(2025, 2, 2, 13, 0, 0, DateTimeKind.Local);
            File.SetLastWriteTime(path, modified);
            var reopened = page.GetItems()[0];
            var updatedDetails = reopened.Details;

            Assert.AreSame(original, reopened);
            Assert.AreSame(originalCommand, reopened.Command);
            Assert.AreSame(data, item.DataPackageView);
            Assert.AreEqual(path, await data.GetTextAsync());
            Assert.AreEqual(1, loads);
            Assert.AreEqual(1, detailsNotifications);
            Assert.AreNotSame(originalDetails, updatedDetails);
            Assert.AreEqual(SizeFormatter.FormatSize(8192), GetDetail(updatedDetails, Resources.metadata_file_system_size_key));
            Assert.AreEqual(modified.ToString(CultureInfo.CurrentCulture), GetDetail(updatedDetails, Resources.metadata_file_system_modified_key));
            Assert.AreNotEqual(originalModified, GetDetail(updatedDetails, Resources.metadata_file_system_modified_key));
            Assert.AreEqual(SizeFormatter.FormatSize(6), GetDetail(originalDetails, Resources.metadata_file_system_size_key));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetItems_FileRemovedAndRecreated_RefreshesActionsAndMetadata()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "before");
            var settings = new TestSettings();
            var item = new ClipboardListItem(new ClipboardItem { Content = path, Settings = settings, Item = null }, settings, CreateTextData(path));
            using var source = new TestClipboardHistorySource
            {
                Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([new("file", _ => Task.FromResult<IListItem>(item))]),
            };
            await using var page = new ClipboardHistoryListPage(settings, source, new ClipboardHistoryWorker());
            await LoadPageAsync(page);
            var initial = page.GetItems()[0];
            var originalCommand = initial.Command;
            Assert.IsTrue(HasShowInFolderCommand(initial));
            Assert.IsTrue(initial.Details.Metadata.Any(detail => detail.Key == Resources.metadata_file_system_size_key));

            File.Delete(path);
            var removed = page.GetItems()[0];
            Assert.AreSame(initial, removed);
            Assert.AreSame(originalCommand, removed.Command);
            Assert.IsFalse(HasShowInFolderCommand(removed));
            Assert.IsFalse(removed.Details.Metadata.Any(detail => detail.Key == Resources.metadata_file_system_size_key));

            File.WriteAllText(path, "recreated");
            var recreated = page.GetItems()[0];
            Assert.AreSame(initial, recreated);
            Assert.AreSame(originalCommand, recreated.Command);
            Assert.IsTrue(HasShowInFolderCommand(recreated));
            Assert.AreEqual(SizeFormatter.FormatSize(9), GetDetail(recreated.Details, Resources.metadata_file_system_size_key));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GetItems_UnchangedTextOrImage_PreservesDetailsAndCommands(bool image)
    {
        var settings = new TestSettings();
        using var imageStream = new InMemoryRandomAccessStream();
        if (image)
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, imageStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 1, 1, 96, 96, [0, 0, 0, 255]);
            await encoder.FlushAsync();
        }

        var item = new ClipboardListItem(
            new ClipboardItem
            {
                Content = image ? null : "unchanged clipboard text",
                ImageData = image ? RandomAccessStreamReference.CreateFromStream(imageStream) : null,
                Settings = settings,
                Item = null,
            },
            settings,
            CreateTextData("unchanged clipboard text"));
        using var source = new TestClipboardHistorySource
        {
            Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([new("item", _ => Task.FromResult<IListItem>(item))]),
        };
        await using var page = new ClipboardHistoryListPage(settings, source, new ClipboardHistoryWorker());
        await LoadPageAsync(page);
        var first = page.GetItems()[0];
        var details = first.Details;
        var commands = first.MoreCommands;

        var reopened = page.GetItems()[0];

        Assert.AreSame(first, reopened);
        Assert.AreSame(details, reopened.Details);
        Assert.AreSame(commands, reopened.MoreCommands);
    }

    [TestMethod]
    public async Task GetItems_RepeatedFetches_ReusesSnapshotWithOneSettingsSubscriber()
    {
        var settings = new TestSettings();
        var reads = 0;
        var item = Mock.Of<IListItem>();
        using var source = new TestClipboardHistorySource
        {
            Read = _ =>
            {
                reads++;
                return Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([new("a", _ => Task.FromResult(item))]);
            },
        };
        await using var page = new ClipboardHistoryListPage(settings, source, new ClipboardHistoryWorker());
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, _) => published.TrySetResult();

        page.GetItems();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var i = 0; i < 20; i++)
        {
            Assert.AreSame(item, page.GetItems()[0]);
        }

        Assert.AreEqual(1, reads);
        Assert.AreEqual(1, settings.Subscribers);
        Assert.AreEqual(1, source.HistorySubscribers);
        Assert.AreEqual(1, source.EnabledSubscribers);

        settings.RaiseChanged();
        Assert.AreEqual(1, reads);
        Assert.AreSame(item, page.GetItems()[0]);

        await page.DisposeAsync();
        Assert.AreEqual(0, settings.Subscribers);
        Assert.AreEqual(0, source.HistorySubscribers);
        Assert.AreEqual(0, source.EnabledSubscribers);
        Assert.IsTrue(source.IsDisposed);
        Assert.IsEmpty(page.GetItems());
    }

    [TestMethod]
    public async Task DisposeAsync_UnopenedPage_DetachesSettingsAndHistoryEvents()
    {
        var settings = new TestSettings();
        using var source = new TestClipboardHistorySource();
        var worker = new ClipboardHistoryWorker();
        await using var page = new ClipboardHistoryListPage(settings, source, worker);
        var notifications = 0;
        page.ItemsChanged += (_, _) => notifications++;

        await page.DisposeAsync();
        settings.RaiseChanged();
        source.RaiseHistoryChanged();
        source.RaiseHistoryEnabledChanged();

        Assert.AreEqual(0, notifications);
        Assert.AreEqual(0, settings.Subscribers);
        Assert.AreEqual(0, source.HistorySubscribers);
        Assert.AreEqual(0, source.EnabledSubscribers);
        Assert.IsTrue(source.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunAsync(() => Task.CompletedTask));
    }

    private static DataPackageView CreateTextData(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        return data.GetView();
    }

    private static string GetDetail(IDetails details, string key)
    {
        var element = details.Metadata.Single(detail => detail.Key == key);
        Assert.IsInstanceOfType<DetailsLink>(element.Data, out var link);
        return link.Text;
    }

    private static bool HasShowInFolderCommand(IListItem item) =>
        item.MoreCommands.OfType<ICommandContextItem>().Any(context => context.Command is ShowFileInFolderCommand);

    private static async Task LoadPageAsync(ClipboardHistoryListPage page)
    {
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnItemsChanged(object sender, IItemsChangedEventArgs args) => published.TrySetResult();
        page.ItemsChanged += OnItemsChanged;
        try
        {
            page.GetItems();
            await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            page.ItemsChanged -= OnItemsChanged;
        }
    }

    private sealed class TestSettings : IClipboardHistorySettings
    {
        private EventHandler _changed;

        public event EventHandler Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;

        public bool KeepAfterPaste => false;

        public bool DeleteFromHistoryRequiresConfirmation => true;

        public PrimaryAction PrimaryAction => PrimaryAction.Default;

        public void RaiseChanged() => _changed?.Invoke(this, EventArgs.Empty);
    }
}
