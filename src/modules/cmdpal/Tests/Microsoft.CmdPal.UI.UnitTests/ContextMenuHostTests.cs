// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ContextMenuHostTests
{
    [TestMethod]
    [DataRow("deferred")]
    [DataRow("immediate")]
    [DataRow("invalid")]
    [DataRow("close")]
    public void PendingOpen_IsSupersededOrCancelledBeforePreparingItsContext(string action)
    {
        Queue<Action> callbacks = new();
        List<ContextMenuRequest> opened = [];
        var closes = 0;
        var host = new ContextMenuHost(
            callback =>
            {
                callbacks.Enqueue(callback);
                return true;
            },
            opened.Add,
            () => closes++);
        var current = new ContextMenuRequest(Mock.Of<IContextMenuContext>());
        host.ShowAfterKeyEvent(() => throw new AssertFailedException("A superseded request must not prepare its context."));

        switch (action)
        {
            case "deferred":
                host.ShowAfterKeyEvent(() => current);
                break;
            case "immediate":
                host.Show(current);
                break;
            case "invalid":
                host.ShowAfterKeyEvent(() => null);
                break;
            case "close":
                host.Close();
                break;
        }

        while (callbacks.TryDequeue(out var callback))
        {
            callback();
        }

        if (action is "deferred" or "immediate")
        {
            CollectionAssert.AreEqual(new[] { current }, opened);
        }
        else
        {
            Assert.IsEmpty(opened);
        }

        Assert.AreEqual(action == "close" ? 1 : 0, closes);
    }
}
