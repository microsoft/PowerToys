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
    public void PrepareForOpen_UpdatesOnlyThatContextMenu()
    {
        var firstContext = CreateContext();
        var secondContext = CreateContext();
        var replacementContext = CreateContext();
        var firstMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        var secondMenu = new ContextMenuViewModel(Mock.Of<IFuzzyMatcherProvider>());
        firstMenu.PrepareForOpen(firstContext);
        secondMenu.PrepareForOpen(secondContext);

        firstMenu.PrepareForOpen(replacementContext);

        Assert.AreSame(replacementContext, firstMenu.SelectedItem);
        Assert.AreSame(secondContext, secondMenu.SelectedItem);
    }

    private static ICommandBarContext CreateContext()
    {
        var context = new Mock<ICommandBarContext>();
        context.SetupGet(x => x.AllCommands).Returns([]);
        return context.Object;
    }
}
