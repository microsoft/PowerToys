// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.ObjectModel.WinUI3;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Data.Json;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed partial class PowerScriptsFallbackNavigationTests
{
    [TestMethod]
    public void OpenedPage_RetainsSnapshotAfterSearchCancellationUntilPageCleanup()
    {
        using var queryCancellation = new CancellationTokenSource();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var snapshot = new RecordingFallbackSnapshot();
        var lease = new FallbackSnapshotLease(snapshot);
        var resultContext = new FallbackQueryContext(host, provider, new object(), lease, queryCancellation.Token);
        var page = CreateFactory().TryCreatePageViewModel(new TestContentPage(), true, host, provider, resultContext);
        lease.ReleaseOwner();

        try
        {
            Assert.IsNotNull(page);
            queryCancellation.Cancel();

            Assert.IsFalse(resultContext.CanInvoke);
            Assert.IsNotNull(page.FallbackContext);
            Assert.IsTrue(page.FallbackContext.CanInvoke);
            Assert.AreSame(host, page.FallbackContext.ExtensionHost);
            Assert.AreSame(provider, page.FallbackContext.ProviderContext);
            Assert.AreSame(lease, page.FallbackContext.SnapshotLease);
            Assert.AreEqual(0, snapshot.DisposeCount);
        }
        finally
        {
            page?.SafeCleanup();
        }

        Assert.AreEqual(1, snapshot.DisposeCount);
        Assert.IsNull(lease.Acquire());
    }

    [TestMethod]
    public async Task OpenedPage_FormSubmissionSurvivesSearchCancellationAndPreservesItsSource()
    {
        using var queryCancellation = new CancellationTokenSource();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var snapshot = new RecordingFallbackSnapshot();
        var lease = new FallbackSnapshotLease(snapshot);
        var resultContext = new FallbackQueryContext(host, provider, new object(), lease, queryCancellation.Token);
        var page = CreateFactory().TryCreatePageViewModel(new TestContentPage(), true, host, provider, resultContext)
            as CommandPaletteContentPageViewModel;
        lease.ReleaseOwner();
        var formModel = new RecordingFormContent();
        ContentFormViewModel? form = null;
        var recipient = new ResultRecipient();
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(recipient);

        try
        {
            Assert.IsNotNull(page);
            form = page.ViewModelFromContent(formModel, new(page)) as ContentFormViewModel;
            Assert.IsNotNull(form);
            queryCancellation.Cancel();

            form.HandleSubmit(new AdaptiveExecuteAction { Id = "run" }, new JsonObject());
            var message = await recipient.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.AreEqual("run", formModel.LastActionId);
            Assert.AreEqual(CommandResultKind.KeepOpen, message.Result.Unsafe!.Kind);
            Assert.AreSame(page, message.Context?.Page);
            Assert.AreSame(host, message.FallbackContext?.ExtensionHost);
            Assert.AreSame(provider, message.FallbackContext?.ProviderContext);
            Assert.IsTrue(message.FallbackContext?.CanInvoke == true);
            Assert.AreEqual(0, snapshot.DisposeCount);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            form?.SafeCleanup();
            page?.SafeCleanup();
        }
    }

    [TestMethod]
    public void OpenedPage_CommandsKeepTheirOwnInvocationSourceAfterSearchCancellation()
    {
        using var queryCancellation = new CancellationTokenSource();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var rootItem = new ListItem(new NoOpCommand());
        var commandItem = new CommandContextItem(new NoOpCommand { Name = "Copy output" });
        var resultContext = new FallbackQueryContext(host, provider, rootItem, queryCancellation.Token);
        var page = CreateFactory().TryCreatePageViewModel(
            new TestContentPage { Commands = [commandItem] },
            true,
            host,
            provider,
            resultContext) as CommandPaletteContentPageViewModel;

        try
        {
            Assert.IsNotNull(page);
            page.InitializeProperties();
            queryCancellation.Cancel();
            Assert.IsNotNull(page.PrimaryCommand);
            var message = new PerformCommandMessage(page.PrimaryCommand);
            var navigation = new PageNavigationService(CreateFactory(), new Mock<IAppHostService>(MockBehavior.Strict).Object);

            Assert.IsFalse(resultContext.CanInvoke);
            Assert.IsTrue(message.FallbackContext?.CanInvoke == true);
            Assert.AreSame(commandItem, message.CommandContext);
            Assert.AreSame(page, message.Context?.Page);
            Assert.AreSame(host, navigation.ResolveHost(message, null));
            Assert.AreSame(provider, navigation.ResolveProviderContext(message, null));
        }
        finally
        {
            page?.SafeCleanup();
        }
    }

    [TestMethod]
    public void OpenedParametersPage_InitialCommandUsesThePageLifetime()
    {
        using var queryCancellation = new CancellationTokenSource();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var resultContext = new FallbackQueryContext(host, provider, new object(), queryCancellation.Token);
        var page = CreateFactory().TryCreatePageViewModel(new TestParametersPage(), true, host, provider, resultContext)
            as ParametersPageViewModel;

        try
        {
            Assert.IsNotNull(page);
            queryCancellation.Cancel();

            Assert.IsFalse(resultContext.CanInvoke);
            Assert.IsTrue(page.FallbackContext?.CanInvoke == true);
            Assert.AreSame(page.FallbackContext, page.Command.FallbackContext);
            Assert.IsTrue(page.Command.FallbackContext?.CanInvoke == true);
        }
        finally
        {
            page?.SafeCleanup();
        }
    }

    [TestMethod]
    public void CanceledResult_CannotOpenANewPageOrKeepItsSnapshotAlive()
    {
        using var queryCancellation = new CancellationTokenSource();
        var host = new TestAppExtensionHost();
        var provider = Mock.Of<ICommandProviderContext>();
        var snapshot = new RecordingFallbackSnapshot();
        var lease = new FallbackSnapshotLease(snapshot);
        var resultContext = new FallbackQueryContext(host, provider, new object(), lease, queryCancellation.Token);
        PageViewModel? page = null;
        queryCancellation.Cancel();

        try
        {
            page = CreateFactory().TryCreatePageViewModel(new TestContentPage(), true, host, provider, resultContext);

            Assert.IsNull(page);
            Assert.AreEqual(0, snapshot.DisposeCount);
        }
        finally
        {
            page?.SafeCleanup();
            lease.ReleaseOwner();
        }

        Assert.AreEqual(1, snapshot.DisposeCount);
    }

    private static CommandPalettePageViewModelFactory CreateFactory() =>
        new(new ImmediateTaskScheduler(), DefaultContextMenuFactory.Instance);

    private sealed class ImmediateTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks() => [];

        protected override void QueueTask(Task task) => TryExecuteTask(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private sealed partial class TestParametersPage : ParametersPage
    {
        public override IListItem Command { get; } = new ListItem(new NoOpCommand());

        public override IParameterRun[] Parameters { get; } = [];
    }

    private sealed partial class RecordingFormContent : FormContent
    {
        public string? LastActionId { get; private set; }

        public override ICommandResult SubmitAction(string actionId, string inputs, string data)
        {
            LastActionId = actionId;
            return CommandResult.KeepOpen();
        }
    }

    internal sealed class ResultRecipient : IRecipient<HandleCommandResultMessage>
    {
        public TaskCompletionSource<HandleCommandResultMessage> MessageReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Receive(HandleCommandResultMessage message) => MessageReceived.TrySetResult(message);
    }

    private sealed partial class RecordingFallbackSnapshot : IFallbackCommandResult, IDisposable
    {
        private int _disposeCount;

        public string Query => "greet";

        public string QueryId => "query-id";

        public IListItem[] Items => [];

        public bool HasMoreItems => false;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> LoadMoreItemsAsync(uint requestedItemCount)
            => throw new NotSupportedException();

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}
