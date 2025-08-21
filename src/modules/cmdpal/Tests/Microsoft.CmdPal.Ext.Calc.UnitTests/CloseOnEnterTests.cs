// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Helper;
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
        TypedEventHandler<object, object> handleSave = (s, e) => { };

        var item = ResultHelper.CreateResult(
            4m,
            CultureInfo.CurrentCulture,
            CultureInfo.CurrentCulture,
            "2+2",
            settings,
            handleSave);

        Assert.IsNotNull(item);
        Assert.IsInstanceOfType(item.Command, typeof(CopyTextCommand));

        var firstMore = item.MoreCommands.First();
        Assert.IsInstanceOfType(firstMore, typeof(CommandContextItem));
        Assert.IsInstanceOfType(((CommandItem)firstMore).Command, typeof(SaveCommand));
    }

    [TestMethod]
    public void PrimaryIsSave_WhenCloseOnEnterFalse()
    {
        var settings = new Settings(closeOnEnter: false);
        TypedEventHandler<object, object> handleSave = (s, e) => { };

        var item = ResultHelper.CreateResult(
            4m,
            CultureInfo.CurrentCulture,
            CultureInfo.CurrentCulture,
            "2+2",
            settings,
            handleSave);

        Assert.IsNotNull(item);
        Assert.IsInstanceOfType(item.Command, typeof(SaveCommand));

        var firstMore = item.MoreCommands.First();
        Assert.IsInstanceOfType(firstMore, typeof(CommandContextItem));
        Assert.IsInstanceOfType(((CommandItem)firstMore).Command, typeof(CopyTextCommand));
    }
}
