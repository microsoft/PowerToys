// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class ProviderLoadGuardTests
{
    private readonly List<string> _temporaryDirectories = [];

    [TestMethod]
    public void EnterAndExit_PersistAndClearGuardedBlock()
    {
        var configDirectory = CreateTempDirectory();
        var sentinelPath = GetSentinelPath(configDirectory);
        var guard = new ProviderLoadGuard(configDirectory);

        guard.Enter("Provider.Block", "Provider");

        var state = ReadState(sentinelPath);
        var entry = state["Provider.Block"] as JsonObject;

        Assert.IsNotNull(entry);
        Assert.AreEqual("Provider", entry[ExtensionLoadState.ProviderIdKey]?.GetValue<string>());
        Assert.AreEqual(true, entry[ExtensionLoadState.LoadingKey]?.GetValue<bool>());
        Assert.AreEqual(0, entry[ExtensionLoadState.CrashCountKey]?.GetValue<int>());

        guard.Exit("Provider.Block");

        Assert.IsFalse(File.Exists(sentinelPath));
    }

    [TestMethod]
    public void Constructor_DisablesProviderAfterSecondConsecutiveCrash()
    {
        var configDirectory = CreateTempDirectory();
        var sentinelPath = GetSentinelPath(configDirectory);

        WriteState(
            sentinelPath,
            new JsonObject
            {
                ["Provider.Block"] = CreateEntry("Provider", loading: true, crashCount: 1),
            });

        var guard = new ProviderLoadGuard(configDirectory);

        Assert.IsTrue(guard.IsProviderDisabled("Provider"));

        var state = ReadState(sentinelPath);
        var entry = state["Provider.Block"] as JsonObject;

        Assert.IsNotNull(entry);
        Assert.AreEqual(false, entry[ExtensionLoadState.LoadingKey]?.GetValue<bool>());
        Assert.AreEqual(2, entry[ExtensionLoadState.CrashCountKey]?.GetValue<int>());
    }

    [TestMethod]
    public void ClearProvider_RemovesDisabledStateAndOnlyMatchingEntries()
    {
        var configDirectory = CreateTempDirectory();
        var sentinelPath = GetSentinelPath(configDirectory);

        WriteState(
            sentinelPath,
            new JsonObject
            {
                ["CustomBlock"] = CreateEntry("Provider", loading: false, crashCount: 2),
                ["OtherProvider.Block"] = CreateEntry("OtherProvider", loading: false, crashCount: 2),
            });

        var guard = new ProviderLoadGuard(configDirectory);

        Assert.IsTrue(guard.IsProviderDisabled("Provider"));
        Assert.IsTrue(guard.IsProviderDisabled("OtherProvider"));

        guard.ClearProvider("Provider");

        Assert.IsFalse(guard.IsProviderDisabled("Provider"));
        Assert.IsTrue(guard.IsProviderDisabled("OtherProvider"));

        var state = ReadState(sentinelPath);
        Assert.IsFalse(state.ContainsKey("CustomBlock"));
        Assert.IsTrue(state.ContainsKey("OtherProvider.Block"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("{")]
    [DataRow("[]")]
    public void Constructor_RecoversFromInvalidSentinelContents(string invalidSentinelContents)
    {
        var configDirectory = CreateTempDirectory();
        var sentinelPath = GetSentinelPath(configDirectory);
        File.WriteAllText(sentinelPath, invalidSentinelContents);

        var guard = new ProviderLoadGuard(configDirectory);

        Assert.IsFalse(guard.IsProviderDisabled("Provider"));
        Assert.IsFalse(File.Exists(sentinelPath));

        guard.Enter("Provider.Block", "Provider");

        var state = ReadState(sentinelPath);
        Assert.IsTrue(state.ContainsKey("Provider.Block"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _temporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CmdPal.ProviderLoadGuardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);
        return directory;
    }

    private static JsonObject CreateEntry(string providerId, bool loading, int crashCount)
    {
        return new JsonObject
        {
            [ExtensionLoadState.ProviderIdKey] = providerId,
            [ExtensionLoadState.LoadingKey] = loading,
            [ExtensionLoadState.CrashCountKey] = crashCount,
        };
    }

    private static string GetSentinelPath(string configDirectory)
    {
        return Path.Combine(configDirectory, ExtensionLoadState.SentinelFileName);
    }

    private static JsonObject ReadState(string sentinelPath)
    {
        if (JsonNode.Parse(File.ReadAllText(sentinelPath)) is JsonObject state)
        {
            return state;
        }

        throw new AssertFailedException($"Sentinel state at '{sentinelPath}' was not a JSON object.");
    }

    private static void WriteState(string sentinelPath, JsonObject state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sentinelPath)!);
        File.WriteAllText(sentinelPath, state.ToJsonString());
    }
}
