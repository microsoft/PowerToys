// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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

    [TestMethod]
    public void ValidateHistoryBuckets()
    {
        // Setup
        // (these will be checked in reverse order, so that A is the most recent)
        var items = new List<ListItemMock>
        {
            new("Command A", "Subtitle A", GivenId: "idA"), // #0  -> bucket 0
            new("Command B", "Subtitle B", GivenId: "idB"), // #1  -> bucket 0
            new("Command C", "Subtitle C", GivenId: "idC"), // #2  -> bucket 0
            new("Command D", "Subtitle D", GivenId: "idD"), // #3  -> bucket 1
            new("Command E", "Subtitle E", GivenId: "idE"), // #4  -> bucket 1
            new("Command F", "Subtitle F", GivenId: "idF"), // #5  -> bucket 1
            new("Command G", "Subtitle G", GivenId: "idG"), // #6  -> bucket 1
            new("Command H", "Subtitle H", GivenId: "idH"), // #7  -> bucket 1
            new("Command I", "Subtitle I", GivenId: "idI"), // #8  -> bucket 1
            new("Command J", "Subtitle J", GivenId: "idJ"), // #9  -> bucket 1
            new("Command K", "Subtitle K", GivenId: "idK"), // #10 -> bucket 1
            new("Command L", "Subtitle L", GivenId: "idL"), // #11 -> bucket 2
            new("Command M", "Subtitle M", GivenId: "idM"), // #12 -> bucket 2
            new("Command N", "Subtitle N", GivenId: "idN"), // #13 -> bucket 2
            new("Command O", "Subtitle O", GivenId: "idO"), // #14 -> bucket 2
        };

        for (var i = items.Count; i <= 50; i++)
        {
            items.Add(new ListItemMock($"Command #{i}", GivenId: $"id{i}"));
        }

        // Act
        var history = CreateHistory(items.Reverse<ListItemMock>().ToList());

        // Assert
        // First three items should be in the top bucket
        var weightA = history.GetCommandHistoryWeight("idA");
        var weightB = history.GetCommandHistoryWeight("idB");
        var weightC = history.GetCommandHistoryWeight("idC");

        Assert.AreEqual(weightA, weightB, "Items A and B were used in the last 3 commands");
        Assert.AreEqual(weightB, weightC, "Items B and C were used in the last 3 commands");

        // Next eight items (3-10 inclusive) should be in the second bucket
        var weightD = history.GetCommandHistoryWeight("idD");
        var weightE = history.GetCommandHistoryWeight("idE");
        var weightF = history.GetCommandHistoryWeight("idF");
        var weightG = history.GetCommandHistoryWeight("idG");
        var weightH = history.GetCommandHistoryWeight("idH");
        var weightI = history.GetCommandHistoryWeight("idI");
        var weightJ = history.GetCommandHistoryWeight("idJ");
        var weightK = history.GetCommandHistoryWeight("idK");

        Assert.AreEqual(weightD, weightE, "Items D and E were used in the last 10 commands");
        Assert.AreEqual(weightE, weightF, "Items E and F were used in the last 10 commands");
        Assert.AreEqual(weightF, weightG, "Items F and G were used in the last 10 commands");
        Assert.AreEqual(weightG, weightH, "Items G and H were used in the last 10 commands");
        Assert.AreEqual(weightH, weightI, "Items H and I were used in the last 10 commands");
        Assert.AreEqual(weightI, weightJ, "Items I and J were used in the last 10 commands");
        Assert.AreEqual(weightJ, weightK, "Items J and K were used in the last 10 commands");

        // Items up to the 15th should be in the third bucket
        var weightL = history.GetCommandHistoryWeight("idL");
        var weightM = history.GetCommandHistoryWeight("idM");
        var weightN = history.GetCommandHistoryWeight("idN");
        var weightO = history.GetCommandHistoryWeight("idO");
        var weight15 = history.GetCommandHistoryWeight("id15");
        Assert.AreEqual(weightL, weightM, "Items L and M were used in the last 15 commands");
        Assert.AreEqual(weightM, weightN, "Items M and N were used in the last 15 commands");
        Assert.AreEqual(weightN, weightO, "Items N and O were used in the last 15 commands");
        Assert.AreEqual(weightO, weight15, "Items O and 15 were used in the last 15 commands");

        // Items after that should be in the lowest buckets
        var weight0 = history.GetCommandHistoryWeight(items[0].Id);
        var weight3 = history.GetCommandHistoryWeight(items[3].Id);
        var weight11 = history.GetCommandHistoryWeight(items[11].Id);
        var weight16 = history.GetCommandHistoryWeight("id16");
        var weight20 = history.GetCommandHistoryWeight("id20");
        var weight30 = history.GetCommandHistoryWeight("id30");
        var weight40 = history.GetCommandHistoryWeight("id40");
        var weight49 = history.GetCommandHistoryWeight("id49");

        Assert.IsTrue(weight0 > weight3);
        Assert.IsTrue(weight3 > weight11);
        Assert.IsTrue(weight11 > weight16);

        Assert.AreEqual(weight16, weight20);
        Assert.AreEqual(weight20, weight30);
        Assert.IsTrue(weight30 > weight40);
        Assert.AreEqual(weight40, weight49);

        // The 50th item has fallen out of the list now
        var weight50 = history.GetCommandHistoryWeight("id50");
        Assert.AreEqual(0, weight50, "Item 50 should have fallen out of the history list");
    }
}
