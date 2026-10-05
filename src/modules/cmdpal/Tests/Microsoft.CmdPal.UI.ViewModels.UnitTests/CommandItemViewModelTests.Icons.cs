// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class CommandItemViewModelTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IconSnapshotIsPublishedOnlyAfterItsPropertiesAreInitialized(bool readDark)
    {
        var context = new TestPageContext();
        var item = new ListItem(new NoOpCommand()) { Icon = new IconInfo("first") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var first = viewModel.Icon;
            IconInfoViewModel? observedDuringRead = null;
            var hadBothThemesDuringRead = false;

            item.Icon = new ObservedIconInfo(
                () =>
                {
                    observedDuringRead = viewModel.Icon;
                    hadBothThemesDuringRead = observedDuringRead.HasIcon(light: true) && observedDuringRead.HasIcon(light: false);
                },
                readDark);

            Assert.AreSame(first, observedDuringRead);
            Assert.IsTrue(hadBothThemesDuringRead);
            Assert.AreNotSame(first, viewModel.Icon);
            Assert.AreEqual("second", viewModel.Icon.Light.Icon);
            Assert.AreEqual("second", viewModel.Icon.Dark.Icon);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedIconReadKeepsPreviousInitializedSnapshot(bool readDark)
    {
        var context = new Mock<IPageContext>();
        context.SetupGet(value => value.Scheduler).Returns(TaskScheduler.Default);
        context.SetupGet(value => value.ProviderContext).Returns(CommandProviderContext.Empty);
        var item = new ControlledIconItem();
        var viewModel = new CommandItemViewModel(new(item), new(context.Object), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var previousIcon = viewModel.Icon;
            var failure = new InvalidOperationException("Extension icon data read failed.");
            item.Icon = new ObservedIconInfo(() => throw failure, readDark);

            item.NotifyIconChanged();

            Assert.AreSame(previousIcon, viewModel.Icon);
            Assert.IsTrue(viewModel.Icon.HasIcon(light: true));
            Assert.IsTrue(viewModel.Icon.HasIcon(light: false));
            context.Verify(value => value.ShowException(failure, "Cached item"), Times.Once);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public void IconUpdatesKeepPresentationOwnerAndReplaceSnapshot()
    {
        var context = new TestPageContext();
        var item = new ListItem(new NoOpCommand()) { Icon = new IconInfo("first") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var first = viewModel.Icon;

            item.Icon = new IconInfo("second");

            Assert.IsNotNull(first.PresentationOwner);
            Assert.AreNotSame(first, viewModel.Icon);
            Assert.AreSame(first.PresentationOwner, viewModel.Icon.PresentationOwner);
            Assert.AreEqual("first", first.Light.Icon);
            Assert.AreEqual("second", viewModel.Icon.Light.Icon);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    public void PrimaryMenuIconUpdatesKeepPresentationOwner()
    {
        var context = new TestPageContext();
        var command = new NoOpCommand { Name = "Primary", Icon = null! };
        var item = new ListItem(command);
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var primary = viewModel.PrimaryMenuItem;
            Assert.IsNotNull(primary);

            item.Icon = new IconInfo("first");
            var first = primary.Icon;
            item.Icon = new IconInfo("second");

            Assert.IsNotNull(first.PresentationOwner);
            Assert.AreSame(first.PresentationOwner, primary.Icon.PresentationOwner);
            Assert.AreNotSame(viewModel.Icon.PresentationOwner, primary.Icon.PresentationOwner);
            Assert.AreNotSame(first, primary.Icon);
            Assert.AreEqual("first", first.Light.Icon);
            Assert.AreEqual("second", primary.Icon.Light.Icon);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    public void DifferentItemsDoNotSharePresentationOwnerForSharedIcon()
    {
        var context = new TestPageContext();
        var icon = new IconInfo("shared");
        var firstItem = new ListItem(new NoOpCommand()) { Icon = icon };
        var secondItem = new ListItem(new NoOpCommand()) { Icon = icon };
        var first = new CommandItemViewModel(new(firstItem), new(context), DefaultContextMenuFactory.Instance);
        var second = new CommandItemViewModel(new(secondItem), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            first.InitializeProperties();
            second.InitializeProperties();

            Assert.IsNotNull(first.Icon.PresentationOwner);
            Assert.IsNotNull(second.Icon.PresentationOwner);
            Assert.AreNotSame(first.Icon.PresentationOwner, second.Icon.PresentationOwner);
        }
        finally
        {
            first.SafeCleanup();
            second.SafeCleanup();
        }
    }

    [TestMethod]
    public void RemovingItemIconSwitchesToCommandIconWithoutItemOwnership()
    {
        var context = new TestPageContext();
        var command = new NoOpCommand { Icon = new IconInfo("command") };
        var item = new ListItem(command) { Icon = new IconInfo("item") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            Assert.IsNotNull(viewModel.Icon.PresentationOwner);

            item.Icon = null;

            Assert.AreSame(viewModel.Command.Icon, viewModel.Icon);
            Assert.IsNull(viewModel.Icon.PresentationOwner);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CleanupReenteredFromIconGetterDoesNotPublishSnapshot(bool propertyChange)
    {
        var context = new TestPageContext();
        var item = new ListItem(new NoOpCommand()) { Icon = new IconInfo("first") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            if (propertyChange)
            {
                viewModel.InitializeProperties();
            }

            item.Icon = new ObservedIconInfo(viewModel.SafeCleanup);
            if (!propertyChange)
            {
                viewModel.InitializeProperties();
            }

            Assert.IsTrue(viewModel.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.IsFalse(viewModel.Icon.IsSet);
            Assert.IsNull(viewModel.Icon.PresentationOwner);

            item.Icon = new IconInfo("third");
            viewModel.InitializeProperties();
            Assert.IsFalse(viewModel.Icon.IsSet);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CleanupDuringBlockedIconGetterDoesNotWaitOrPublishSnapshot(bool propertyChange)
    {
        var timeout = TimeSpan.FromSeconds(5);
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new TestPageContext();
        var item = new ListItem(new NoOpCommand()) { Icon = new IconInfo("first") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        if (propertyChange)
        {
            viewModel.InitializeProperties();
        }

        var operation = Task.Run(() =>
        {
            item.Icon = new ObservedIconInfo(() =>
            {
                entered.TrySetResult();
                Assert.IsTrue(resume.Wait(timeout * 2), "The icon getter was not released.");
            });
            if (!propertyChange)
            {
                viewModel.InitializeProperties();
            }
        });
        var cleanup = Task.CompletedTask;
        var testSucceeded = false;
        try
        {
            await entered.Task.WaitAsync(timeout);
            cleanup = Task.Run(viewModel.SafeCleanup);
            await cleanup.WaitAsync(timeout);
            Assert.IsTrue(viewModel.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.IsFalse(viewModel.Icon.IsSet);

            resume.Set();
            await operation.WaitAsync(timeout);
            Assert.IsFalse(viewModel.Icon.IsSet);
            Assert.IsNull(viewModel.Icon.PresentationOwner);
            testSucceeded = true;
        }
        finally
        {
            resume.Set();
            try
            {
                await Task.WhenAll(operation, cleanup).WaitAsync(timeout);
            }
            catch (Exception exception) when (!testSucceeded)
            {
                Console.Error.WriteLine($"Secondary failure while waiting for background tasks: {exception}");
            }
            finally
            {
                viewModel.SafeCleanup();
                GC.KeepAlive(context);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedInitializationPublishesErrorIcon(bool fastInit)
    {
        var context = new TestPageContext();
        var item = CreateFailingInitializationItem(fastInit);
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            Assert.IsFalse(fastInit ? viewModel.SafeFastInit() : viewModel.SafeInitializeProperties());
            Assert.IsTrue(viewModel.IsInErrorState);
            Assert.IsTrue(viewModel.Icon.IsSet);
            Assert.AreEqual("\uEA39", viewModel.Icon.Light.Icon);
            Assert.AreEqual("\uEA39", viewModel.Icon.Dark.Icon);

            viewModel.SafeCleanup();
            Assert.IsFalse(viewModel.Icon.IsSet);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CleanupReenteredFromFailingInitializationDoesNotPublishErrorIcon(bool fastInit)
    {
        var context = new TestPageContext();
        CommandItemViewModel? viewModel = null;
        var item = CreateFailingInitializationItem(fastInit, () => viewModel!.SafeCleanup());
        viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            Assert.IsFalse(fastInit ? viewModel.SafeFastInit() : viewModel.SafeInitializeProperties());
            Assert.IsTrue(viewModel.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.IsFalse(viewModel.Icon.IsSet);
            Assert.IsNull(viewModel.Icon.PresentationOwner);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CleanupDuringBlockedFailingInitializationDoesNotWaitOrPublishErrorIcon(bool fastInit)
    {
        var timeout = TimeSpan.FromSeconds(5);
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new TestPageContext();
        var item = CreateFailingInitializationItem(fastInit, () =>
        {
            entered.TrySetResult();
            Assert.IsTrue(resume.Wait(timeout * 2), "The failing getter was not released.");
        });
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        var operation = Task.Run(() => fastInit ? viewModel.SafeFastInit() : viewModel.SafeInitializeProperties());
        var cleanup = Task.CompletedTask;
        var testSucceeded = false;
        try
        {
            await entered.Task.WaitAsync(timeout);
            cleanup = Task.Run(viewModel.SafeCleanup);
            await cleanup.WaitAsync(timeout);
            Assert.IsTrue(viewModel.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.IsFalse(viewModel.Icon.IsSet);

            resume.Set();
            Assert.IsFalse(await operation.WaitAsync(timeout));
            Assert.IsFalse(viewModel.Icon.IsSet);
            Assert.IsNull(viewModel.Icon.PresentationOwner);
            testSucceeded = true;
        }
        finally
        {
            resume.Set();
            try
            {
                await Task.WhenAll(operation, cleanup).WaitAsync(timeout);
            }
            catch (Exception exception) when (!testSucceeded)
            {
                Console.Error.WriteLine($"Secondary failure while waiting for background tasks: {exception}");
            }
            finally
            {
                viewModel.SafeCleanup();
                GC.KeepAlive(context);
            }
        }
    }

    [TestMethod]
    public void ItemIconUpdateDoesNotRepopulateCleanedPrimaryMenuItem()
    {
        var context = new TestPageContext();
        var item = new ListItem(new NoOpCommand { Name = "Primary" }) { Icon = new IconInfo("first") };
        var viewModel = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();
            var primary = viewModel.PrimaryMenuItem;
            Assert.IsNotNull(primary);

            item.Icon = new ObservedIconInfo(primary.SafeCleanup);

            Assert.AreEqual("second", viewModel.Icon.Light.Icon);
            Assert.IsTrue(primary.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreSame(viewModel.Command.Icon, primary.Icon);
            Assert.IsNull(primary.Icon.PresentationOwner);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    private static ListItem CreateFailingInitializationItem(bool fastInit, Action? beforeFailure = null)
    {
        void Fail()
        {
            beforeFailure?.Invoke();
            throw new InvalidOperationException("Extension property read failed.");
        }

        return fastInit
            ? new ObservedSubtitleItem(Fail)
            : new ListItem(new NoOpCommand()) { Icon = new ObservedIconInfo(Fail) };
    }

    private sealed partial class ControlledIconItem : ICommandItem
    {
        public ICommand? Command { get; } = new NoOpCommand { Icon = new IconInfo("command") };

        public IIconInfo? Icon { get; set; } = new IconInfo("first");

        public string Title => "Cached item";

        public string Subtitle => string.Empty;

        public IContextItem[] MoreCommands => [];

        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

        public void NotifyIconChanged()
        {
            PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Icon)));
        }
    }

    private sealed partial class ObservedSubtitleItem : ListItem
    {
        private readonly Action _onRead;

        public ObservedSubtitleItem(Action onRead)
            : base(new NoOpCommand())
        {
            _onRead = onRead;
        }

        public override string Subtitle
        {
            get
            {
                _onRead();
                return base.Subtitle;
            }

            set => base.Subtitle = value;
        }
    }

    private sealed partial class ObservedIconInfo : IconInfo
    {
        private readonly Action _onRead;
        private readonly bool _readDark;

        public ObservedIconInfo(Action onRead, bool readDark = false)
            : base("second")
        {
            _onRead = onRead;
            _readDark = readDark;
        }

        public override IconData Light
        {
            get
            {
                if (!_readDark)
                {
                    _onRead();
                }

                return base.Light;
            }

            set => base.Light = value;
        }

        public override IconData Dark
        {
            get
            {
                if (_readDark)
                {
                    _onRead();
                }

                return base.Dark;
            }

            set => base.Dark = value;
        }
    }
}
