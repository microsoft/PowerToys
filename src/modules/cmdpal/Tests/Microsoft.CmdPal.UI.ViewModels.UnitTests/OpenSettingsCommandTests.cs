// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class OpenSettingsCommandTests
{
    private sealed class SettingsMessageRecipient
    {
        public List<OpenSettingsMessage> Messages { get; } = [];
    }

    [TestMethod]
    public void OpenSettingsCommand_Invoke_SendsGeneralSettingsMessage()
    {
        var recipient = new SettingsMessageRecipient();
        WeakReferenceMessenger.Default.Register<SettingsMessageRecipient, OpenSettingsMessage>(recipient, static (r, m) => r.Messages.Add(m));

        try
        {
            var command = new OpenSettingsCommand();

            command.Invoke();

            Assert.AreEqual(OpenSettingsCommand.SettingsCommandId, command.Id);
            Assert.AreEqual(1, recipient.Messages.Count);
            Assert.AreEqual(string.Empty, recipient.Messages[0].SettingsPageTag);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public void OpenGallerySettingsCommand_Invoke_SendsGallerySettingsMessage()
    {
        var recipient = new SettingsMessageRecipient();
        WeakReferenceMessenger.Default.Register<SettingsMessageRecipient, OpenSettingsMessage>(recipient, static (r, m) => r.Messages.Add(m));

        try
        {
            var command = new OpenGallerySettingsCommand();

            command.Invoke();

            Assert.AreEqual(1, recipient.Messages.Count);
            Assert.AreEqual("Gallery", recipient.Messages[0].SettingsPageTag);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public void OpenSettingsCommand_Invoke_PreservesExtensionDestination()
    {
        var recipient = new SettingsMessageRecipient();
        WeakReferenceMessenger.Default.Register<SettingsMessageRecipient, OpenSettingsMessage>(recipient, static (r, m) => r.Messages.Add(m));
        try
        {
            var destination = new OpenSettingsMessage(
                SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page,
                ExtensionProviderId: "owning.extension");
            var command = new OpenSettingsCommand(destination, "Extension settings", OpenSettingsCommand.ExtensionSettingsCommandId);

            command.Invoke();

            Assert.AreEqual("Extension settings", command.Name);
            Assert.AreEqual(OpenSettingsCommand.ExtensionSettingsCommandId, command.Id);
            Assert.AreNotEqual(OpenSettingsCommand.SettingsCommandId, command.Id);
            Assert.HasCount(1, recipient.Messages);
            Assert.AreSame(destination, recipient.Messages[0]);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }
}
