// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Pages;
using Microsoft.CommandPalette.Extensions;
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

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FallbackItemsUseCalculatorCommandsForCopyAndPaste(bool saveFallbackResultsToHistory)
    {
        var settings = new Settings(saveFallbackResultsToHistory: saveFallbackResultsToHistory);
        var page = new CalculatorListPage(settings);
        var item = new FallbackCalculatorItem(settings, page);

        item.UpdateQuery("2+2");

        Assert.IsInstanceOfType(item.Command, typeof(CalculatorCopyCommand));
        Assert.IsInstanceOfType(GetFallbackSecondaryCommand(item), typeof(CalculatorPasteCommand));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FallbackItemsRespectPrimaryActionWhenHistorySavingToggles(bool saveFallbackResultsToHistory)
    {
        var settings = new Settings(
            primaryAction: PrimaryAction.Paste,
            saveFallbackResultsToHistory: saveFallbackResultsToHistory);
        var page = new CalculatorListPage(settings);
        var item = new FallbackCalculatorItem(settings, page);

        item.UpdateQuery("2+2");

        Assert.IsInstanceOfType(item.Command, typeof(CalculatorPasteCommand));
        Assert.IsInstanceOfType(GetFallbackSecondaryCommand(item), typeof(CalculatorCopyCommand));
    }

    private static ICommand GetFallbackSecondaryCommand(FallbackCalculatorItem item)
    {
        var secondaryCommand = item.MoreCommands
            .OfType<CommandContextItem>()
            .Skip(1)
            .Select(contextItem => ((CommandItem)contextItem).Command)
            .FirstOrDefault();

        Assert.IsNotNull(secondaryCommand);
        return secondaryCommand;
    }
}
