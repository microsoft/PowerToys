// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class RecentCommandsTests : CommandPaletteUnitTestBase
{
    private static RecentCommandsManager CreateHistory(IList<string>? commandIds = null)
    {
        var history = new RecentCommandsManager();
        if (commandIds != null)
        {
            foreach (var item in commandIds)
            {
                history.AddHistoryItem(item);
            }
        }

        return history;
    }

    private static RecentCommandsManager CreateMockHistoryServiceWithCommonCommands()
    {
        var commonCommands = new List<string>
        {
            "com.microsoft.cmdpal.shell",
            "com.microsoft.cmdpal.windowwalker",
            "Visual Studio 2022 Preview_6533433915015224980",
            "com.microsoft.cmdpal.reload",
            "com.microsoft.cmdpal.shell",
        };

        return CreateHistory(commonCommands);
    }

    [TestMethod]
    public void ValidateHistoryFunctionality()
    {
        // Setup
        var history = CreateHistory();

        // Act
        history.AddHistoryItem("com.microsoft.cmdpal.shell");

        // Assert
        Assert.IsTrue(history.GetCommandHistoryWeight("com.microsoft.cmdpal.shell") > 0);
    }

    [TestMethod]
    public void ValidateHistoryWeighting()
    {
        // Setup
        var history = CreateMockHistoryServiceWithCommonCommands();

        // Act
        var shellWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.shell");
        var windowWalkerWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.windowwalker");
        var vsWeight = history.GetCommandHistoryWeight("Visual Studio 2022 Preview_6533433915015224980");
        var reloadWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.reload");
        var nonExistentWeight = history.GetCommandHistoryWeight("non.existent.command");

        // Assert
        Assert.IsTrue(shellWeight > windowWalkerWeight, "Shell should be weighted higher than Window Walker, more uses");
        Assert.IsTrue(vsWeight > windowWalkerWeight, "Visual Studio should be weighted higher than Window Walker, because recency");
        Assert.AreEqual(reloadWeight, vsWeight, "both reload and VS were used in the last three commands, same weight");
        Assert.IsTrue(shellWeight > vsWeight, "VS and run were both used in the last 3, but shell has 2 more frequency");
        Assert.AreEqual(0, nonExistentWeight, "Non-existent command should have zero weight");
    }

    private sealed record ListItemMock(
        string Title,
        string? Subtitle = "",
        string? GivenId = "",
        string? ProviderId = "")
    {
        public string Id => string.IsNullOrEmpty(GivenId) ? GenerateId() : GivenId;

        private string GenerateId()
        {
            // Use WyHash64 to generate stable ID hashes.
            // manually seeding with 0, so that the hash is stable across launches
            var result = WyHash64.ComputeHash64(ProviderId + Title + Subtitle, seed: 0);
            return $"{ProviderId}{result}";
        }
    }

    private static RecentCommandsManager CreateHistory(IList<ListItemMock> items)
    {
        var history = new RecentCommandsManager();
        foreach (var item in items)
        {
            history.AddHistoryItem(item.Id);
        }

        return history;
    }

    [TestMethod]
    public void ValidateMocksWork()
    {
        // Setup
        var items = new List<ListItemMock>
        {
            new("Command A", "Subtitle A", "idA", "providerA"),
            new("Command B", "Subtitle B", GivenId: "idB"),
            new("Command C", "Subtitle C", ProviderId: "providerC"),
            new("Command A", "Subtitle A", "idA", "providerA"), // Duplicate to test incrementing uses
        };

        // Act
        var history = CreateHistory(items);

        // Assert
        foreach (var item in items)
        {
            var weight = history.GetCommandHistoryWeight(item.Id);
            Assert.IsTrue(weight > 0, $"Item {item.Title} should have a weight greater than zero.");
        }

        // Check that the duplicate item has a higher weight due to increased uses
        var weightA = history.GetCommandHistoryWeight("idA");
        var weightB = history.GetCommandHistoryWeight("idB");
        var weightC = history.GetCommandHistoryWeight(items[2].Id); // providerC generated ID
        Assert.IsTrue(weightA > weightB, "Item A should have a higher weight than Item B due to more uses.");
        Assert.IsTrue(weightA > weightC, "Item A should have a higher weight than Item C due to more uses.");
        Assert.AreEqual(weightC, weightB, "Item C and Item B were used in the last 3 commands");
    }
}
