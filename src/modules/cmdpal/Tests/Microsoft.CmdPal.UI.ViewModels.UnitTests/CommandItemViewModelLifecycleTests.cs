// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class CommandItemViewModelLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private partial class TestObservable : INotifyPropChanged
    {
        private event TypedEventHandler<object, IPropChangedEventArgs>? PropChangedHandlers;

        private volatile bool _addingSubscription;

        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
        {
            add
            {
                _addingSubscription = true;
                try
                {
                    BeforeSubscribe?.Invoke();
                    PropChangedHandlers += value;
                    AfterSubscribe?.Invoke();
                }
                finally
                {
                    _addingSubscription = false;
                }
            }

            remove
            {
                RemovedDuringSubscribe |= _addingSubscription;
                PropChangedHandlers -= value;
                AfterUnsubscribe?.Invoke();
            }
        }

        public bool RemovedDuringSubscribe { get; private set; }

        public Action? BeforeSubscribe { get; set; }

        public Action? AfterSubscribe { get; set; }

        public Action? AfterUnsubscribe { get; set; }

        public int SubscriberCount => PropChangedHandlers?.GetInvocationList().Length ?? 0;

        public int CountSubscribers<T>() => PropChangedHandlers?.GetInvocationList().Count(handler => handler.Target is T) ?? 0;

        public void RaisePropertyChanged(string name) => PropChangedHandlers?.Invoke(this, new PropChangedEventArgs(name));

        public Action CapturePropertyChanged(string name)
        {
            var handlers = PropChangedHandlers;
            return () => handlers?.Invoke(this, new PropChangedEventArgs(name));
        }
    }

    private partial class TestCommandItem : TestObservable, ICommandItem
    {
        public Action? ReadIcon { get; set; }

        public Action? ReadCommand { get; set; }

        public ICommand? CommandValue { get; set; }

        public ICommand? Command
        {
            get
            {
                ReadCommand?.Invoke();
                return CommandValue;
            }
        }

        public IContextItem[] MoreCommands => [];

        public IIconInfo? Icon
        {
            get
            {
                ReadIcon?.Invoke();
                return null;
            }
        }

        public string Title { get; set; } = "Before";

        public string Subtitle => string.Empty;

        public void RaiseTitleChanged() => RaisePropertyChanged(nameof(Title));

        public Action CaptureTitleChanged() => CapturePropertyChanged(nameof(Title));
    }

    private sealed partial class CachedCommandAdapter(CommandViewModel command, TestCommandItem source) : CommandItemViewModelAdapter, ICommandItem
    {
        public int CachedCommandReads { get; private set; }

        public bool FailTitleRead { get; set; }

        public override CommandViewModel CommandViewModel
        {
            get
            {
                CachedCommandReads++;
                return command;
            }
        }

        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
        {
            add => source.PropChanged += value;
            remove => source.PropChanged -= value;
        }

        public ICommand? Command => source.Command;

        public IContextItem[] MoreCommands => source.MoreCommands;

        public IIconInfo? Icon => source.Icon;

        public string Title => FailTitleRead ? throw new InvalidOperationException("Title read failed.") : source.Title;

        public string Subtitle => source.Subtitle;
    }

    [TestMethod]
    public void CachedCommandAdapter_BorrowsWithoutOwningCommandLifetime()
    {
        var context = new TestPageContext();
        var command = new TestCommand();
        var cachedCommand = new CommandViewModel(command, new(context));
        cachedCommand.InitializeProperties();
        var source = new TestCommandItem { CommandValue = command };
        var adapter = new CachedCommandAdapter(cachedCommand, source);
        var viewModel = new CommandItemViewModel(new(adapter), new(context), null);
        try
        {
            viewModel.InitializeProperties();
            Assert.AreSame(cachedCommand, viewModel.Command);

            source.RaisePropertyChanged(nameof(ICommandItem.Command));
            Assert.AreSame(cachedCommand, viewModel.Command);
            Assert.AreEqual(2, adapter.CachedCommandReads);

            viewModel.SafeCleanup();
            Assert.AreEqual(1, command.SubscriberCount);
            AssertNoSubscriptions(source, viewModel);
        }
        finally
        {
            viewModel.SafeCleanup();
            cachedCommand.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedCachedCommandInitialization_ClearsCommandWithoutReadingAdapterAgain(bool fastInit)
    {
        var context = new TestPageContext();
        var command = new TestCommand { Name = "Run" };
        var cachedCommand = new CommandViewModel(command, new(context));
        cachedCommand.InitializeProperties();
        var source = new TestCommandItem { CommandValue = command };
        var adapter = new CachedCommandAdapter(cachedCommand, source) { FailTitleRead = true };
        var viewModel = new CommandItemViewModel(new(adapter), new(context), null);
        try
        {
            Assert.IsFalse(fastInit ? viewModel.SafeFastInit() : viewModel.SafeInitializeProperties());
            Assert.IsTrue(viewModel.IsInErrorState);
            Assert.IsFalse(viewModel.Command.IsSet);
            Assert.AreNotSame(cachedCommand, viewModel.Command);
            Assert.AreEqual(1, adapter.CachedCommandReads);
            Assert.AreEqual(1, command.SubscriberCount);
        }
        finally
        {
            viewModel.SafeCleanup();
            cachedCommand.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public Task CleanupDuringIconGetter_DoesNotLeaveSubscriptions() =>
        VerifyCleanupDuringInitialization((item, block) => item.ReadIcon = block);

    [TestMethod]
    public Task CleanupBeforeEventAddCompletes_DoesNotLeaveSubscriptions() =>
        VerifyCleanupDuringInitialization((item, block) => item.BeforeSubscribe = block);

    [TestMethod]
    public Task CleanupAfterEventAdd_DoesNotLeaveSubscriptions() =>
        VerifyCleanupDuringInitialization((item, block) => item.AfterSubscribe = block);

    [TestMethod]
    public void CleanupReenteredFromEventAdd_DoesNotLeaveSubscriptions()
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        item.BeforeSubscribe = viewModel.SafeCleanup;

        try
        {
            viewModel.InitializeProperties();

            AssertNoSubscriptions(item, viewModel);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void FailedSubscription_RetainsCleanupObligation(bool throwAfterAdd, bool cleanupDuringAdd)
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        var addFailure = new InvalidOperationException("Event add failed.");
        Action failAdd = () =>
        {
            if (cleanupDuringAdd)
            {
                viewModel.SafeCleanup();
            }

            throw addFailure;
        };
        if (throwAfterAdd)
        {
            item.AfterSubscribe = failAdd;
        }
        else
        {
            item.BeforeSubscribe = failAdd;
        }

        try
        {
            Assert.AreSame(addFailure, Assert.ThrowsExactly<InvalidOperationException>(viewModel.InitializeProperties));
            if (!cleanupDuringAdd)
            {
                viewModel.SafeCleanup();
            }

            AssertNoSubscriptions(item, viewModel);
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
    public void FailedSubscription_PreservesAddExceptionWhenRemovalThrows(bool throwAfterAdd)
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        var addFailure = new InvalidOperationException("Event add failed.");
        item.AfterUnsubscribe = () => throw new InvalidOperationException("Event remove failed.");
        Action failAdd = () =>
        {
            viewModel.SafeCleanup();
            throw addFailure;
        };
        if (throwAfterAdd)
        {
            item.AfterSubscribe = failAdd;
        }
        else
        {
            item.BeforeSubscribe = failAdd;
        }

        try
        {
            Assert.AreSame(addFailure, Assert.ThrowsExactly<InvalidOperationException>(viewModel.InitializeProperties));
            AssertNoSubscriptions(item, viewModel);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public void SuccessfulSubscription_PropagatesRemovalFailureWhenCleanupWins()
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        var removeFailure = new InvalidOperationException("Event remove failed.");
        item.BeforeSubscribe = viewModel.SafeCleanup;
        item.AfterUnsubscribe = () => throw removeFailure;

        try
        {
            Assert.AreSame(removeFailure, Assert.ThrowsExactly<InvalidOperationException>(viewModel.InitializeProperties));
            AssertNoSubscriptions(item, viewModel);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public void InitializeAfterCleanup_DoesNotReadExtensionOrSubscribe()
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        item.ReadIcon = () => Assert.Fail("A cleaned item must not restart initialization.");
        item.ReadCommand = item.ReadIcon;

        viewModel.SafeCleanup();
        viewModel.FastInitializeProperties();
        viewModel.InitializeProperties();
        viewModel.SlowInitializeProperties();

        AssertNoSubscriptions(item, viewModel);
        GC.KeepAlive(context);
    }

    [TestMethod]
    public void Initialization_SubscribesOnceAndCleanupStopsUpdates()
    {
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);

        try
        {
            viewModel.InitializeProperties();
            viewModel.InitializeProperties();
            Assert.AreEqual(1, item.SubscriberCount);

            item.Title = "Updated";
            item.RaiseTitleChanged();
            Assert.AreEqual("Updated", viewModel.Title);

            var queuedNotification = item.CaptureTitleChanged();
            viewModel.SafeCleanup();
            AssertNoSubscriptions(item, viewModel);
            item.Title = "After cleanup";
            item.RaiseTitleChanged();
            queuedNotification();
            Assert.AreEqual("Updated", viewModel.Title);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public async Task ConcurrentInitialization_SubscribesOnce()
    {
        using var entered = new CountdownEvent(2);
        using var resume = new ManualResetEventSlim();
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        viewModel.FastInitializeProperties();
        item.ReadIcon = () =>
        {
            entered.Signal();
            Assert.IsTrue(resume.Wait(TestTimeout * 2), "Initialization was not released.");
        };

        var initialization = Task.WhenAll(Task.Run(viewModel.InitializeProperties), Task.Run(viewModel.InitializeProperties));
        try
        {
            Assert.IsTrue(entered.Wait(TestTimeout), "Both initializers did not reach the blocking getter.");
            resume.Set();
            await initialization.WaitAsync(TestTimeout);

            Assert.AreEqual(1, item.SubscriberCount);
            viewModel.SafeCleanup();
            AssertNoSubscriptions(item, viewModel);
        }
        finally
        {
            resume.Set();
            await initialization.WaitAsync(TestTimeout);
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    private static async Task VerifyCleanupDuringInitialization(Action<TestCommandItem, Action> configureBlock)
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var context = new TestPageContext();
        var item = new TestCommandItem();
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        configureBlock(item, () =>
        {
            entered.Set();
            Assert.IsTrue(resume.Wait(TestTimeout * 2), "Initialization was not released.");
        });

        var initialization = Task.Run(viewModel.InitializeProperties);
        try
        {
            Assert.IsTrue(entered.Wait(TestTimeout), "Initialization did not reach the blocking call.");
            await Task.Run(viewModel.SafeCleanup).WaitAsync(TestTimeout);
            resume.Set();
            await initialization.WaitAsync(TestTimeout);

            AssertNoSubscriptions(item, viewModel);
            item.Title = "After cleanup";
            item.RaiseTitleChanged();
            Assert.AreEqual("Before", viewModel.Title);
            Assert.IsTrue(viewModel.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            resume.Set();
            await initialization.WaitAsync(TestTimeout);
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    private static void AssertNoSubscriptions(TestCommandItem item, CommandItemViewModel viewModel)
    {
        Assert.IsFalse(item.RemovedDuringSubscribe, "Cleanup overlapped the extension's event add and remove calls.");
        Assert.AreEqual(0, item.SubscriberCount, "The extension still retains the cleaned item.");
        var commandEvent = typeof(ObservableObject)
            .GetField(nameof(ObservableObject.PropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(commandEvent, "ObservableObject event storage changed; update the subscription assertion.");
        var commandHandlers = (Delegate?)commandEvent.GetValue(viewModel.Command);
        Assert.IsFalse(
            commandHandlers?.GetInvocationList().Any(handler => ReferenceEquals(handler.Target, viewModel)) == true,
            "The command still retains the cleaned item.");
    }
}
