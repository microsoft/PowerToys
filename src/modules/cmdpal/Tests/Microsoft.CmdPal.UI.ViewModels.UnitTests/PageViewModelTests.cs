// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class PageViewModelTests
{
    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        protected override IEnumerable<Task>? GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
            }
        }
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestPageViewModel : PageViewModel
    {
        public TestPageViewModel(Page page, TaskScheduler scheduler)
            : base(page, scheduler, new TestAppExtensionHost(), CommandProviderContext.Empty)
        {
        }

        public void SetInitialSearchText(string value) => SetInitialSearchTextBox(value);
    }

    [TestMethod]
    public void InitialSearchTextBoxUpdate_IsMarshaledToUiScheduler()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Page
        {
            Id = "page",
            Name = "Page",
        };
        var viewModel = new TestPageViewModel(page, scheduler);
        var searchTextChanged = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PageViewModel.SearchTextBox))
            {
                searchTextChanged = true;
            }
        };

        viewModel.SetInitialSearchText("ssh");

        Assert.AreEqual("ssh", viewModel.SearchTextBox);
        Assert.IsFalse(searchTextChanged, "The UI-facing notification must not be raised on the initialization thread.");

        viewModel.ApplyPendingUpdates();
        Assert.IsFalse(searchTextChanged, "Publishing the pending update must only enqueue work on the UI scheduler.");

        scheduler.RunAll();
        Assert.IsTrue(searchTextChanged);
    }

    [TestMethod]
    public void IconUpdate_InitializesReplacementIcon()
    {
        var page = new Page
        {
            Id = "page",
            Name = "Page",
            Icon = new IconInfo("initial"),
        };
        var viewModel = new PageViewModel(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        var initialIcon = viewModel.Icon;

        page.Icon = new IconInfo(new IconData("light"), new IconData("dark"));

        Assert.AreNotSame(initialIcon, viewModel.Icon);
        Assert.AreEqual("light", viewModel.Icon.Light.Icon);
        Assert.AreEqual("dark", viewModel.Icon.Dark.Icon);
    }

    [TestMethod]
    public void PrepareCommandMessages_AddsPageContextAndPreservesHandlers()
    {
        var host = new TestAppExtensionHost();
        var providerContext = CommandProviderContext.Empty;
        var viewModel = new PageViewModel(new Page(), TaskScheduler.Default, host, providerContext);
        Action confirmation = () => { };
        Func<ICommandResult, bool> resultHandler = _ => true;

        var perform = new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand()))
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };
        var handled = new HandleCommandResultMessage(new(Mock.Of<ICommandResult>()))
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };

        Assert.AreSame(perform, viewModel.PreparePerformCommandMessage(perform));
        Assert.AreSame(viewModel, perform.SourcePage);
        Assert.AreSame(host, perform.SourceExtensionHost);
        Assert.AreSame(providerContext, perform.SourceProviderContext);
        Assert.AreSame(confirmation, perform.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, perform.ResultHandler);

        Assert.AreSame(handled, viewModel.PrepareHandleCommandResultMessage(handled));
        Assert.AreSame(viewModel, handled.SourcePage);
        Assert.AreSame(host, handled.SourceExtensionHost);
        Assert.AreSame(providerContext, handled.SourceProviderContext);
        Assert.AreSame(confirmation, handled.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, handled.ResultHandler);
    }

    [TestMethod]
    public void ContextMenuInvocation_PreservesPageContextAndMessageHandlers()
    {
        var host = new TestAppExtensionHost();
        var page = new PageViewModel(new Page(), TaskScheduler.Default, host, CommandProviderContext.Empty);
        var item = new CommandItem(new NoOpCommand { Name = "Command" });
        var command = new CommandItemViewModel(new(item), new(page), DefaultContextMenuFactory.Instance);
        command.InitializeProperties();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        var recipient = new object();
        PerformCommandMessage? dispatched = null;
        Action confirmation = () => { };
        Func<ICommandResult, bool> resultHandler = _ => true;
        menu.CommandInvoking += (_, message) =>
        {
            Assert.AreSame(page, message.SourcePage);
            Assert.AreSame(host, message.SourceExtensionHost);
            Assert.AreSame(page.ProviderContext, message.SourceProviderContext);
            message.OnBeforeShowConfirmation = confirmation;
            message.ResultHandler = resultHandler;
        };
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, message) => dispatched = message);

        try
        {
            Assert.AreEqual(ContextKeybindingResult.Hide, menu.InvokeCommand(command, navigateSubmenus: false));
            Assert.IsNotNull(dispatched);
            Assert.AreSame(page, dispatched.SourcePage);
            Assert.AreSame(item, dispatched.Context);
            Assert.AreSame(confirmation, dispatched.OnBeforeShowConfirmation);
            Assert.AreSame(resultHandler, dispatched.ResultHandler);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            command.SafeCleanup();
            GC.KeepAlive(page);
        }
    }
}
