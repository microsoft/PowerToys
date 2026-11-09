// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TrayPaletteViewModelTests
{
    private sealed class ImmediateTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks() => [];

        protected override void QueueTask(Task task) => TryExecuteTask(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
    }

    [TestMethod]
    [DataRow("supported")]
    [DataRow("null")]
    [DataRow("unsupported")]
    [DataRow("failure")]
    public async Task SmallContent_UsesExistingTrayPinsAndFallsBackToTheirButtons(string kind)
    {
        using var unsubscribed = new SemaphoreSlim(0);
        var form = new TrayFormContent(unsubscribed);
        var requests = 0;
        var size = new Size(TrayPaletteLayout.Width, TrayPaletteLayout.SmallContentHeight);
        var provider = new TestProvider("provider")
        {
            SmallContentFactory = (id, availableSpace) =>
            {
                Assert.AreEqual("command", id);
                Assert.AreEqual(size, availableSpace);
                requests++;
                return kind switch
                {
                    "supported" => form,
                    "null" => null,
                    "unsupported" => new UnsupportedTrayContent(),
                    _ => throw new InvalidOperationException("Small content failed."),
                };
            },
        };
        var settings = new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(settings);
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        await model.RefreshAsync();

        await model.LoadSmallContentAsync(new ImmediateTaskScheduler(), size);

        Assert.HasCount(1, model.Items);
        var item = model.Items[0];
        Assert.AreEqual(1, requests);
        Assert.AreEqual(kind == "supported", item.HasSmallContent);
        Assert.AreEqual(kind != "supported", item.IsButton);
        Assert.AreEqual("command", item.Pin.CommandId);
        Assert.AreEqual("provider", item.CreateMessage().Context?.ProviderContext.ProviderId);
        Assert.IsFalse(model.HasLoadError);
        if (kind == "supported")
        {
            Assert.IsNotNull(item.SmallContent);
            Assert.HasCount(1, item.SmallContent.Content);
            Assert.AreEqual(1, form.SubscriberCount);

            await model.LoadSmallContentAsync(new ImmediateTaskScheduler(), size);
            Assert.AreEqual(1, requests, "Refreshing visible quick actions must not recreate live content.");
        }

        model.ClearSmallContent();
        Assert.IsTrue(item.IsButton);
        Assert.IsNull(item.SmallContent);
        if (kind == "supported")
        {
            Assert.IsTrue(await unsubscribed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, form.SubscriberCount);

            await model.LoadSmallContentAsync(new ImmediateTaskScheduler(), size);
            Assert.IsTrue(item.HasSmallContent);
            Assert.AreEqual(2, requests, "Reopening quick actions must fetch fresh content.");
            model.Dispose();
            Assert.IsTrue(await unsubscribed.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, form.SubscriberCount);
        }
    }

    [TestMethod]
    public async Task SmallContent_DismissDuringFetchDoesNotPublishOrSubscribe()
    {
        using var unsubscribed = new SemaphoreSlim(0);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<IContent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var form = new TrayFormContent(unsubscribed);
        var provider = new TestProvider("provider")
        {
            SmallContentFactory = (_, _) =>
            {
                started.TrySetResult();
                return result.Task.GetAwaiter().GetResult();
            },
        };
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(new SettingsModel { TrayPalette = new() { Commands = [new("provider", "command")] } });
        using var services = CreateServices(service.Object);
        using var manager = new TopLevelCommandManager(services, [new BuiltInExtensionService([provider], TaskScheduler.Default)]);
        await manager.LoadBuiltInProvidersAsync();
        using var model = new TrayPaletteViewModel(service.Object, manager, DefaultContextMenuFactory.Instance);
        await model.RefreshAsync();

        var loading = model.LoadSmallContentAsync(new ImmediateTaskScheduler(), new(TrayPaletteLayout.Width, TrayPaletteLayout.SmallContentHeight));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            model.ClearSmallContent();
        }
        finally
        {
            result.TrySetResult(form);
            await loading;
        }

        Assert.IsTrue(model.Items[0].IsButton);
        Assert.IsNull(model.Items[0].SmallContent);
        Assert.AreEqual(0, form.SubscriberCount);
    }

    private sealed partial class UnsupportedTrayContent : BaseObservable, IContent
    {
    }

    private sealed partial class TrayFormContent(SemaphoreSlim unsubscribed) : IFormContent
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public int SubscriberCount => _propChanged?.GetInvocationList().Length ?? 0;

        public string TemplateJson => """{"type":"AdaptiveCard","version":"1.5","body":[]}""";

        public string DataJson => "{}";

        public string StateJson => "{}";

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove
            {
                _propChanged -= value;
                unsubscribed.Release();
            }
        }

        public ICommandResult SubmitForm(string inputs, string data) => CommandResult.KeepOpen();
    }
}
