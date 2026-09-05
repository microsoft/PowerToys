// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class DetailsLifecycleTests
{
    private readonly TestPageContext _context = new();
    private readonly List<ExtensionObjectViewModel> _owned = [];

    [TestCleanup]
    public void Cleanup()
    {
        _owned.ForEach(vm => vm.SafeCleanup());
        _context.Scheduler.RunAll();
    }

    [TestMethod]
    public void RepeatedSelection_ReusesLiveDetailsAndReadsSnapshotOnce()
    {
        var markdown = new TrackedMarkdown();
        var details = new TrackedDetails { Content = [markdown] };
        var item = new TrackedListItem { Details = details };
        var vm = CreateItem(item);

        vm.SlowInitializeProperties();
        var original = vm.Details;
        for (var i = 0; i < 10; i++)
        {
            vm.SlowInitializeProperties();
        }

        Assert.AreSame(original, vm.Details);
        Assert.AreEqual(1, item.DetailsReads);
        Assert.AreEqual(1, details.BodyReads);
        Assert.AreEqual(1, details.Adds);
        Assert.AreEqual(1, markdown.Adds);

        details.BodyValue = "live details";
        details.Raise(nameof(IDetails.Body));
        markdown.BodyValue = "live markdown";
        markdown.Raise(nameof(IMarkdownContent.Body));
        _context.Scheduler.RunAll();

        Assert.AreEqual("live details", vm.Details?.Body);
        Assert.IsInstanceOfType<ContentMarkdownViewModel>(vm.Details?.Content.Single(), out var content);
        Assert.AreEqual("live markdown", content.Body);

        vm.SafeCleanup();
        vm.SafeCleanup();
        Assert.AreEqual(1, details.Removes);
        Assert.AreEqual(1, markdown.Removes);
    }

    [TestMethod]
    public void RepeatedSelection_WithoutDetailsDoesNotRepeatExtensionReads()
    {
        var item = new TrackedListItem();
        var vm = CreateItem(item);

        vm.SlowInitializeProperties();
        vm.SlowInitializeProperties();

        Assert.IsNull(vm.Details);
        Assert.AreEqual(1, item.DetailsReads);
    }

    [TestMethod]
    public async Task OverlappingSelectionAndReplacement_OnlyPublishTheCurrentDetails()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var previous = new TrackedDetails { OnBodyRead = () => Block(entered, release) };
        var replacement = new TrackedDetails { BodyValue = "replacement" };
        var item = new TrackedListItem { Details = previous };
        var vm = CreateItem(item);
        var initialization = Task.Run(vm.SlowInitializeProperties);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            vm.SlowInitializeProperties();
            item.Details = replacement;
            Assert.AreEqual(1, item.DetailsReads);
        }
        finally
        {
            release.Set();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.AreEqual("replacement", vm.Details?.Body);
        Assert.AreEqual(0, previous.Subscribers);
        Assert.AreEqual(1, previous.Removes);
        Assert.AreEqual(1, replacement.Subscribers);
        vm.SlowInitializeProperties();
        Assert.AreEqual(1, replacement.Adds);
    }

    [TestMethod]
    public async Task CleanupDuringInitialization_DiscardsPendingWorkAndReleasesChildren()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var markdown = new TrackedMarkdown { OnBodyRead = () => Block(entered, release) };
        var details = new TrackedDetails { Content = [markdown] };
        var vm = CreateItem(new TrackedListItem { Details = details });
        var initialization = Task.Run(vm.SlowInitializeProperties);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            vm.SafeCleanup();
            details.Raise(nameof(Details.Content));
            vm.SlowInitializeProperties();
        }
        finally
        {
            release.Set();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
        }

        _context.Scheduler.RunAll(newestFirst: true);
        Assert.IsNull(vm.Details);
        Assert.AreEqual(0, details.Subscribers);
        Assert.AreEqual(0, markdown.Subscribers);
        Assert.AreEqual(details.Adds, details.Removes);
        Assert.AreEqual(markdown.Adds, markdown.Removes);
    }

    [TestMethod]
    public void Selection_NonObservableDetailsRefreshWithoutLeakingPreviousContent()
    {
        var first = new TrackedMarkdown();
        var second = new TrackedMarkdown();
        var details = new SnapshotDetails { Content = [first] };
        var vm = CreateItem(new TrackedListItem { Details = details });
        vm.SlowInitializeProperties();
        var original = vm.Details;

        details.Body = "new snapshot";
        details.Content = [second];
        vm.SlowInitializeProperties();
        _context.Scheduler.RunAll(newestFirst: true);

        Assert.AreSame(original, vm.Details);
        Assert.AreEqual("new snapshot", vm.Details?.Body);
        Assert.AreEqual(0, first.Subscribers);
        Assert.AreEqual(1, second.Subscribers);
        Assert.IsInstanceOfType<ContentMarkdownViewModel>(vm.Details?.Content.Single(), out var content);
        Assert.AreSame(second, content.Model.Unsafe);
    }

    [TestMethod]
    public void ContentReplacementBeforeUiPublication_DetachesOldGraphAndPublishesLatest()
    {
        var first = new TrackedMarkdown();
        var second = new TrackedMarkdown();
        var details = new TrackedDetails { Content = [first] };
        var vm = Own(new DetailsViewModel(details, new(_context)));
        vm.InitializeProperties();
        var lateCallback = first.Capture(nameof(IMarkdownContent.Body));

        details.Content = [second];
        details.Raise(nameof(Details.Content));
        _context.Scheduler.RunAll(newestFirst: true);

        Assert.AreEqual(0, first.Subscribers);
        Assert.AreEqual(1, second.Subscribers);
        Assert.IsInstanceOfType<ContentMarkdownViewModel>(vm.Content.Single(), out var content);
        Assert.AreSame(second, content.Model.Unsafe);

        var reads = first.BodyReads;
        lateCallback();
        Assert.AreEqual(reads, first.BodyReads);
        second.BodyValue = "still live";
        second.Raise(nameof(IMarkdownContent.Body));
        Assert.AreEqual("still live", content.Body);

        vm.SafeCleanup();
        _context.Scheduler.RunAll(newestFirst: true);
        Assert.IsEmpty(vm.Content);
        Assert.AreEqual(0, second.Subscribers);
    }

    [TestMethod]
    public void MetadataReplacement_CleansOnlyOwnedCommandsAndKeepsSharedModelsAlive()
    {
        var command = new TrackedCommand();
        var replacement = new TrackedCommand();
        var details = new TrackedDetails { Metadata = [Commands(command)] };
        var first = Own(new DetailsViewModel(details, new(_context)));
        var second = Own(new DetailsViewModel(details, new(_context)));
        first.InitializeProperties();
        second.InitializeProperties();
        Assert.AreEqual(2, command.Subscribers);

        first.SafeCleanup();
        Assert.AreEqual(1, command.Subscribers);
        command.Name = "shared model is live";
        command.Raise(nameof(ICommand.Name));
        Assert.IsInstanceOfType<DetailsCommandsViewModel>(second.Metadata.Single(), out var metadata);
        Assert.AreEqual("shared model is live", metadata.Commands.Single().Name);

        details.Metadata = [Commands(replacement)];
        details.Raise(nameof(IDetails.Metadata));
        Assert.AreEqual(0, command.Subscribers);
        Assert.AreEqual(1, replacement.Subscribers);
        second.SafeCleanup();
        second.SafeCleanup();
        Assert.AreEqual(1, replacement.Removes);
    }

    [TestMethod]
    public void NestedTreeChanges_ReleaseReplacedRootAndUnpublishedChildren()
    {
        var root = new TrackedMarkdown();
        var firstChild = new TrackedMarkdown();
        var nextRoot = new TrackedMarkdown();
        var nextChild = new TrackedMarkdown();
        var tree = new TrackedTree { RootContent = root, Children = [firstChild] };
        var details = new TrackedDetails { Content = [tree] };
        var vm = Own(new DetailsViewModel(details, new(_context)));
        vm.InitializeProperties();

        tree.RootContent = nextRoot;
        tree.Raise(nameof(ITreeContent.RootContent));
        tree.Children = [nextChild];
        tree.RaiseItemsChanged();
        _context.Scheduler.RunAll(newestFirst: true);

        Assert.AreEqual(0, root.Subscribers);
        Assert.AreEqual(0, firstChild.Subscribers);
        Assert.IsInstanceOfType<ContentTreeViewModel>(vm.Content.Single(), out var treeVm);
        Assert.IsInstanceOfType<ContentMarkdownViewModel>(treeVm.RootContent, out var rootVm);
        Assert.AreSame(nextRoot, rootVm.Model.Unsafe);
        Assert.IsInstanceOfType<ContentMarkdownViewModel>(treeVm.Children.Single(), out var childVm);
        Assert.AreSame(nextChild, childVm.Model.Unsafe);

        tree.RootContent = null;
        tree.Raise(nameof(ITreeContent.RootContent));
        Assert.IsNull(treeVm.RootContent);
        Assert.AreEqual(0, nextRoot.Subscribers);
        var lateItemsChanged = tree.CaptureItemsChanged();
        vm.SafeCleanup();
        lateItemsChanged();
        _context.Scheduler.RunAll(newestFirst: true);
        Assert.AreEqual(0, tree.Subscribers);
        Assert.AreEqual(0, tree.ItemsSubscribers);
        Assert.AreEqual(0, nextChild.Subscribers);
        Assert.IsEmpty(treeVm.Children);
    }

    [TestMethod]
    public void FailedInitialization_ReleasesPartialGraphAndCannotResubscribe()
    {
        var command = new TrackedCommand();
        var first = new TrackedMarkdown();
        var broken = new TrackedMarkdown { OnBodyRead = () => throw new InvalidOperationException("broken content") };
        var details = new TrackedDetails { Metadata = [Commands(command)], Content = [first, broken] };
        var vm = Own(new DetailsViewModel(details, new(_context)));

        Assert.ThrowsExactly<InvalidOperationException>(vm.InitializeProperties);
        vm.InitializeProperties();
        vm.SafeCleanup();
        _context.Scheduler.RunAll(newestFirst: true);

        Assert.AreEqual(1, details.Adds);
        Assert.AreEqual(1, details.Removes);
        Assert.AreEqual(0, command.Subscribers);
        Assert.AreEqual(0, first.Subscribers);
        Assert.AreEqual(1, broken.Removes);
        Assert.IsEmpty(vm.Content);
        Assert.IsEmpty(vm.Metadata);
    }

    [TestMethod]
    public void FailedReplacement_PreservesPreviousDetailsAndReportsFailure()
    {
        var previous = new TrackedDetails();
        var broken = new TrackedDetails { OnBodyRead = () => throw new InvalidOperationException("broken details") };
        var item = new TrackedListItem { Details = previous };
        var vm = CreateItem(item);
        vm.SlowInitializeProperties();
        var original = vm.Details;

        item.Details = broken;

        Assert.AreSame(original, vm.Details);
        Assert.AreEqual(1, previous.Subscribers);
        Assert.AreEqual(0, broken.Subscribers);
        Assert.HasCount(1, _context.Errors);
        Assert.IsInstanceOfType<InvalidOperationException>(_context.Errors[0]);
        previous.BodyValue = "still available";
        previous.Raise(nameof(IDetails.Body));
        Assert.AreEqual("still available", original?.Body);
    }

    [TestMethod]
    public void CrossThreadNotificationDuringRead_IsDeferredWithoutBlockingTheExtension()
    {
        var details = new TrackedDetails();
        details.OnBodyRead = () =>
        {
            details.OnBodyRead = null;
            var callback = Task.Run(() =>
            {
                details.BodyValue = "updated during read";
                details.Raise(nameof(IDetails.Body));
            });
            Assert.IsTrue(callback.Wait(TimeSpan.FromSeconds(5)));
        };
        var vm = Own(new DetailsViewModel(details, new(_context)));

        vm.InitializeProperties();

        Assert.AreEqual("updated during read", vm.Body);
        Assert.AreEqual(1, details.Subscribers);
    }

    [TestMethod]
    public void ContentInitializationAndCleanup_AreIdempotentAndIgnoreLateCallbacks()
    {
        var markdown = new TrackedMarkdown();
        var vm = Own(new ContentMarkdownViewModel(markdown, new(_context)));
        vm.InitializeProperties();
        vm.InitializeProperties();
        var lateCallback = markdown.Capture(nameof(IMarkdownContent.Body));

        vm.SafeCleanup();
        vm.SafeCleanup();
        vm.InitializeProperties();
        lateCallback();

        Assert.AreEqual(1, markdown.Adds);
        Assert.AreEqual(1, markdown.Removes);
        Assert.AreEqual(1, markdown.BodyReads);
    }

    [TestMethod]
    public void FailedSelection_ReleasesAlreadyInitializedDetails()
    {
        var markdown = new TrackedMarkdown();
        var details = new TrackedDetails { Content = [markdown] };
        var item = new TrackedListItem { Details = details, FailTextToSuggest = true };
        var vm = CreateItem(item);

        Assert.IsFalse(vm.SafeSlowInit());
        vm.SlowInitializeProperties();

        Assert.IsTrue(vm.IsInErrorState);
        Assert.IsNull(vm.Details);
        Assert.AreEqual(1, details.Adds);
        Assert.AreEqual(1, details.Removes);
        Assert.AreEqual(1, markdown.Removes);
    }

    [TestMethod]
    public void ObservableDetails_SizeUpdatesWithoutAnotherSelectionSnapshot()
    {
        var details = new Details { Size = ContentSize.Small };
        var vm = Own(new DetailsViewModel(details, new(_context)));
        vm.InitializeProperties();

        details.Size = ContentSize.Large;

        Assert.AreEqual(ContentSize.Large, vm.Size);
    }

    [TestMethod]
    public void DetailsReplacement_RetargetsShowDetailsAndRemovalDropsTheCommand()
    {
        var page = new ListViewModel(new TestListPage(), _context.Scheduler, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var item = new TrackedListItem { Details = new TrackedDetails() };
        var vm = Own(new ListItemViewModel(item, new(page), DefaultContextMenuFactory.Instance));
        var recipient = new DetailsRecipient();
        WeakReferenceMessenger.Default.Register<DetailsRecipient, ShowDetailsMessage>(recipient, static (r, message) => r.Details = message.Details);
        try
        {
            vm.SlowInitializeProperties();
            item.Details = new TrackedDetails();
            var command = vm.MoreCommands.OfType<CommandContextItemViewModel>()
                .Single(c => c.Command.Id == ShowDetailsCommand.ShowDetailsCommandId);
            Assert.IsInstanceOfType<ShowDetailsCommand>(command.Command.Model.Unsafe, out var showDetails);
            showDetails.Invoke();
            Assert.AreSame(vm.Details, recipient.Details);

            item.Details = null;
            Assert.IsNull(vm.Details);
            Assert.IsFalse(vm.MoreCommands.OfType<CommandContextItemViewModel>()
                .Any(c => c.Command.Id == ShowDetailsCommand.ShowDetailsCommandId));
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            vm.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    private ListItemViewModel CreateItem(IListItem item) =>
        Own(new ListItemViewModel(item, new(_context), DefaultContextMenuFactory.Instance));

    private T Own<T>(T vm)
        where T : ExtensionObjectViewModel
    {
        _owned.Add(vm);
        return vm;
    }

    private static DetailsElement Commands(ICommand command) => new()
    {
        Key = "Commands",
        Data = new DetailsCommands { Commands = [command] },
    };

    private static void Block(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        entered.Set();
        Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
    }

    private sealed class TestPageContext : IPageContext
    {
        public QueuedScheduler Scheduler { get; } = new();

        TaskScheduler IPageContext.Scheduler => Scheduler;

        public List<Exception> Errors { get; } = [];

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) => Errors.Add(ex);
    }

    private sealed class QueuedScheduler : TaskScheduler
    {
        private readonly Lock _gate = new();
        private readonly List<Task> _tasks = [];

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_gate)
            {
                return _tasks.ToArray();
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_gate)
            {
                _tasks.Add(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll(bool newestFirst = false)
        {
            while (true)
            {
                Task task;
                lock (_gate)
                {
                    if (_tasks.Count == 0)
                    {
                        return;
                    }

                    var index = newestFirst ? _tasks.Count - 1 : 0;
                    task = _tasks[index];
                    _tasks.RemoveAt(index);
                }

                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }
    }

    private abstract partial class TrackedObservable : INotifyPropChanged
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _handlers;

        public int Adds { get; private set; }

        public int Removes { get; private set; }

        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add
            {
                Adds++;
                _handlers += value;
            }

            remove
            {
                Removes++;
                _handlers -= value;
            }
        }

        public void Raise(string property) => _handlers?.Invoke(this, new PropChangedEventArgs(property));

        public Action Capture(string property)
        {
            var handlers = _handlers;
            return () => handlers?.Invoke(this, new PropChangedEventArgs(property));
        }
    }

    private sealed partial class TrackedDetails : TrackedObservable, IDetails2
    {
        public IIconInfo HeroImage => null!;

        public string Title => "Details";

        public string BodyValue { get; set; } = "initial";

        public Action? OnBodyRead { get; set; }

        public int BodyReads { get; private set; }

        public string Body
        {
            get
            {
                BodyReads++;
                var value = BodyValue;
                OnBodyRead?.Invoke();
                return value;
            }
        }

        public IDetailsElement[] Metadata { get; set; } = [];

        public IContent[] Content { get; set; } = [];

        public IContent[] GetContent() => Content;
    }

    private sealed partial class SnapshotDetails : IDetails2
    {
        public IIconInfo HeroImage => null!;

        public string Title => "Legacy details";

        public string Body { get; set; } = "initial";

        public IDetailsElement[] Metadata => [];

        public IContent[] Content { get; set; } = [];

        public IContent[] GetContent() => Content;
    }

    private sealed partial class TrackedMarkdown : TrackedObservable, IMarkdownContent
    {
        public string BodyValue { get; set; } = "initial";

        public Action? OnBodyRead { get; set; }

        public int BodyReads { get; private set; }

        public string Body
        {
            get
            {
                BodyReads++;
                OnBodyRead?.Invoke();
                return BodyValue;
            }
        }
    }

    private sealed partial class TrackedCommand : TrackedObservable, ICommand
    {
        public string Name { get; set; } = "Command";

        public string Id => "test.command";

        public IIconInfo Icon => null!;
    }

    private sealed partial class TrackedTree : TrackedObservable, ITreeContent
    {
        private TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChanged;

        public IContent? RootContent { get; set; }

        public IContent[] Children { get; set; } = [];

        public int ItemsSubscribers => _itemsChanged?.GetInvocationList().Length ?? 0;

        public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged
        {
            add => _itemsChanged += value;
            remove => _itemsChanged -= value;
        }

        public IContent[] GetChildren() => Children;

        public void RaiseItemsChanged() => _itemsChanged?.Invoke(this, new ItemsChangedEventArgs());

        public Action CaptureItemsChanged()
        {
            var handlers = _itemsChanged;
            return () => handlers?.Invoke(this, new ItemsChangedEventArgs());
        }
    }

    private sealed partial class TrackedListItem : ListItem
    {
        public int DetailsReads { get; private set; }

        public bool FailTextToSuggest { get; set; }

        public override string TextToSuggest
        {
            get => FailTextToSuggest ? throw new InvalidOperationException("broken suggestion") : base.TextToSuggest;
            set => base.TextToSuggest = value;
        }

        public override IDetails? Details
        {
            get
            {
                DetailsReads++;
                return base.Details;
            }

            set => base.Details = value;
        }
    }

    private sealed class DetailsRecipient
    {
        public DetailsViewModel? Details { get; set; }
    }

    private sealed partial class TestListPage : ListPage
    {
        public override IListItem[] GetItems() => [];
    }

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Details lifecycle tests";
    }
}
