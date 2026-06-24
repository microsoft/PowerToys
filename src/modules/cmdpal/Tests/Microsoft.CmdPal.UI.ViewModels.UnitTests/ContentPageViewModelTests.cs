// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ContentPageViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private static CommandContextItem Command(string name) => new(new NoOpCommand { Name = name });

    private static ContentPageViewModel CreateViewModel(TestContentPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);

    [TestMethod]
    public void AllCommandsAndMoreCommands_ReturnCachedSnapshots()
    {
        // Content pages should expose stable snapshots, not the live Commands
        // list, so repeated reads don't allocate and callers can't observe
        // in-place list mutations.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var allCommands = viewModel.AllCommands;
        var moreCommands = viewModel.MoreCommands;

        Assert.AreSame(allCommands, viewModel.AllCommands);
        Assert.AreSame(moreCommands, viewModel.MoreCommands);
        Assert.AreEqual(2, allCommands.Count);
        Assert.AreEqual(1, moreCommands.Count);
        Assert.AreEqual("Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand?.Name);
    }

    [TestMethod]
    public void CommandsUpdate_RefreshesSnapshotsConsistently()
    {
        // Updating the model commands should swap in a new coherent snapshot.
        // The old snapshots stay intact, and the new cached values agree on
        // counts, primary/secondary commands, and "has more" state.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var oldAllCommands = viewModel.AllCommands;
        var oldMoreCommands = viewModel.MoreCommands;

        page.Commands =
        [
            Command("Updated Primary"),
            new Separator("Group"),
            Command("Updated Secondary"),
        ];

        Assert.AreEqual(2, oldAllCommands.Count);
        Assert.AreEqual(1, oldMoreCommands.Count);

        Assert.AreEqual(3, viewModel.AllCommands.Count);
        Assert.AreEqual(2, viewModel.MoreCommands.Count);
        Assert.IsTrue(viewModel.HasCommands);
        Assert.IsTrue(viewModel.HasMoreCommands);
        Assert.AreEqual("Updated Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommandName);
    }
}
