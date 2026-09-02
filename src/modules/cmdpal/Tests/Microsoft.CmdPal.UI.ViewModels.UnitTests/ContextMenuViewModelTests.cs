// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class ContextMenuViewModelTests
{
    [TestMethod]
    public void SetCommandContext_UpdatesOnlyThatContextMenu()
    {
        var firstContext = CreateContext();
        var secondContext = CreateContext();
        var replacementContext = CreateContext();
        var firstMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        var secondMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        firstMenu.SetCommandContext(firstContext);
        secondMenu.SetCommandContext(secondContext);

        firstMenu.SetCommandContext(replacementContext);

        Assert.AreSame(replacementContext, firstMenu.SelectedItem);
        Assert.AreSame(secondContext, secondMenu.SelectedItem);
    }

    private static ICommandBarContext CreateContext()
    {
        var context = new Mock<ICommandBarContext>();
        context.SetupGet(x => x.AllCommands).Returns([]);
        context.SetupGet(x => x.MoreCommands).Returns([]);
        return context.Object;
    }
}
