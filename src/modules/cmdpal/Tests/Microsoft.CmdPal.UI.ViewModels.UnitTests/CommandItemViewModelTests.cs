// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
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

    [TestMethod]
    public void FastInitializeProperties_CreatesPrimaryContextItem()
    {
        // Context menus are opened from fast-initialized list items before slow init completes.
        // The synthetic primary command must already exist so the first right-click can open the menu.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.FastInitializeProperties();

        Assert.AreEqual(1, viewModel.AllCommands.Count);
        Assert.IsTrue(viewModel.CanOpenContextMenu);
        Assert.AreEqual("Primary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);
    }

    [TestMethod]
    public void LatePrimaryCommandCreation_AddsPrimaryToAllCommands()
    {
        // Reproduces issue where SlowInitializeProperties runs before a real primary command exists.
        // The late-arriving command should still create the synthetic primary context item and prepend it to AllCommands.
        var pageContext = new TestPageContext();
        var item = new CommandItem()
        {
            Command = null,
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.AreEqual(1, viewModel.AllCommands.Count);
        Assert.AreEqual("Secondary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);

        item.Command = new NoOpCommand { Name = "Primary" };

        Assert.AreEqual(2, viewModel.AllCommands.Count);
        Assert.AreEqual("Primary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);
        Assert.AreEqual("Secondary", ((CommandContextItemViewModel)viewModel.AllCommands[1]).Name);
        Assert.IsTrue(viewModel.HasMoreCommands);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand?.Name);
    }

    [TestMethod]
    public void SyntheticPrimaryContextItem_UpdatesSubtitleAndCachedSubtitleTarget()
    {
        // The synthetic primary context item copies subtitle state from the parent CommandItemViewModel.
        // When subtitle changes later, both the exposed subtitle and its cached fuzzy-search target must refresh.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Subtitle = "before",
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        var primaryContextItem = (CommandContextItemViewModel)viewModel.AllCommands[0];
        var matcher = new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

        Assert.AreEqual("before", primaryContextItem.Subtitle);
        Assert.AreEqual("before", primaryContextItem.GetSubtitleTarget(matcher).Original);

        item.Subtitle = "after unique";

        Assert.AreEqual("after unique", primaryContextItem.Subtitle);
        Assert.AreEqual("after unique", primaryContextItem.GetSubtitleTarget(matcher).Original);
    }
}
