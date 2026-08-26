// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class PageViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    [TestMethod]
    public void IconUpdate_InitializesReplacementIcon()
    {
        var page = new Page
        {
            Id = "page",
            Name = "Page",
            Icon = new IconInfo("initial"),
        };
        var viewModel = new PageViewModel(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        var initialIcon = viewModel.Icon;

        page.Icon = new IconInfo(new IconData("light"), new IconData("dark"));

        Assert.AreNotSame(initialIcon, viewModel.Icon);
        Assert.AreEqual("light", viewModel.Icon.Light.Icon);
        Assert.AreEqual("dark", viewModel.Icon.Dark.Icon);
    }

    [TestMethod]
    public void PrepareCommandMessages_AddsPageContextAndPreservesHandlers()
    {
        var host = new TestAppExtensionHost();
        var providerContext = CommandProviderContext.Empty;
        var viewModel = new PageViewModel(new Page(), TaskScheduler.Default, host, providerContext);
        Action confirmation = () => { };
        Func<ICommandResult, bool> resultHandler = _ => true;

        var perform = new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand()))
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };
        var handled = new HandleCommandResultMessage(new(Mock.Of<ICommandResult>()))
        {
            OnBeforeShowConfirmation = confirmation,
            ResultHandler = resultHandler,
        };

        Assert.AreSame(perform, viewModel.PreparePerformCommandMessage(perform));
        Assert.AreSame(viewModel, perform.SourcePage);
        Assert.AreSame(host, perform.SourceExtensionHost);
        Assert.AreSame(providerContext, perform.SourceProviderContext);
        Assert.AreSame(confirmation, perform.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, perform.ResultHandler);

        Assert.AreSame(handled, viewModel.PrepareHandleCommandResultMessage(handled));
        Assert.AreSame(viewModel, handled.SourcePage);
        Assert.AreSame(host, handled.SourceExtensionHost);
        Assert.AreSame(providerContext, handled.SourceProviderContext);
        Assert.AreSame(confirmation, handled.OnBeforeShowConfirmation);
        Assert.AreSame(resultHandler, handled.ResultHandler);
    }
}
