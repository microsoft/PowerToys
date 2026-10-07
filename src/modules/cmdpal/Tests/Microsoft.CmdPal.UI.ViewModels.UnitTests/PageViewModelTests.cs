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

        public void SetProviderContext(ICommandProviderContext value) => ProviderContext = value;
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
    public void CommandMessageConstructors_CapturePageContextAndPreserveHandlers()
    {
        var host = new TestAppExtensionHost();
        var providerContext = CommandProviderContext.Empty;
        var viewModel = new PageViewModel(new Page(), TaskScheduler.Default, host, providerContext);
        Action confirmation = () => { };
        Func<ICommandResult, bool> resultHandler = _ => true;

        var perform = new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand()), viewModel)
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };
        var handled = new HandleCommandResultMessage(new(Mock.Of<ICommandResult>()), viewModel)
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };

        Assert.IsNotNull(perform.Context);
        Assert.AreSame(viewModel, perform.Context.Page);
        Assert.AreSame(host, perform.Context.ExtensionHost);
        Assert.AreSame(providerContext, perform.Context.ProviderContext);
        Assert.AreSame(confirmation, perform.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, perform.ResultHandler);

        Assert.IsNotNull(handled.Context);
        Assert.AreSame(viewModel, handled.Context.Page);
        Assert.AreSame(host, handled.Context.ExtensionHost);
        Assert.AreSame(providerContext, handled.Context.ProviderContext);
        Assert.AreSame(confirmation, handled.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, handled.ResultHandler);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PerformCommandConstructors_PreserveInvocationPayload(bool withPage)
    {
        var page = new TestPageViewModel(new Page(), TaskScheduler.Default);
        var sourcePage = withPage ? page : null;
        var command = new NoOpCommand();
        var listItem = new ListItem(command);
        var commandItem = new CommandItem(command);
        var contextItem = new CommandContextItem(command);
        var confirmation = new ConfirmResultViewModel(Mock.Of<IConfirmationArgs>(), new(page));
        (PerformCommandMessage Message, object? Payload)[] cases =
        [
            (new(new ExtensionObject<ICommand>(command), sourcePage), null),
            (new(new ExtensionObject<ICommand>(command), new ExtensionObject<IListItem>(listItem), sourcePage), listItem),
            (new(new ExtensionObject<ICommand>(command), new ExtensionObject<ICommandItem>(commandItem), sourcePage), commandItem),
            (new(new ExtensionObject<ICommand>(command), new ExtensionObject<ICommandContextItem>(contextItem), sourcePage), contextItem),
            (new(confirmation, sourcePage), null),
        ];

        foreach (var (message, payload) in cases)
        {
            Assert.AreSame(payload, message.CommandContext);
            if (withPage)
            {
                Assert.IsNotNull(message.Context);
                Assert.AreSame(page, message.Context.Page);
                Assert.AreSame(page.ExtensionHost, message.Context.ExtensionHost);
                Assert.AreSame(page.ProviderContext, message.Context.ProviderContext);
            }
            else
            {
                Assert.IsNull(message.Context);
            }
        }

        var handled = new HandleCommandResultMessage(new(Mock.Of<ICommandResult>()), sourcePage);
        Assert.AreSame(sourcePage, handled.Context?.Page);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CommandItemConstructor_UsesAvailableSourcePage(bool withPage)
    {
        var page = new TestPageViewModel(new Page(), TaskScheduler.Default);
        IPageContext context = withPage
            ? page
            : Mock.Of<IPageContext>(value => value.Scheduler == TaskScheduler.Default);
        var item = new CommandItem(new NoOpCommand());
        var contextItem = new CommandContextItem(new NoOpCommand());
        CommandItemViewModel[] commands =
        [
            new(new(item), new(context), DefaultContextMenuFactory.Instance),
            new CommandContextItemViewModel(contextItem, new(context)),
        ];

        try
        {
            foreach (var command in commands)
            {
                command.InitializeProperties();
                var message = new PerformCommandMessage(command);

                Assert.AreSame(command.Command.Model, message.Command);
                Assert.AreSame(command.Model.Unsafe, message.CommandContext);
                if (withPage)
                {
                    Assert.IsNotNull(message.Context);
                    Assert.AreSame(page, message.Context.Page);
                    Assert.AreSame(page.ExtensionHost, message.Context.ExtensionHost);
                    Assert.AreSame(page.ProviderContext, message.Context.ProviderContext);
                }
                else
                {
                    Assert.IsNull(message.Context);
                }
            }
        }
        finally
        {
            foreach (var command in commands)
            {
                command.SafeCleanup();
            }

            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public void SourceContext_CapturesProviderAtConstruction()
    {
        var page = new TestPageViewModel(new Page(), TaskScheduler.Default);
        var originalProvider = page.ProviderContext;
        var perform = new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand()), page);
        var handled = new HandleCommandResultMessage(new(Mock.Of<ICommandResult>()), page);

        page.SetProviderContext(Mock.Of<ICommandProviderContext>());

        Assert.IsNotNull(perform.Context);
        Assert.IsNotNull(handled.Context);
        Assert.AreSame(originalProvider, perform.Context.ProviderContext);
        Assert.AreSame(originalProvider, handled.Context.ProviderContext);
        Assert.AreNotSame(page.ProviderContext, perform.Context.ProviderContext);
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
            Assert.IsNotNull(message.Context);
            Assert.AreSame(page, message.Context.Page);
            Assert.AreSame(host, message.Context.ExtensionHost);
            Assert.AreSame(page.ProviderContext, message.Context.ProviderContext);
            message.OnBeforeShowConfirmation = confirmation;
            message.ResultHandler = resultHandler;
        };
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, message) => dispatched = message);

        try
        {
            Assert.AreEqual(ContextKeybindingResult.Hide, menu.InvokeCommand(command, navigateSubmenus: false));
            Assert.IsNotNull(dispatched);
            Assert.IsNotNull(dispatched.Context);
            Assert.AreSame(page, dispatched.Context.Page);
            Assert.AreSame(item, dispatched.CommandContext);
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
