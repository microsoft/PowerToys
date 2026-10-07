// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class RecentAppHistoryCompatibilityTests
{
    [TestMethod]
    public void WithoutHistoryItem_RemovesEquivalentAppIdsAndPreservesUnrelatedEntries()
    {
        const string legacyId = "Legacy Editor_123";
        const string otherLegacyId = "Older Editor_456";
        const string ambiguousId = "Shared Editor_789";
        const string unrelatedId = "other-command";
        var app = new AppListItem(new AppItem { Name = "Editor", CatalogId = "packaged:Contoso.Editor!App" });
        var canonicalId = app.Command!.Id;
        var snapshot = new AppListItemSnapshot(
            [app],
            [],
            commandAliases: new Dictionary<string, string>
            {
                [legacyId] = canonicalId,
                [otherLegacyId] = canonicalId,
                [ambiguousId] = string.Empty,
            });
        var history = new RecentCommandsManager()
            .WithHistoryItem(legacyId)
            .WithHistoryItem(canonicalId)
            .WithHistoryItem(unrelatedId)
            .WithHistoryItem(ambiguousId)
            .WithHistoryItem(otherLegacyId);

        var updated = history.WithoutHistoryItem(canonicalId, id => snapshot.GetApp(id)?.Command?.Id ?? id);

        CollectionAssert.AreEqual(new[] { ambiguousId, unrelatedId }, updated.EnumerateRecentCommandIds().ToArray());
        Assert.AreEqual(5, history.History.Count, "Resolving and removing a displayed item must not mutate the original history.");
        foreach (var entry in updated.History)
        {
            Assert.AreSame(history.History.Single(original => original.CommandId == entry.CommandId), entry);
        }
    }

    [TestMethod]
    public void WithoutHistoryItem_UnavailableEquivalentCommandReturnsTheSameHistory()
    {
        var history = new RecentCommandsManager().WithHistoryItem("other-command");

        var updated = history.WithoutHistoryItem("missing-command", static id => id);

        Assert.AreSame(history, updated);
    }
}
