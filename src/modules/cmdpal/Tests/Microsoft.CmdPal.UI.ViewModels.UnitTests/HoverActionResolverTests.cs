// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class HoverActionResolverTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private static CommandContextItemViewModel CreateContextItem(
        string title,
        bool showInHover = false,
        int hoverOrder = 0,
        bool hostInjected = false)
    {
        ICommandContextItem item = new CommandContextItem(new NoOpCommand { Name = title })
        {
            Title = title,
            ShowInHoverActions = showInHover,
            HoverOrder = hoverOrder,
        };

        var vm = new CommandContextItemViewModel(item, new WeakReference<IPageContext>(new TestPageContext()));
        vm.InitializeProperties();

        if (hostInjected)
        {
            typeof(CommandContextItemViewModel)
                .GetProperty(nameof(CommandContextItemViewModel.IsHostInjected), BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(vm, true);
        }

        return vm;
    }

    private static HoverActionResolveContext CreateContext(
        bool enableHover = true,
        bool isHome = false,
        HoverActionsMode mode = HoverActionsMode.FirstN,
        int max = -1,
        params CommandContextItemViewModel[] commands)
    {
        return new HoverActionResolveContext(
            enableHover,
            isHome,
            mode,
            max,
            HoverActionsVisibility.Default,
            IsRowHovered: true,
            IsListSelected: false,
            commands);
    }

    [TestMethod]
    public void Resolve_UserDisabled_ReturnsEmpty()
    {
        var commands = new[] { CreateContextItem("Edit") };

        var result = HoverActionResolver.Resolve(CreateContext(enableHover: false, commands: commands));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Resolve_HomeSurface_SkipsHostInjectedCommands()
    {
        var commands = new[]
        {
            CreateContextItem("Pin to home", hostInjected: true),
            CreateContextItem("Edit"),
            CreateContextItem("Run as admin"),
            CreateContextItem("Open folder"),
        };

        var result = HoverActionResolver.Resolve(CreateContext(isHome: true, commands: commands));

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Edit", result[0].Title);
        Assert.AreEqual("Run as admin", result[1].Title);
        Assert.AreEqual("Open folder", result[2].Title);
    }

    [TestMethod]
    public void Resolve_ExtensionSurface_TakesFirstThree()
    {
        var commands = new[]
        {
            CreateContextItem("One"),
            CreateContextItem("Two"),
            CreateContextItem("Three"),
            CreateContextItem("Four"),
        };

        var result = HoverActionResolver.Resolve(CreateContext(commands: commands));

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("One", result[0].Title);
        Assert.AreEqual("Two", result[1].Title);
        Assert.AreEqual("Three", result[2].Title);
    }

    [TestMethod]
    public void Resolve_ExplicitMode_UsesShowInHoverActionsAndOrder()
    {
        var commands = new[]
        {
            CreateContextItem("Delete", showInHover: true, hoverOrder: 30),
            CreateContextItem("Edit", showInHover: true, hoverOrder: 10),
            CreateContextItem("Pin", showInHover: true, hoverOrder: 20),
            CreateContextItem("Hidden"),
        };

        var result = HoverActionResolver.Resolve(CreateContext(mode: HoverActionsMode.Explicit, commands: commands));

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Edit", result[0].Title);
        Assert.AreEqual("Pin", result[1].Title);
        Assert.AreEqual("Delete", result[2].Title);
    }

    [TestMethod]
    public void Resolve_FirstNUpgradesToExplicitWhenFlagged()
    {
        var commands = new[]
        {
            CreateContextItem("One"),
            CreateContextItem("Two", showInHover: true, hoverOrder: 2),
            CreateContextItem("Three", showInHover: true, hoverOrder: 1),
        };

        var result = HoverActionResolver.Resolve(CreateContext(commands: commands));

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Three", result[0].Title);
        Assert.AreEqual("Two", result[1].Title);
    }

    [TestMethod]
    public void ShouldShowHoverStrip_RespectsVisibility()
    {
        var commands = new[] { CreateContextItem("Edit") };
        var hovered = CreateContext(commands: commands);
        var selectedOnly = hovered with { IsRowHovered = false, IsListSelected = true };
        var hidden = hovered with { IsRowHovered = false, IsListSelected = false };

        Assert.IsTrue(HoverActionResolver.ShouldShowHoverStrip(hovered, hasHoverActions: true));
        Assert.IsTrue(HoverActionResolver.ShouldShowHoverStrip(selectedOnly with { Visibility = HoverActionsVisibility.HoverOrSelected }, hasHoverActions: true));
        Assert.IsFalse(HoverActionResolver.ShouldShowHoverStrip(selectedOnly with { Visibility = HoverActionsVisibility.OnHoverOnly }, hasHoverActions: true));
        Assert.IsFalse(HoverActionResolver.ShouldShowHoverStrip(hidden, hasHoverActions: true));
    }
}
