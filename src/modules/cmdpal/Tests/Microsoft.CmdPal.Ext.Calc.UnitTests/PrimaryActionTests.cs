// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class PrimaryActionTests
{
    [TestMethod]
    public void PrimaryActionPaste_UsesPasteAsPrimaryAndCopyAsSecondary()
    {
        var settings = new Settings(primaryAction: PrimaryAction.Paste);
        TypedEventHandler<object, object> handleReplace = (_, _) => { };

        var item = ResultHelper.CreateResultForPage(
            4m,
            CultureInfo.CurrentCulture,
            CultureInfo.CurrentCulture,
            "2+2",
            settings,
            handleReplace);

        Assert.IsNotNull(item);
        Assert.IsInstanceOfType(item.Command, typeof(CalculatorPasteCommand));

        var firstMore = item.MoreCommands.OfType<CommandContextItem>().FirstOrDefault();
        Assert.IsNotNull(firstMore);
        Assert.IsInstanceOfType(((CommandItem)firstMore).Command, typeof(CalculatorCopyCommand));
    }

    [TestMethod]
    public void HistoryItemsUsePasteWhenPrimaryActionPaste()
    {
        var settings = new Settings(primaryAction: PrimaryAction.Paste);
        settings.AddHistoryItem(new HistoryItem("2+2", "4", DateTime.UtcNow));

        var page = new CalculatorListPage(settings);
        var historyItem = page.GetItems().FirstOrDefault(item => item.Title == "4");

        Assert.IsNotNull(historyItem);
        Assert.IsInstanceOfType(historyItem.Command, typeof(CalculatorPasteCommand));
    }
}
