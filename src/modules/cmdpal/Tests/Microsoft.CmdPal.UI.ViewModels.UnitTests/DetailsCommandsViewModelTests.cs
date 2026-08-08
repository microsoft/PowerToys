// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DetailsCommandsViewModelTests
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
    public void InitializeProperties_BuildsACommandPerExtensionCommand()
    {
        var pageContext = new TestPageContext();
        var element = ElementWith(new Command { Name = "First" }, new Command { Name = "Second" });

        var vm = new DetailsCommandsViewModel(element, new(pageContext));
        vm.InitializeProperties();

        Assert.AreEqual(2, vm.Commands.Count);
        Assert.IsTrue(vm.HasCommands);
    }

    [TestMethod]
    public void Cleanup_UnsubscribesFromPropChanged()
    {
        var pageContext = new TestPageContext();
        var command = new Command { Name = "Original" };
        var vm = new DetailsCommandsViewModel(ElementWith(command), new(pageContext));
        vm.InitializeProperties();
        var commandVm = vm.Commands[0];

        vm.SafeCleanup();
        command.Name = "After cleanup";
        commandVm.ApplyPendingUpdates();

        Assert.AreEqual("Original", commandVm.Name);
    }

    [TestMethod]
    public void Cleanup_ReleasesCommandViewModels()
    {
        // The extension-side command is what does the rooting: initializing a
        // CommandViewModel subscribes to its PropChanged, so an unrevoked
        // handler keeps the view-model alive for as long as the extension
        // holds the command. Keep it alive here to model that.
        var pageContext = new TestPageContext();
        var command = new Command { Name = "Do the thing" };

        var weakCommandVm = BuildInitializeAndCleanup(ElementWith(command), pageContext);

        GcAssert.IsCollected(weakCommandVm, "CommandViewModel");

        GC.KeepAlive(command);
        GC.KeepAlive(pageContext);
    }

    // Separate frame so the view-models are unreachable on return - a Debug
    // build keeps locals alive to the end of their scope, so nulling them out
    // in the test body would not be enough.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CommandViewModel> BuildInitializeAndCleanup(
        IDetailsElement element,
        IPageContext pageContext)
    {
        var vm = new DetailsCommandsViewModel(element, new(pageContext));
        vm.InitializeProperties();

        // Captured before cleanup - cleanup empties the list.
        var weak = new WeakReference<CommandViewModel>(vm.Commands[0]);

        vm.SafeCleanup();
        return weak;
    }

    private static DetailsElement ElementWith(params ICommand[] commands) =>
        new()
        {
            Key = "commands",
            Data = new DetailsCommands { Commands = commands },
        };
}
