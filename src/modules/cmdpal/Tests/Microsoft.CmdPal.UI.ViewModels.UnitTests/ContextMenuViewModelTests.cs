// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class ContextMenuViewModelTests
{
    private sealed class MessageRecipient
    {
        public List<PerformCommandMessage> Messages { get; } = [];
    }

    [TestMethod]
    public void SetCommandContext_UpdatesOnlyThatContextMenu()
    {
        var firstContext = CreateContext();
        var secondContext = CreateContext();
        var replacementContext = CreateContext();
        var firstMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        var secondMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        firstMenu.SetCommandContext(firstContext);
        secondMenu.SetCommandContext(secondContext);

        firstMenu.SetCommandContext(replacementContext);

        Assert.AreSame(replacementContext, firstMenu.SelectedItem);
        Assert.AreSame(secondContext, secondMenu.SelectedItem);
    }

    [TestMethod]
    public void InvokeCommand_CanceledSend_DoesNotPublishTheMessage()
    {
        var page = new PageViewModel(
            new Page(),
            TaskScheduler.Default,
            new TestAppExtensionHost(),
            CommandProviderContext.Empty);
        var model = new ListItem(new NoOpCommand { Name = "Run" });
        var command = new CommandItemViewModel(
            new ExtensionObject<ICommandItem>(model),
            new(page),
            DefaultContextMenuFactory.Instance);
        command.SlowInitializeProperties();

        var contextMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        var recipient = new MessageRecipient();
        WeakReferenceMessenger.Default.Register<MessageRecipient, PerformCommandMessage>(
            recipient,
            static (r, message) => r.Messages.Add(message));
        var commandInvoked = false;
        EventHandler<PerformCommandMessage> cancelSend = (_, message) => message.CancelSend();
        contextMenu.CommandInvoking += cancelSend;
        contextMenu.CommandInvoked += (_, _) => commandInvoked = true;

        try
        {
            Assert.AreEqual(ContextKeybindingResult.Hide, contextMenu.InvokeCommand(command));
            Assert.AreEqual(0, recipient.Messages.Count);
            Assert.IsFalse(commandInvoked);

            contextMenu.CommandInvoking -= cancelSend;
            Assert.AreEqual(ContextKeybindingResult.Hide, contextMenu.InvokeCommand(command));
            Assert.AreEqual(1, recipient.Messages.Count);
            Assert.IsTrue(commandInvoked);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            command.SafeCleanup();
            page.SafeCleanup();
        }
    }

    private static ICommandBarContext CreateContext()
    {
        var context = new Mock<ICommandBarContext>();
        context.SetupGet(x => x.AllCommands).Returns([]);
        context.SetupGet(x => x.MoreCommands).Returns([]);
        return context.Object;
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }
}
