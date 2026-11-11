// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class PageMenuViewModelTests
{
    private sealed class ImmediateTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks() => [];

        protected override void QueueTask(Task task) => TryExecuteTask(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestMenuPage : Page, IExtendedAttributesProvider
    {
        public IDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();

        public Action? BeforeReadProperties { get; set; }

        public new IDictionary<string, object> GetProperties()
        {
            BeforeReadProperties?.Invoke();
            return Attributes;
        }

        public void NotifyPropertiesChanged() => OnPropertyChanged("Properties");
    }

    private sealed partial class OptInMenuPage : Page, IExtendedAttributesProvider
    {
    }

    private sealed partial class TestMenuContentPage : ContentPage, IExtendedAttributesProvider
    {
        public IDictionary<string, object> Attributes { get; } = new Dictionary<string, object>();

        public new IDictionary<string, object> GetProperties() => Attributes;

        public override IContent[] GetContent() => [];
    }

    private sealed partial class TestMenuListPage(IContextItem command) : ListPage, IExtendedAttributesProvider
    {
        public bool ItemsRead { get; private set; }

        public new IDictionary<string, object> GetProperties() => new Dictionary<string, object>
        {
            [WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command },
        };

        public override IListItem[] GetItems()
        {
            ItemsRead = true;
            return [new ListItem(new NoOpCommand { Name = "Item" })];
        }
    }

    private sealed partial class BlockingMenuItem(ManualResetEventSlim entered, ManualResetEventSlim resume)
        : CommandContextItem(new NoOpCommand { Name = "Settings" })
    {
        public override IContextItem[] MoreCommands
        {
            get
            {
                entered.Set();
                Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(10)));
                return [];
            }

            set => base.MoreCommands = value;
        }
    }

    private static CommandContextItem Command(string name) => new(new NoOpCommand { Name = name });

    private static PageViewModel CreateViewModel(Page page) =>
        new(page, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty);

    private static void InitializePageAndMenu(PageViewModel viewModel)
    {
        viewModel.InitializeProperties();
        viewModel.PageMenu.InitializeProperties();
    }

    private static async Task InitializePageAndMenuAsync(PageViewModel viewModel)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void MenuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PageMenuViewModel.AllCommands))
            {
                loaded.TrySetResult();
            }
        }

        viewModel.PageMenu.PropertyChanged += MenuChanged;
        try
        {
            await viewModel.InitializeCommand.ExecuteAsync(null);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            viewModel.PageMenu.PropertyChanged -= MenuChanged;
        }
    }

    [TestMethod]
    public async Task Initialization_CompletesWhileOptionalMenuProviderIsBlocked()
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var page = new TestMenuPage
        {
            BeforeReadProperties = () =>
            {
                entered.Set();
                Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(10)));
            },
        };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Settings") };
        var viewModel = CreateViewModel(page);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PageMenu.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PageMenuViewModel.AllCommands))
            {
                loaded.TrySetResult();
            }
        };
        var initialization = Task.Run(viewModel.InitializeAsync);

        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(await initialization.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(viewModel.IsInitialized);
            Assert.IsFalse(loaded.Task.IsCompleted);

            resume.Set();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
        }
        finally
        {
            resume.Set();
            await initialization;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task ListInitialization_FetchesItemsWithoutWaitingForMenuCommands()
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var page = new TestMenuListPage(new BlockingMenuItem(entered, resume));
        var viewModel = new ListViewModel(page, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var initialization = Task.Run(viewModel.InitializeAsync);

        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(await initialization.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(viewModel.IsInitialized);
            Assert.IsTrue(page.ItemsRead);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
        }
        finally
        {
            resume.Set();
            await initialization;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Initialization_ReadsExplicitlyBoxedInspectableArrayFromPropertySet()
    {
        var first = Command("Settings");
        var nested = Command("More");
        nested.MoreCommands = [Command("Nested command")];
        var page = new TestMenuPage { Attributes = new PropertySet() };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = PropertyValue.CreateInspectableArray(new IContextItem[] { first, new Separator(), nested });
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(3, viewModel.PageMenu.AllCommands.Count);
            Assert.AreSame(first, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[0]).Model.Unsafe);
            Assert.IsInstanceOfType<SeparatorViewModel>(viewModel.PageMenu.AllCommands[1]);
            var submenu = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands[2];
            Assert.IsTrue(submenu.HasSubmenu);
            Assert.IsTrue(submenu.AllCommands.OfType<CommandContextItemViewModel>().Any(command => command.Name == "Nested command"));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Initialization_ReadsContextItemVectorThroughNativeInterface(bool empty, bool objectArray)
    {
        var first = Command("Settings");
        var nested = Command("More");
        nested.MoreCommands = [Command("Nested command")];
        IContextItem[] commands = empty ? [] : [first, new Separator(), nested];
        var payload = objectArray ? PropertyValue.CreateInspectableArray(commands) : commands;
        var marshaler = MarshalInspectable<object>.CreateMarshaler2(payload);
        var inspectable = IInspectable.FromAbi(marshaler.GetAbi());
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = inspectable;
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.AreEqual(!empty, viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(commands.Length, viewModel.PageMenu.AllCommands.Count);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
            if (!empty)
            {
                Assert.AreEqual("Settings", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[0]).Name);
                Assert.IsInstanceOfType<SeparatorViewModel>(viewModel.PageMenu.AllCommands[1]);
                var submenu = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands[2];
                Assert.IsTrue(submenu.HasSubmenu);
                Assert.IsTrue(submenu.AllCommands.OfType<CommandContextItemViewModel>().Any(command => command.Name == "Nested command"));
            }
        }
        finally
        {
            try
            {
                await viewModel.CleanupAsync();
            }
            finally
            {
                inspectable.ObjRef.Dispose();
                marshaler.Dispose();
            }
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Initialization_ReadsIterableOnlyPayloadThroughNativeInterface(bool empty, bool objectItems)
    {
        IContextItem[] commands = empty ? [] : [Command("Settings"), new Separator(), Command("Help")];
        object payload = objectItems ? new Queue<object>(commands) : new Queue<IContextItem>(commands);
        var marshaler = MarshalInspectable<object>.CreateMarshaler2(payload);
        var inspectable = IInspectable.FromAbi(marshaler.GetAbi());
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = inspectable;
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.AreEqual(!empty, viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(commands.Length, viewModel.PageMenu.AllCommands.Count);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
            if (!empty)
            {
                Assert.AreEqual("Settings", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[0]).Name);
                Assert.IsInstanceOfType<SeparatorViewModel>(viewModel.PageMenu.AllCommands[1]);
                Assert.AreEqual("Help", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands[2]).Name);
            }
        }
        finally
        {
            try
            {
                await viewModel.CleanupAsync();
            }
            finally
            {
                inspectable.ObjRef.Dispose();
                marshaler.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task Initialization_InvalidIterableEntryDoesNotPreventPageInitialization()
    {
        var payload = new Queue<object>([Command("Settings"), "invalid"]);
        var marshaler = MarshalInspectable<object>.CreateMarshaler2(payload);
        var inspectable = IInspectable.FromAbi(marshaler.GetAbi());
        var page = new TestMenuPage { Title = "Usable page" };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = inspectable;
        var viewModel = CreateViewModel(page);

        try
        {
            await InitializePageAndMenuAsync(viewModel);

            Assert.IsTrue(viewModel.IsInitialized);
            Assert.AreEqual("Usable page", viewModel.Title);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
            Assert.IsFalse(string.IsNullOrEmpty(viewModel.ErrorMessage));
        }
        finally
        {
            try
            {
                await viewModel.CleanupAsync();
            }
            finally
            {
                inspectable.ObjRef.Dispose();
                marshaler.Dispose();
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Initialization_InvalidNativeInspectableDoesNotPreventPageInitialization(bool propertyValue)
    {
        object payload = propertyValue ? PropertyValue.CreateString("invalid") : new Page();
        var marshaler = MarshalInspectable<object>.CreateMarshaler2(payload);
        var inspectable = IInspectable.FromAbi(marshaler.GetAbi());
        var page = new TestMenuPage { Title = "Usable page" };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = inspectable;
        var viewModel = CreateViewModel(page);

        try
        {
            await InitializePageAndMenuAsync(viewModel);

            Assert.IsTrue(viewModel.IsInitialized);
            Assert.AreEqual("Usable page", viewModel.Title);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
            Assert.IsFalse(string.IsNullOrEmpty(viewModel.ErrorMessage));
        }
        finally
        {
            try
            {
                await viewModel.CleanupAsync();
            }
            finally
            {
                inspectable.ObjRef.Dispose();
                marshaler.Dispose();
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Initialization_AcceptsTypedAndObjectArrays(bool objectArray)
    {
        var command = Command("Settings");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = objectArray
            ? new object[] { command }
            : new IContextItem[] { command };
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreSame(command, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Initialization_EnumeratesReadOnlyListWithoutReadingCountOrIndexer()
    {
        var command = Command("Settings");
        var values = new Mock<IReadOnlyList<IContextItem>>(MockBehavior.Strict);
        values.Setup(value => value.GetEnumerator()).Returns(() => ((IEnumerable<IContextItem>)new IContextItem[] { command }).GetEnumerator());
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = values.Object;
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreSame(command, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
            values.Verify(value => value.GetEnumerator(), Times.Once());
            values.VerifyNoOtherCalls();
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Initialization_WithoutExtendedAttributesKeepsNormalPage()
    {
        var viewModel = CreateViewModel(new Page { Title = "Normal page" });
        try
        {
            await InitializePageAndMenuAsync(viewModel);

            Assert.IsTrue(viewModel.IsInitialized);
            Assert.AreEqual("Normal page", viewModel.Title);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Initialization_EmptyOrSeparatorOnlyMenuIsUnavailable(bool separators)
    {
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = separators ? new IContextItem[] { new Separator() } : Array.Empty<IContextItem>();
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Initialization_InvalidPayloadDoesNotPreventPageInitialization(bool invalidEntry)
    {
        var page = new TestMenuPage { Title = "Usable page" };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = invalidEntry ? new object[] { Command("Settings"), "invalid" } : "invalid";
        var viewModel = CreateViewModel(page);

        try
        {
            await InitializePageAndMenuAsync(viewModel);

            Assert.IsTrue(viewModel.IsInitialized);
            Assert.AreEqual("Usable page", viewModel.Title);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
            Assert.IsFalse(string.IsNullOrEmpty(viewModel.ErrorMessage));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Initialization_ThrowingAttributeProviderDoesNotPreventPageInitialization()
    {
        var page = new TestMenuPage { BeforeReadProperties = () => throw new InvalidOperationException("Provider failed") };
        var viewModel = CreateViewModel(page);

        try
        {
            await InitializePageAndMenuAsync(viewModel);

            Assert.IsTrue(viewModel.IsInitialized);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            StringAssert.Contains(viewModel.ErrorMessage, "Provider failed");
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PropertiesChange_ReturnsWhileMenuProviderIsBlocked(bool cleanupWhileLoading)
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Original") };
        var viewModel = CreateViewModel(page);
        InitializePageAndMenu(viewModel);
        var original = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
        var replacement = Command("Replacement");
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { replacement };
        page.BeforeReadProperties = () =>
        {
            entered.Set();
            Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(10)));
        };
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void MenuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PageMenuViewModel.AllCommands) &&
                viewModel.PageMenu.AllCommands.OfType<CommandContextItemViewModel>().Any(command => ReferenceEquals(command.Model.Unsafe, replacement)))
            {
                loaded.TrySetResult();
            }
        }

        viewModel.PageMenu.PropertyChanged += MenuChanged;
        var callback = Task.Run(page.NotifyPropertiesChanged);
        try
        {
            await callback.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.AreSame(original, viewModel.PageMenu.AllCommands.Single());

            if (cleanupWhileLoading)
            {
                var cleanup = viewModel.CleanupAsync();
                Assert.IsFalse(cleanup.IsCompleted);
                resume.Set();
                await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
            }
            else
            {
                resume.Set();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreSame(replacement, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
            }

            Assert.IsTrue(original.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        }
        finally
        {
            resume.Set();
            await callback;
            viewModel.PageMenu.PropertyChanged -= MenuChanged;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Refresh_ReplacesAndRemovesMenuAndCleansDisplacedCommands()
    {
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Original") };
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);
            var menu = viewModel.PageMenu;
            var original = (CommandContextItemViewModel)menu.AllCommands.Single();

            page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Replacement") };
            viewModel.PageMenu.InitializeProperties();

            Assert.AreSame(menu, viewModel.PageMenu);
            Assert.IsTrue(original.Initialized.HasFlag(InitializedState.CleanedUp));
            var replacement = (CommandContextItemViewModel)menu.AllCommands.Single();
            Assert.AreEqual("Replacement", replacement.Name);

            page.Attributes.Remove(WellKnownExtensionAttributes.PageMenuCommands);
            viewModel.PageMenu.InitializeProperties();

            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, menu.AllCommands.Count);
            Assert.IsTrue(replacement.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Refresh_UnchangedCommandsKeepMenuViewModels(bool replaceArray)
    {
        var command = Command("Settings");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command };
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);
            var original = viewModel.PageMenu.AllCommands;
            if (replaceArray)
            {
                page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command };
            }

            page.Attributes["Unrelated"] = true;
            viewModel.PageMenu.InitializeProperties();

            Assert.AreSame(original, viewModel.PageMenu.AllCommands);
            Assert.IsFalse(((CommandContextItemViewModel)original[0]).Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Refresh_MutatedArrayReplacesMenuCommands()
    {
        IContextItem[] commands = [Command("Original")];
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = commands;
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);
            var original = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            commands[0] = Command("Replacement");
            viewModel.PageMenu.InitializeProperties();

            Assert.AreEqual("Replacement", ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Name);
            Assert.IsTrue(original.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task SupersededRead_DoesNotReportAnErrorOrResetTheCurrentMenu()
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var reads = 0;
        var page = new TestMenuPage
        {
            BeforeReadProperties = () =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                {
                    entered.Set();
                    Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(10)));
                    throw new InvalidOperationException("Stale provider failure");
                }
            },
        };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { new Separator() };
        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();
        var staleRead = Task.Run(viewModel.PageMenu.InitializeProperties);

        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            viewModel.PageMenu.InitializeProperties();
            var current = viewModel.PageMenu.AllCommands;
            Assert.HasCount(0, current);
            viewModel.PageMenu.ApplyPendingUpdates();
            var notifications = 0;
            viewModel.PageMenu.PropertyChanged += (_, _) => notifications++;

            resume.Set();
            await staleRead;
            viewModel.PageMenu.ApplyPendingUpdates();

            Assert.AreSame(current, viewModel.PageMenu.AllCommands);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
            Assert.AreEqual(0, notifications);
        }
        finally
        {
            resume.Set();
            await staleRead;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandNameChanges_KeepSnapshotAndOpenMenuWhenVisibilityIsUnchanged(bool rename)
    {
        var command = Command("Settings");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command };
        var viewModel = CreateViewModel(page);
        var menu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());

        try
        {
            InitializePageAndMenu(viewModel);
            viewModel.PageMenu.ApplyPendingUpdates();
            var snapshot = viewModel.PageMenu.AllCommands;
            var cached = (CommandContextItemViewModel)snapshot.Single();
            menu.PrepareForOpen(viewModel.PageMenu);
            List<string?> notifications = [];
            viewModel.PageMenu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            var menuNotifications = 0;
            menu.PropertyChanged += (_, _) => menuNotifications++;

            if (rename)
            {
                ((Command)command.Command!).Name = "Preferences";
            }
            else
            {
                cached.MarkPropertyDirty(nameof(CommandItemViewModel.Name));
            }

            cached.Command.ApplyPendingUpdates();
            cached.ApplyPendingUpdates();
            viewModel.PageMenu.ApplyPendingUpdates();

            Assert.AreEqual(rename ? "Preferences" : "Settings", cached.Name);
            Assert.AreSame(snapshot, viewModel.PageMenu.AllCommands);
            Assert.AreSame(cached, menu.FilteredItems.Single());
            Assert.HasCount(0, notifications);
            Assert.AreEqual(0, menuNotifications);
        }
        finally
        {
            menu.Close();
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task CommandVisibilityChanges_UpdateMenuAvailabilityNotification()
    {
        var command = Command("Settings");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command };
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);
            var cached = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            List<string?> notifications = [];
            viewModel.PageMenu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            ((Command)command.Command!).Name = string.Empty;
            cached.Command.ApplyPendingUpdates();
            cached.ApplyPendingUpdates();
            viewModel.PageMenu.ApplyPendingUpdates();

            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.HasCount(0, viewModel.PageMenu.AllCommands);
            Assert.IsFalse(cached.Initialized.HasFlag(InitializedState.CleanedUp));
            CollectionAssert.Contains(notifications, nameof(PageMenuViewModel.CanOpenContextMenu));

            ((Command)command.Command).Name = "Settings";
            cached.Command.ApplyPendingUpdates();
            cached.ApplyPendingUpdates();
            viewModel.PageMenu.ApplyPendingUpdates();

            Assert.IsTrue(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreSame(cached, viewModel.PageMenu.AllCommands.Single());
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task CommandRename_KeepsVisibleMenuWithoutNotification()
    {
        var command = Command("Settings");
        var page = new TestMenuPage();
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { command };
        var viewModel = CreateViewModel(page);

        try
        {
            InitializePageAndMenu(viewModel);
            var cached = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            cached.Command.ApplyPendingUpdates();
            cached.ApplyPendingUpdates();
            viewModel.PageMenu.ApplyPendingUpdates();
            var visible = viewModel.PageMenu.AllCommands;
            List<string?> notifications = [];
            viewModel.PageMenu.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            ((Command)command.Command!).Name = "Preferences";
            cached.Command.ApplyPendingUpdates();
            cached.ApplyPendingUpdates();
            viewModel.PageMenu.ApplyPendingUpdates();

            Assert.AreEqual("Preferences", cached.Name);
            Assert.AreSame(visible, viewModel.PageMenu.AllCommands);
            Assert.IsEmpty(notifications);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task ToolkitPageWithoutDeclaredProvider_DoesNotPublishMenuCommands()
    {
        var viewModel = CreateViewModel(new Page { MenuCommands = [Command("Settings")] });

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task MenuCommandsChange_RefreshesMenuForOptedInToolkitPage()
    {
        var page = new OptInMenuPage { MenuCommands = [Command("Original")] };
        var viewModel = CreateViewModel(page);
        var replacement = Command("Replacement");
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void MenuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PageMenuViewModel.AllCommands) &&
                viewModel.PageMenu.AllCommands.OfType<CommandContextItemViewModel>().Any(command => ReferenceEquals(command.Model.Unsafe, replacement)))
            {
                loaded.TrySetResult();
            }
        }

        try
        {
            InitializePageAndMenu(viewModel);
            var original = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            viewModel.PageMenu.PropertyChanged += MenuChanged;

            page.MenuCommands = [replacement];
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreSame(replacement, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
            Assert.IsTrue(original.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            viewModel.PageMenu.PropertyChanged -= MenuChanged;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Cleanup_RemovesMenuAndDetachesAttributeNotifications()
    {
        var page = new Mock<IPage>();
        TypedEventHandler<object, IPropChangedEventArgs>? subscription = null;
        page.SetupAdd(value => value.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(handler => subscription = handler);
        var attributes = page.As<IExtendedAttributesProvider>();
        attributes.Setup(value => value.GetProperties()).Returns(new Dictionary<string, object>
        {
            [WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Settings") },
        });
        var viewModel = new PageViewModel(page.Object, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty);

        try
        {
            InitializePageAndMenu(viewModel);
            var command = (CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single();
            Assert.IsNotNull(subscription);

            await viewModel.CleanupAsync();

            page.VerifyRemove(value => value.PropChanged -= subscription, Times.Once);
            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
            Assert.IsTrue(command.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task Cleanup_WaitsForInFlightMenuReadAndRemovesItsCommands()
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var page = new TestMenuPage
        {
            BeforeReadProperties = () =>
            {
                entered.Set();
                Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(10)));
            },
        };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { Command("Settings") };
        var viewModel = CreateViewModel(page);
        var initialization = Task.Run(viewModel.InitializeAsync);

        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            var cleanup = viewModel.CleanupAsync();
            Assert.IsFalse(cleanup.IsCompleted);

            resume.Set();
            await initialization;
            await cleanup;

            Assert.IsFalse(viewModel.PageMenu.CanOpenContextMenu);
            Assert.AreEqual(0, viewModel.PageMenu.AllCommands.Count);
        }
        finally
        {
            resume.Set();
            await initialization;
            await viewModel.CleanupAsync();
        }
    }

    [TestMethod]
    public async Task ContentPage_KeepsExistingCommandsIndependentFromPageMenu()
    {
        var contentCommand = Command("Save");
        var pageCommand = Command("Settings");
        var page = new TestMenuContentPage { Commands = [contentCommand] };
        page.Attributes[WellKnownExtensionAttributes.PageMenuCommands] = new IContextItem[] { pageCommand };
        var viewModel = new ContentPageViewModel(page, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty);

        try
        {
            InitializePageAndMenu(viewModel);

            Assert.AreEqual("Save", viewModel.PrimaryCommand?.Name);
            Assert.AreSame(contentCommand, ((CommandContextItemViewModel)viewModel.AllCommands.Single()).Model.Unsafe);
            Assert.AreSame(pageCommand, ((CommandContextItemViewModel)viewModel.PageMenu.AllCommands.Single()).Model.Unsafe);
        }
        finally
        {
            await viewModel.CleanupAsync();
        }
    }
}
