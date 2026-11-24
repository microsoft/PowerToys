// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Commands;
using Microsoft.CmdPal.Ext.WindowsSettings.Pages;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsSettings.UnitTests;

[TestClass]
public class FallbackWindowsSettingsItemTests
{
    [TestMethod]
    public void FallbackPreservesIdentityAndExposesResultsHandler()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings());

        Assert.AreEqual("com.microsoft.cmdpal.builtin.windows.settings.fallback", fallback.Id);
        Assert.AreEqual(Resources.settings_title, fallback.DisplayTitle);
        Assert.IsInstanceOfType<FallbackResultsItem>(fallback);
        Assert.AreSame(fallback, ((IFallbackCommandItem3)fallback).QueryHandler);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("unmatched-query")]
    public async Task QueryWithoutMatchesReturnsEmptySnapshot(string query)
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(CreateSetting("Display")));

        var result = await QueryAsync(fallback, query);

        Assert.IsEmpty(result.Items);
        Assert.IsFalse(result.HasMoreItems);
        Assert.AreEqual(query, result.Query);
        Assert.AreEqual("query-id", result.QueryId);
    }

    [TestMethod]
    public async Task MissingCatalogReturnsEmptySnapshot()
    {
        var fallback = new FallbackWindowsSettingsItem(null);

        var result = await QueryAsync(fallback, "Display");

        Assert.IsEmpty(result.Items);
        Assert.IsFalse(result.HasMoreItems);
    }

    [TestMethod]
    public async Task QueryPreservesScoringDeduplicationAndResultPresentation()
    {
        var settings = CreateSettings(
            CreateSetting("Display options"),
            CreateSetting("Other", "ms-settings:other", "Display"),
            CreateSetting("Display"),
            CreateSetting("Duplicate", "ms-settings:Display"));
        var fallback = new FallbackWindowsSettingsItem(settings);
        var expected = new WindowsSettingsListPage(settings).Query("Display");

        var result = await QueryAsync(fallback, "Display", uint.MaxValue);

        Assert.HasCount(3, result.Items);
        for (var index = 0; index < result.Items.Length; index++)
        {
            var actual = result.Items[index];
            Assert.AreEqual(expected[index].Title, actual.Title);
            Assert.AreEqual(expected[index].Subtitle, actual.Subtitle);
            Assert.AreEqual(expected[index].Command.Id, actual.Command.Id);
            Assert.IsInstanceOfType<OpenSettingsCommand>(actual.Command);
            Assert.IsNotNull(actual.Icon);
            Assert.HasCount(1, actual.MoreCommands);
            Assert.IsInstanceOfType<CopyTextCommand>(((ICommandContextItem)actual.MoreCommands[0]).Command);
        }

        Assert.AreEqual("Display", result.Items[0].Title);
        Assert.IsFalse(result.HasMoreItems);
    }

    [TestMethod]
    public async Task QueryPreservesMmcNotes()
    {
        var setting = CreateSetting("Device Manager", "mmc.exe");
        setting.Note = "Not available on this edition";
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(setting));

        var result = await QueryAsync(fallback, "Device");

        Assert.HasCount(1, result.Items);
        StringAssert.Contains(result.Items[0].Subtitle, $"{Resources.Note}: {setting.Note}");
    }

    [TestMethod]
    public async Task QueryUsesStableDistinctDestinationIds()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(
            CreateSetting("Display", "ms-settings:display"),
            CreateSetting("Display", "ms-settings:easeofaccess-display")));

        var first = await QueryAsync(fallback, "Display", 10, "first");
        var second = await QueryAsync(fallback, "Disp", 10, "second");

        Assert.HasCount(2, first.Items);
        Assert.AreNotEqual(first.Items[0].Command.Id, first.Items[1].Command.Id);
        Assert.AreEqual(first.Items[0].Command.Id, second.Items[0].Command.Id);
        Assert.AreEqual(first.Items[1].Command.Id, second.Items[1].Command.Id);
        Assert.AreNotSame(first.Items[0], second.Items[0]);
        Assert.AreEqual("first", first.QueryId);
        Assert.AreEqual("second", second.QueryId);
    }

    [TestMethod]
    public async Task LoadMoreReturnsCumulativeSnapshotsWithoutChangingEarlierResults()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(
            CreateSetting("Display A"),
            CreateSetting("Display B"),
            CreateSetting("Display C")));

        var first = await QueryAsync(fallback, "Display", 1);
        var second = await first.LoadMoreItemsAsync(1);
        ((IDisposable)first).Dispose();
        var last = await second.LoadMoreItemsAsync(uint.MaxValue);

        Assert.HasCount(1, first.Items);
        Assert.HasCount(2, second.Items);
        Assert.HasCount(3, last.Items);
        Assert.IsTrue(first.HasMoreItems);
        Assert.IsTrue(second.HasMoreItems);
        Assert.IsFalse(last.HasMoreItems);
        Assert.AreSame(first.Items[0], last.Items[0]);
        Assert.AreEqual("Display", last.Query);
        Assert.AreEqual("query-id", last.QueryId);
    }

    [TestMethod]
    public async Task NewQueriesAndCatalogUpdatesDoNotChangePendingContinuation()
    {
        var settings = CreateSettings(CreateSetting("Display A"), CreateSetting("Display B"));
        var fallback = new FallbackWindowsSettingsItem(settings);
        var first = await QueryAsync(fallback, "Display", 1, "first");
        settings.Settings = [CreateSetting("Sound")];

        var second = await QueryAsync(fallback, "Sound", 10, "second");
        var last = await first.LoadMoreItemsAsync(10);

        Assert.HasCount(2, last.Items);
        Assert.IsTrue(last.Items.All(item => item.Title.StartsWith("Display", StringComparison.Ordinal)));
        Assert.HasCount(1, second.Items);
        Assert.AreEqual("Sound", second.Items[0].Title);
        Assert.AreEqual("first", last.QueryId);
        Assert.AreEqual("second", second.QueryId);
    }

    [TestMethod]
    public async Task ZeroBudgetReturnsNoResults()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(CreateSetting("Display")));

        var result = await QueryAsync(fallback, "Display", 0);

        Assert.IsEmpty(result.Items);
        Assert.IsFalse(result.HasMoreItems);
    }

    [TestMethod]
    public async Task ZeroLoadMoreBudgetKeepsContinuation()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(CreateSetting("Display A"), CreateSetting("Display B")));
        var first = await QueryAsync(fallback, "Display", 1);

        var unchanged = await first.LoadMoreItemsAsync(0);
        var last = await unchanged.LoadMoreItemsAsync(1);

        Assert.HasCount(1, unchanged.Items);
        Assert.IsTrue(unchanged.HasMoreItems);
        Assert.HasCount(2, last.Items);
        Assert.IsFalse(last.HasMoreItems);
    }

    [TestMethod]
    public async Task ClosingSnapshotReleasesContinuation()
    {
        var fallback = new FallbackWindowsSettingsItem(CreateSettings(CreateSetting("Display A"), CreateSetting("Display B")));
        var first = await QueryAsync(fallback, "Display", 1);
        ((IDisposable)first).Dispose();

        var repeated = await first.LoadMoreItemsAsync(10);

        Assert.HasCount(1, repeated.Items);
        Assert.IsFalse(repeated.HasMoreItems);
    }

    [TestMethod]
    public async Task QueryHonorsCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var settings = CreateSettings();
        settings.Settings = DelayedSettings();
        var fallback = new FallbackWindowsSettingsItem(settings);
        var operation = ((IFallbackCommandItem3)fallback).QueryHandler.QueryAsync(new FallbackQueryArgs("Display", "id", 1));

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            operation.Cancel();
        }
        finally
        {
            release.Set();
        }

        await Assert.ThrowsAsync<OperationCanceledException>(() => operation.AsTask());

        IEnumerable<WindowsSetting> DelayedSettings()
        {
            started.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The query was not released.");
            }

            yield return CreateSetting("Display");
        }
    }

    private static async Task<IFallbackCommandResult> QueryAsync(
        FallbackWindowsSettingsItem fallback,
        string query,
        uint count = 10,
        string queryId = "query-id") =>
        await ((IFallbackCommandItem3)fallback).QueryHandler.QueryAsync(new FallbackQueryArgs(query, queryId, count));

    private static WindowsSetting CreateSetting(string name, string command = null, string alternativeName = null) =>
        new()
        {
            Name = name,
            Command = command ?? $"ms-settings:{name}",
            Type = "Settings",
            JoinedFullSettingsPath = "Settings > System",
            AltNames = alternativeName is null ? [] : [alternativeName],
        };

    private static WindowsSettings.Classes.WindowsSettings CreateSettings(params WindowsSetting[] settings) =>
        new() { Settings = settings.ToList() };
}
