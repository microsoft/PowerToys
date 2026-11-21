// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerToysFallbackResultSourceTests
{
    [TestMethod]
    public void FallbackItemsAppendFallbackSuffixToCommandIds()
    {
        var firstCommand = new NoOpCommand { Id = "com.microsoft.powertoys.first" };
        var secondCommand = new NoOpCommand { Id = "com.microsoft.powertoys.second" };

        var firstFallback = new PowerToysFallbackResultSource(firstCommand, "First", string.Empty, null, null);
        var secondFallback = new PowerToysFallbackResultSource(secondCommand, "Second", string.Empty, null, null);

        Assert.AreEqual($"{firstCommand.Id}.fallback", firstFallback.Id);
        Assert.AreEqual($"{secondCommand.Id}.fallback", secondFallback.Id);
        Assert.AreNotEqual(firstCommand.Id, firstFallback.Id);
        Assert.AreNotEqual(secondCommand.Id, secondFallback.Id);
        Assert.AreNotEqual(firstFallback.Id, secondFallback.Id);
    }

    [TestMethod]
    [DataRow("Color", true)]
    [DataRow("sample", true)]
    [DataRow("zzzzzzzz", false)]
    [DataRow("", false)]
    [DataRow("   ", false)]
    public async Task QueryPreservesFuzzyTitleAndSubtitleMatching(string query, bool expectedMatch)
    {
        var command = new NoOpCommand { Id = "color-picker", Name = "Open" };
        var source = new PowerToysFallbackResultSource(command, "Color Picker", "Pick a sample from the screen", null, null);

        var fallback = (IFallbackCommandItem3)source;
        var result = await fallback.QueryHandler.QueryAsync(new FallbackQueryArgs(query, "query-id", 1));

        Assert.HasCount(expectedMatch ? 1 : 0, result.Items);
        Assert.AreEqual(query, result.Query);
        Assert.AreEqual("query-id", result.QueryId);
        Assert.IsFalse(result.HasMoreItems);
        Assert.AreEqual("Open", command.Name);
        Assert.IsInstanceOfType<FallbackResultsItem>(source);
        Assert.AreSame(source, fallback.QueryHandler);
    }

    [TestMethod]
    public async Task QueryPreservesCommandAndPresentationWithoutMutatingEarlierSnapshots()
    {
        var command = new NoOpCommand { Id = "color-picker", Name = "Open" };
        var context = new CommandContextItem(new NoOpCommand { Id = "settings", Name = "Settings" });
        var icon = new IconInfo("\uE790");
        var source = new PowerToysFallbackResultSource(command, "Color Picker", "Pick a color", icon, [context]);

        var handler = ((IFallbackCommandItem3)source).QueryHandler;
        var first = await handler.QueryAsync(new FallbackQueryArgs("Color", "first", 1));
        var empty = await handler.QueryAsync(new FallbackQueryArgs("zzzzzzzz", "empty", 1));
        var second = await handler.QueryAsync(new FallbackQueryArgs("Picker", "second", 1));

        var item = first.Items[0];
        Assert.AreSame(command, item.Command);
        Assert.AreSame(icon, item.Icon);
        Assert.HasCount(1, item.MoreCommands);
        Assert.AreSame(context, item.MoreCommands[0]);
        Assert.AreEqual("Color Picker", item.Title);
        Assert.AreEqual("Pick a color", item.Subtitle);
        Assert.AreEqual("Open", item.Command.Name);
        Assert.IsEmpty(empty.Items);
        Assert.AreNotSame(item, second.Items[0]);
        Assert.AreEqual("first", first.QueryId);
        Assert.AreEqual("second", second.QueryId);
    }

    [TestMethod]
    public async Task ZeroBudgetReturnsNoMatchingCommand()
    {
        var source = new PowerToysFallbackResultSource(new NoOpCommand { Id = "module" }, "Module", string.Empty, null, null);

        var result = await ((IFallbackCommandItem3)source).QueryHandler.QueryAsync(new FallbackQueryArgs("Module", "id", 0));

        Assert.IsEmpty(result.Items);
        Assert.IsFalse(result.HasMoreItems);
    }
}
