// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ContentPageViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private sealed partial class SwappableContentPage : ContentPage
    {
        public IContent[] Content { get; set; } = [];

        public override IContent[] GetContent() => Content;

        public void TriggerItemsChanged() => RaiseItemsChanged(Content.Length);
    }

    /// <summary>
    /// Reports whether a view-model is still subscribed to it. Something that
    /// was dropped without cleanup shows up here as a live handler.
    /// </summary>
    private sealed partial class CountingMarkdown(string body) : IMarkdownContent
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove => _propChanged -= value;
        }

        public int HandlerCount => _propChanged?.GetInvocationList().Length ?? 0;

        public string Body => body;
    }

    /// <summary>
    /// Details that report whether a view-model is still subscribed.
    /// </summary>
    private sealed partial class CountingDetails : IDetails, INotifyPropChanged
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove => _propChanged -= value;
        }

        public int HandlerCount => _propChanged?.GetInvocationList().Length ?? 0;

        public IIconInfo HeroImage => new IconInfo(string.Empty);

        public string Title => "Counting";

        public string Body => "Counting body";

        public IDetailsElement[] Metadata => [];
    }

    private sealed partial class ThrowingDetails : IDetails
    {
        public IIconInfo HeroImage => throw new InvalidOperationException("boom");

        public string Title => "Throwing";

        public string Body => "Throwing body";

        public IDetailsElement[] Metadata => [];
    }

    private sealed class ShowDetailsRecipient
    {
        private int _messageCount;

        public int MessageCount => Volatile.Read(ref _messageCount);

        public void RecordMessage() => Interlocked.Increment(ref _messageCount);
    }

    private static CommandPaletteContentPageViewModel CreateContentViewModel(SwappableContentPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);

    [TestMethod]
    public void ContentUpdate_ReleasesDisplacedContent()
    {
        var first = new CountingMarkdown("first");
        var page = new SwappableContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Content = [first],
        };

        var viewModel = CreateContentViewModel(page);
        viewModel.InitializeProperties();

        // FetchContent hands the collection update to the page scheduler.
        SpinWait.SpinUntil(() => first.HandlerCount == 1, TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, first.HandlerCount, "the initial content should be subscribed");

        page.Content = [new CountingMarkdown("second")];
        page.TriggerItemsChanged();

        SpinWait.SpinUntil(() => first.HandlerCount == 0, TimeSpan.FromSeconds(2));
        Assert.AreEqual(0, first.HandlerCount, "content displaced by an update was left subscribed");
    }

    [TestMethod]
    public void DetailsUpdate_ReleasesDisplacedDetails()
    {
        var first = new CountingDetails();
        var page = new SwappableContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Details = first,
        };

        var viewModel = CreateContentViewModel(page);
        viewModel.InitializeProperties();

        Assert.AreEqual(1, first.HandlerCount, "the initial details should be subscribed");

        page.Details = new CountingDetails();

        Assert.AreEqual(0, first.HandlerCount, "details displaced by an update were left subscribed");
    }

    [TestMethod]
    public void DetailsUpdate_InitializesTheReplacement()
    {
        var page = new SwappableContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Details = new Details { Title = "First", Body = "First body" },
        };

        var viewModel = CreateContentViewModel(page);
        viewModel.InitializeProperties();

        page.Details = new Details { Title = "Second", Body = "Second body" };

        Assert.AreEqual("Second body", viewModel.Details?.Body, "the replacement details were never initialized");
    }

    [TestMethod]
    public void DetailsUpdate_CleansUpDisplacedDetails_WhenReplacementInitializationThrows()
    {
        var first = new CountingDetails();
        var page = new SwappableContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Details = first,
        };

        var recipient = new ShowDetailsRecipient();
        WeakReferenceMessenger.Default.Register<ShowDetailsRecipient, ShowDetailsMessage>(recipient, static (r, _) => r.RecordMessage());

        try
        {
            var viewModel = CreateContentViewModel(page);
            viewModel.InitializeProperties();

            Assert.AreEqual(1, first.HandlerCount, "the initial details should be subscribed");
            Assert.IsTrue(
                SpinWait.SpinUntil(() => recipient.MessageCount > 0, TimeSpan.FromSeconds(2)),
                "initial details message was not sent");
            var initialShowMessages = recipient.MessageCount;

            page.Details = new ThrowingDetails();

            SpinWait.SpinUntil(() => first.HandlerCount == 0, TimeSpan.FromSeconds(2));
            Assert.AreEqual(0, first.HandlerCount, "displaced details were left subscribed after replacement initialization failed");
            Assert.IsTrue(
                SpinWait.SpinUntil(() => recipient.MessageCount == initialShowMessages + 1, TimeSpan.FromSeconds(2)),
                "details update message was not sent when replacement initialization failed");
            Assert.AreEqual(initialShowMessages + 1, recipient.MessageCount, "unexpected details message count");
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    private static CommandContextItem Command(string name) => new(new NoOpCommand { Name = name });

    private static ContentPageViewModel CreateViewModel(TestContentPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);

    [TestMethod]
    public void AllCommandsAndMoreCommands_ReturnCachedSnapshots()
    {
        // Content pages should expose stable snapshots, not the live Commands
        // list, so repeated reads don't allocate and callers can't observe
        // in-place list mutations.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var allCommands = viewModel.AllCommands;
        var moreCommands = viewModel.MoreCommands;

        Assert.AreSame(allCommands, viewModel.AllCommands);
        Assert.AreSame(moreCommands, viewModel.MoreCommands);
        Assert.AreEqual(2, allCommands.Count);
        Assert.AreEqual(1, moreCommands.Count);
        Assert.AreEqual("Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand?.Name);
    }

    [TestMethod]
    public void CommandsUpdate_RefreshesSnapshotsConsistently()
    {
        // Updating the model commands should swap in a new coherent snapshot.
        // The old snapshots stay intact, and the new cached values agree on
        // counts, primary/secondary commands, and "has more" state.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var oldAllCommands = viewModel.AllCommands;
        var oldMoreCommands = viewModel.MoreCommands;

        page.Commands =
        [
            Command("Updated Primary"),
            new Separator("Group"),
            Command("Updated Secondary"),
        ];

        Assert.AreEqual(2, oldAllCommands.Count);
        Assert.AreEqual(1, oldMoreCommands.Count);

        Assert.AreEqual(3, viewModel.AllCommands.Count);
        Assert.AreEqual(2, viewModel.MoreCommands.Count);
        Assert.IsTrue(viewModel.HasCommands);
        Assert.IsTrue(viewModel.HasMoreCommands);
        Assert.AreEqual("Updated Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommandName);
    }
}
