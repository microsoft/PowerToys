// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class CommandBarViewModelTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvokeContextCommand_LeafPreservesCommandContextAndHostRoute(bool dock)
    {
        var page = CreatePage(dock);
        var model = new CommandContextItem(new NoOpCommand { Name = "Action" });
        var command = new CommandContextItemViewModel(model, new(page));
        command.SlowInitializeProperties();
        var recipient = new MessageRecipient();
        WeakReferenceMessenger.Default.Register<MessageRecipient, PerformCommandMessage>(
            recipient,
            static (r, message) => r.Messages.Add(message));
        var submenuRequests = 0;

        try
        {
            CommandBarViewModel.InvokeContextCommand(command, _ => submenuRequests++);

            Assert.AreEqual(0, submenuRequests);
            Assert.HasCount(1, recipient.Messages);
            var message = recipient.Messages[0];
            Assert.AreSame(command.Command, message.Command);
            Assert.AreSame(model, message.CommandContext);
            Assert.AreSame(page, message.Context?.Page);
            Assert.AreEqual(page.DockRoute, message.DockRoute);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            command.SafeCleanup();
            page.SafeCleanup();
        }
    }

    [TestMethod]
    public void InvokeContextCommand_SubmenuRequestsPresentationWithoutInvokingCommand()
    {
        var page = CreatePage(dock: true);
        var model = new CommandContextItem(new NoOpCommand { Name = "Tools" })
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Child" })],
        };
        var command = new CommandContextItemViewModel(model, new(page));
        command.SlowInitializeProperties();
        var recipient = new MessageRecipient();
        WeakReferenceMessenger.Default.Register<MessageRecipient, PerformCommandMessage>(
            recipient,
            static (r, message) => r.Messages.Add(message));
        var submenus = new List<CommandContextItemViewModel>();

        try
        {
            CommandBarViewModel.InvokeContextCommand(command, submenus.Add);

            Assert.HasCount(1, submenus);
            Assert.AreSame(command, submenus[0]);
            Assert.IsEmpty(recipient.Messages);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            command.SafeCleanup();
            page.SafeCleanup();
        }
    }

    private static PageViewModel CreatePage(bool dock) => new(
        new Page(),
        TaskScheduler.Default,
        new TestAppExtensionHost(),
        CommandProviderContext.Empty)
    {
        DockRoute = dock ? new DockCommandRoute((nint)42, Guid.NewGuid()) : null,
    };

    private sealed class MessageRecipient
    {
        public List<PerformCommandMessage> Messages { get; } = [];
    }
}
