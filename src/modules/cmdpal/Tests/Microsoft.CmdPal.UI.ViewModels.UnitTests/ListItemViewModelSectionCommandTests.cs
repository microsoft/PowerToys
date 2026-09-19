// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ListItemViewModelSectionCommandTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed class MessageRecipient
    {
        public PerformCommandMessage? Message { get; set; }
    }

    [TestMethod]
    public void InitializeProperties_LoadsSectionCommandWithoutMakingSeparatorInteractive()
    {
        var command = new NoOpCommand { Name = "Show more..." };
        var separator = new Separator("Recent", command);
        var (viewModel, pageContext) = CreateViewModel(separator);

        try
        {
            Assert.AreEqual(ListItemType.SectionHeader, viewModel.Type);
            Assert.IsFalse(viewModel.IsInteractive);
            Assert.IsTrue(viewModel.IsSectionCommandTarget);
            Assert.IsTrue(viewModel.IsKeyboardNavigable);
            Assert.IsFalse(viewModel.Command.IsSet);
            Assert.IsTrue(viewModel.HasSectionCommand);
            Assert.AreEqual("Show more...", viewModel.SectionCommandName);
            Assert.AreEqual("Recent, Show more...", viewModel.SectionCommandAccessibleName);
            Assert.AreEqual("Recent, Show more...", viewModel.ToString());
            Assert.AreSame(command, viewModel.SectionCommand?.Model.Unsafe);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void SectionCommandChange_ReplacesAndClearsSectionCommand()
    {
        var first = new NoOpCommand { Name = "First" };
        var second = new NoOpCommand { Name = "Second" };
        var separator = new Separator("Recent", first);
        var (viewModel, pageContext) = CreateViewModel(separator);

        try
        {
            separator.SectionCommand = second;

            Assert.AreSame(second, viewModel.SectionCommand?.Model.Unsafe);
            Assert.AreEqual("Second", viewModel.SectionCommandName);
            Assert.IsTrue(viewModel.HasSectionCommand);
            Assert.AreEqual(ListItemType.SectionHeader, viewModel.Type);

            separator.SectionCommand = null;

            Assert.IsNull(viewModel.SectionCommand);
            Assert.AreEqual(string.Empty, viewModel.SectionCommandName);
            Assert.AreEqual("Recent", viewModel.SectionCommandAccessibleName);
            Assert.IsFalse(viewModel.HasSectionCommand);
            Assert.IsFalse(viewModel.IsSectionCommandTarget);
            Assert.IsFalse(viewModel.IsKeyboardNavigable);
            Assert.IsFalse(viewModel.Command.IsSet);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void SectionCommandSelection_IsIndependentFromListItemInteractivity()
    {
        var separator = new Separator("Recent", new NoOpCommand { Name = "Show more..." });
        var (viewModel, pageContext) = CreateViewModel(separator);

        try
        {
            viewModel.SetSectionCommandSelected(true);

            Assert.IsTrue(viewModel.IsSectionCommandSelected);
            Assert.IsFalse(viewModel.IsInteractive);

            viewModel.SetSectionCommandSelected(false);

            Assert.IsFalse(viewModel.IsSectionCommandSelected);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void UntitledSeparator_WithSectionCommand_IsNotKeyboardNavigable()
    {
        var separator = new Separator(string.Empty, new NoOpCommand { Name = "Show more..." });
        var (viewModel, pageContext) = CreateViewModel(separator);

        try
        {
            Assert.AreEqual(ListItemType.Separator, viewModel.Type);
            Assert.IsFalse(viewModel.IsSectionCommandTarget);
            Assert.IsFalse(viewModel.IsKeyboardNavigable);

            viewModel.SetSectionCommandSelected(true);
            Assert.IsFalse(viewModel.IsSectionCommandSelected);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void InvokeSectionCommand_SendsCommandWithSeparatorContext()
    {
        var command = new NoOpCommand { Name = "Show more..." };
        var separator = new Separator("Recent", command);
        var (viewModel, pageContext) = CreateViewModel(separator);
        var recipient = new MessageRecipient();
        WeakReferenceMessenger.Default.Register<MessageRecipient, PerformCommandMessage>(recipient, static (r, message) => r.Message = message);

        try
        {
            viewModel.InvokeSectionCommandCommand.Execute(null);

            Assert.IsNotNull(recipient.Message);
            Assert.AreSame(command, recipient.Message.Command.Unsafe);
            Assert.AreSame(separator, recipient.Message.Context);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    private static (ListItemViewModel ViewModel, TestPageContext PageContext) CreateViewModel(Separator separator)
    {
        var pageContext = new TestPageContext();
        var viewModel = new ListItemViewModel(separator, new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.InitializeProperties();
        return (viewModel, pageContext);
    }
}
