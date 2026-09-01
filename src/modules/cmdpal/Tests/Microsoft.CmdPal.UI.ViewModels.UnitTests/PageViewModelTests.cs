// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
