// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ShowToastMessageTests
{
    [TestMethod]
    public void DefaultToastKeepsExistingDuration()
    {
        var message = new ShowToastMessage("Default toast");

        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), message.VisibleDuration);
    }

    [TestMethod]
    public void ActionToastKeepsLongerDuration()
    {
        var context = Mock.Of<IPageContext>();
        var command = new CommandViewModel(new NoOpCommand(), new WeakReference<IPageContext>(context));
        var message = new ShowToastMessage("Action toast", Command: command);

        Assert.AreEqual(TimeSpan.FromSeconds(5), message.VisibleDuration);
        GC.KeepAlive(context);
    }

    [TestMethod]
    public void CustomDurationAppliesOnlyToThatToast()
    {
        var message = new ShowToastMessage("Installing Test App")
        {
            Duration = TimeSpan.FromSeconds(4),
        };

        Assert.AreEqual(TimeSpan.FromSeconds(4), message.VisibleDuration);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), new ShowToastMessage("Default toast").VisibleDuration);
    }
}
