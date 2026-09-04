// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
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

    private sealed partial class TestCommand(CommandResult result) : InvokableCommand
    {
        public override ICommandResult Invoke() => result;
    }

    [TestMethod]
    [DataRow(CommandResultKind.Dismiss)]
    [DataRow(CommandResultKind.Hide)]
    public async Task PerformCommand_ReportsInvocationBeforeDismissal(CommandResultKind resultKind)
    {
        var host = new TestAppExtensionHost();
        var appHostService = new Mock<IAppHostService>();
        appHostService.Setup(service => service.GetDefaultHost()).Returns(host);
        appHostService.Setup(service => service.GetHostForCommand(It.IsAny<object?>(), It.IsAny<AppExtensionHost?>())).Returns(host);
        appHostService.Setup(service => service.GetProviderContextForCommand(It.IsAny<object?>(), It.IsAny<ICommandProviderContext?>())).Returns(CommandProviderContext.Empty);

        var viewModel = new ShellViewModel(
            TaskScheduler.Default,
            Mock.Of<IRootPageService>(),
            Mock.Of<IPageViewModelFactoryService>(),
            appHostService.Object);
        var recipient = new object();
        var dismissed = new TaskCompletionSource<TelemetryExtensionInvokedMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryExtensionInvokedMessage? invocation = null;
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(recipient, (_, message) => invocation = message);
        WeakReferenceMessenger.Default.Register<DismissMessage>(recipient, (_, _) => dismissed.TrySetResult(invocation));

        try
        {
            var command = new TestCommand(resultKind == CommandResultKind.Dismiss ? CommandResult.Dismiss() : CommandResult.Hide())
            {
                Id = "test.command",
            };
            viewModel.Receive(new PerformCommandMessage(new ExtensionObject<ICommand>(command)));

            var reportedInvocation = await dismissed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsNotNull(reportedInvocation, "The session must count the invocation before dismissal is queued.");
            Assert.IsTrue(reportedInvocation.Success);
            Assert.AreEqual(command.Id, reportedInvocation.CommandId);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            WeakReferenceMessenger.Default.UnregisterAll(viewModel);
        }
    }
}
