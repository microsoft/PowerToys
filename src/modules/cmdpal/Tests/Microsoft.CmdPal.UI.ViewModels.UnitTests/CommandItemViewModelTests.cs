// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CommandItemViewModelTests
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

    [TestMethod]
    public void MoreCommandsAndAllCommands_ReturnSnapshots()
    {
        // The public getters should return cached read-only snapshots, so
        // repeated reads don't allocate a new list when the backing data hasn't
        // changed.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        var moreCommands = viewModel.MoreCommands;
        var allCommands = viewModel.AllCommands;

        Assert.AreSame(moreCommands, viewModel.MoreCommands);
        Assert.AreSame(allCommands, viewModel.AllCommands);
        Assert.AreEqual(1, moreCommands.Count);
        Assert.AreEqual(2, allCommands.Count);
    }

    [TestMethod]
    public void SecondaryCommand_IgnoresLeadingSeparators()
    {
        // SecondaryCommand/HasMoreCommands should be derived from the first actual command item,
        // not from the raw first entry in MoreCommands.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands =
            [
                new Separator("Group"),
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.IsTrue(viewModel.HasMoreCommands);
        Assert.IsNotNull(viewModel.SecondaryCommand);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand.Name);
    }
}
