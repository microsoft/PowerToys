// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class CloseOnEnterTests
{
    [TestMethod]
    public void PrimaryIsCopy_WhenCloseOnEnterTrue()
    {
        var settings = new Settings(closeOnEnter: true);
        TypedEventHandler<object, object> handleReplace = (s, e) => { };

        var item = ResultHelper.CreateResultForPage(
            4m,
            CultureInfo.CurrentCulture,
            CultureInfo.CurrentCulture,
            "2+2",
            settings,
            handleReplace);

        Assert.IsNotNull(item);
        Assert.IsInstanceOfType(item.Command, typeof(CopyTextCommand));
        Assert.IsTrue(item.MoreCommands.OfType<CommandContextItem>().All(command => command.Command is not SaveCommand));

        var result = ((CopyTextCommand)item.Command).Result;
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toastArgs = result.Args as ToastArgs;
        Assert.IsNotNull(toastArgs);
        Assert.AreEqual(CommandResultKind.Hide, ((CommandResult)toastArgs.Result).Kind);
    }

    [TestMethod]
    public void PrimaryIsCopy_WhenCloseOnEnterFalse()
    {
        var settings = new Settings(closeOnEnter: false);
        TypedEventHandler<object, object> handleReplace = (s, e) => { };

        var item = ResultHelper.CreateResultForPage(
            4m,
            CultureInfo.CurrentCulture,
            CultureInfo.CurrentCulture,
            "2+2",
            settings,
            handleReplace);

        Assert.IsNotNull(item);
        Assert.IsInstanceOfType(item.Command, typeof(CopyTextCommand));
        Assert.IsTrue(item.MoreCommands.OfType<CommandContextItem>().All(command => command.Command is not SaveCommand));

        var result = ((CopyTextCommand)item.Command).Result;
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toastArgs = result.Args as ToastArgs;
        Assert.IsNotNull(toastArgs);
        Assert.AreEqual(CommandResultKind.KeepOpen, ((CommandResult)toastArgs.Result).Kind);
    }
}
