// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class TopLevelCommandResolverTests
{
    [TestMethod]
    public void Resolve_UsesPinOrderAndSkipsUnavailableOrIneligibleCommands()
    {
        var pins = new[]
        {
            new PinnedCommandSettings("provider-b", "second"),
            new PinnedCommandSettings("missing", "command"),
            new PinnedCommandSettings("provider-a", "first"),
            new PinnedCommandSettings("provider-a", "hidden"),
        };
        var commands = new[]
        {
            new TestCommand("provider-a", "first", IsEligible: true),
            new TestCommand("provider-a", "hidden", IsEligible: false),
            new TestCommand("provider-b", "second", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            pins,
            [],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible);

        CollectionAssert.AreEqual(new[] { commands[2], commands[0] }, sections.Pinned.ToArray());
        Assert.AreEqual(0, sections.Recent.Count);
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [TestMethod]
    public void Resolve_RecentCommandsFollowHistoryAndExcludePinsAndMissingCommands()
    {
        var commands = new[]
        {
            new TestCommand("provider-e", "pinned", IsEligible: true),
            new TestCommand("provider-a", "pinned", IsEligible: true),
            new TestCommand("provider-b", "older", IsEligible: true),
            new TestCommand("provider-c", "newer", IsEligible: true),
            new TestCommand("provider-d", "regular", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            [new PinnedCommandSettings("provider-a", "pinned")],
            ["pinned", "missing", "newer", "older", "regular"],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            recentCommandLimit: 2);

        CollectionAssert.AreEqual(new[] { commands[1] }, sections.Pinned.ToArray());
        CollectionAssert.AreEqual(new[] { commands[3], commands[2] }, sections.Recent.ToArray());
        CollectionAssert.AreEqual(new[] { commands[0], commands[4] }, sections.Regular.ToArray());
    }

    [TestMethod]
    public void Resolve_UsesAdditionalResolverForRecentItemsWithoutAddingThemToRegularCommands()
    {
        var regular = new TestCommand("provider-a", "regular", IsEligible: true);
        var recentApp = new TestCommand("AllApps", "recent-app", IsEligible: true);

        var sections = TopLevelCommandResolver.Resolve(
            [],
            ["missing", "recent-app"],
            [regular],
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            commandId => commandId == recentApp.CommandId ? recentApp : null);

        CollectionAssert.AreEqual(new[] { recentApp }, sections.Recent.ToArray());
        CollectionAssert.AreEqual(new[] { regular }, sections.Regular.ToArray());
    }

    [DataTestMethod]
    [DataRow(false, "Command", true)]
    [DataRow(true, "Command", false)]
    [DataRow(false, "", false)]
    [DataRow(false, null, false)]
    public void IsEligibleForHome_ExcludesFallbacksAndUntitledCommands(bool isFallback, string? title, bool expected)
    {
        Assert.AreEqual(expected, TopLevelCommandEligibility.IsEligibleForHome(isFallback, title));
    }

    private sealed record TestCommand(string ProviderId, string CommandId, bool IsEligible);
}
