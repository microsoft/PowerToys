// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public partial class ShellViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestCommand(CommandResult result, Action? onInvoke = null) : InvokableCommand
    {
        public override ICommandResult Invoke()
        {
            onInvoke?.Invoke();
            return result;
        }
    }

    private sealed partial class TestPage : ListPage
    {
        public override IListItem[] GetItems() => [];
    }

    private sealed partial class TestPageViewModel : PageViewModel
    {
        public TestPageViewModel(AppExtensionHost host)
            : base(null, TaskScheduler.Default, host, CommandProviderContext.Empty)
        {
            IsInitialized = true;
        }
    }

    [TestMethod]
    [DataRow(CommandResultKind.Dismiss, false, false)]
    [DataRow(CommandResultKind.Hide, false, false)]
    [DataRow(CommandResultKind.KeepOpen, false, false)]
    [DataRow(CommandResultKind.KeepOpen, true, false)]
    [DataRow(CommandResultKind.KeepOpen, false, true)]
    [DataRow(CommandResultKind.KeepOpen, true, true)]
    public async Task PerformCommand_CountsInvocationBeforeSessionEnds(CommandResultKind resultKind, bool hideDuringInvoke, bool throwDuringInvoke)
    {
        var viewModel = CreateViewModel();
        var recipient = new object();
        var events = new List<string>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryExtensionInvokedMessage? invocation = null;
        WeakReferenceMessenger.Default.Register<TelemetryCommandStartedMessage>(recipient, (_, _) => events.Add("started"));
        WeakReferenceMessenger.Default.Register<HideWindowMessage>(recipient, (_, _) => events.Add("hide"));
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(recipient, (_, message) =>
        {
            events.Add("completed");
            invocation = message;
        });
        WeakReferenceMessenger.Default.Register<DismissMessage>(recipient, (_, _) =>
        {
            events.Add("dismiss");
            finished.TrySetResult();
        });
        WeakReferenceMessenger.Default.Register<ErrorOccurredMessage>(recipient, (_, _) => finished.TrySetResult());
        WeakReferenceMessenger.Default.Register<TelemetryInvokeResultMessage>(recipient, (_, message) =>
        {
            if (message.Kind == CommandResultKind.KeepOpen)
            {
                finished.TrySetResult();
            }
        });

        try
        {
            var result = resultKind switch
            {
                CommandResultKind.Dismiss => CommandResult.Dismiss(),
                CommandResultKind.Hide => CommandResult.Hide(),
                _ => CommandResult.KeepOpen(),
            };
            var command = new TestCommand(result, () =>
            {
                if (hideDuringInvoke)
                {
                    WeakReferenceMessenger.Default.Send<HideWindowMessage>();
                }

                if (throwDuringInvoke)
                {
                    throw new InvalidOperationException("Test invocation failure");
                }
            })
            {
                Id = "test.command",
            };
            viewModel.Receive(new PerformCommandMessage(new ExtensionObject<ICommand>(command)));

            await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var expected = new List<string> { "started" };
            if (hideDuringInvoke)
            {
                expected.Add("hide");
            }

            expected.Add("completed");
            if (resultKind is CommandResultKind.Dismiss or CommandResultKind.Hide)
            {
                expected.Add("dismiss");
            }

            CollectionAssert.AreEqual(expected, events);
            Assert.IsNotNull(invocation);
            Assert.AreEqual(!throwDuringInvoke, invocation.Success);
            Assert.AreEqual(command.Id, invocation.CommandId);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            WeakReferenceMessenger.Default.UnregisterAll(viewModel);
        }
    }

    [TestMethod]
    public async Task PerformCommand_DoesNotCountAnInvocationRejectedWhileBusy()
    {
        var viewModel = CreateViewModel();
        var recipient = new object();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var started = 0;
        WeakReferenceMessenger.Default.Register<TelemetryCommandStartedMessage>(recipient, (_, _) => Interlocked.Increment(ref started));
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(recipient, (_, _) => completed.TrySetResult());

        try
        {
            var command = new TestCommand(CommandResult.KeepOpen(), () =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the invocation");
                }
            });
            var message = new PerformCommandMessage(new ExtensionObject<ICommand>(command));
            viewModel.Receive(message);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            viewModel.Receive(message);

            Assert.AreEqual(1, Volatile.Read(ref started));
        }
        finally
        {
            release.Set();
            try
            {
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
                WeakReferenceMessenger.Default.UnregisterAll(viewModel);
            }
        }
    }

    [TestMethod]
    public void PerformCommand_CountsPageNavigationOnceAfterShowingWindow()
    {
        var viewModel = CreateViewModel();
        var recipient = new object();
        var events = new List<string>();
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(recipient, (_, _) => events.Add("show"));
        WeakReferenceMessenger.Default.Register<TelemetryCommandStartedMessage>(recipient, (_, _) => events.Add("started"));
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(recipient, (_, _) => events.Add("completed"));
        WeakReferenceMessenger.Default.Register<NavigateToPageMessage>(recipient, (_, _) => events.Add("navigate"));

        try
        {
            viewModel.Receive(new PerformCommandMessage(new ExtensionObject<ICommand>(new TestPage())) { ShowWindowIfPage = true });

            string[] expected = ["show", "started", "completed", "navigate"];
            CollectionAssert.AreEqual(expected, events);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            WeakReferenceMessenger.Default.UnregisterAll(viewModel);
        }
    }

    private static ShellViewModel CreateViewModel()
    {
        var host = new TestAppExtensionHost();
        var appHostService = new Mock<IAppHostService>();
        appHostService.Setup(service => service.GetDefaultHost()).Returns(host);
        appHostService.Setup(service => service.GetHostForCommand(It.IsAny<object?>(), It.IsAny<AppExtensionHost?>())).Returns(host);
        appHostService.Setup(service => service.GetProviderContextForCommand(It.IsAny<object?>(), It.IsAny<ICommandProviderContext?>())).Returns(CommandProviderContext.Empty);

        var pageFactory = new Mock<IPageViewModelFactoryService>();
        pageFactory.Setup(factory => factory.TryCreatePageViewModel(It.IsAny<IPage>(), It.IsAny<bool>(), It.IsAny<AppExtensionHost>(), It.IsAny<ICommandProviderContext>()))
            .Returns(new TestPageViewModel(host));

        return new ShellViewModel(
            TaskScheduler.Default,
            Mock.Of<IRootPageService>(),
            pageFactory.Object,
            appHostService.Object);
    }
}
