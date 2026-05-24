// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Tracks guarded provider blocks in the shared extension-load sentinel file so
/// callers can fail closed after repeated native crashes that bypass managed
/// exception handling.
/// </summary>
public sealed class ProviderCrashSentinel
{
    private readonly string _providerId;
    private readonly Lock _sentinelLock = new();
    private readonly HashSet<string> _completedBlocks = [];
    private readonly HashSet<string> _activeBlocks = [];

    public ProviderCrashSentinel(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        _providerId = providerId;
    }

    public bool BeginBlock(string blockSuffix)
    {
        var blockId = CreateBlockId(blockSuffix);

        lock (_sentinelLock)
        {
            if (_completedBlocks.Contains(blockId) || !_activeBlocks.Add(blockId))
            {
                return false;
            }

            UpdateState(
                state =>
                {
                    var entry = GetOrCreateEntry(state, blockId);
                    entry[ExtensionLoadState.LoadingKey] = true;
                });
            return true;
        }
    }

    public void CompleteBlock(string blockSuffix)
    {
        var blockId = CreateBlockId(blockSuffix);

        lock (_sentinelLock)
        {
            if (!_activeBlocks.Remove(blockId))
            {
                return;
            }

            _completedBlocks.Add(blockId);
            UpdateState(state => state.Remove(blockId));
        }
    }

    public void CancelBlock(string blockSuffix)
    {
        var blockId = CreateBlockId(blockSuffix);

        lock (_sentinelLock)
        {
            if (!_activeBlocks.Remove(blockId))
            {
                return;
            }

            UpdateState(state => state.Remove(blockId));
        }
    }

    public void ClearProviderState()
    {
        lock (_sentinelLock)
        {
            _completedBlocks.RemoveWhere(blockId => blockId.StartsWith(_providerId + ".", StringComparison.Ordinal));
            _activeBlocks.RemoveWhere(blockId => blockId.StartsWith(_providerId + ".", StringComparison.Ordinal));

            UpdateState(
                state =>
                {
                    var keysToRemove = state
                        .Where(kvp => IsProviderEntry(kvp.Key, kvp.Value as JsonObject))
                        .Select(kvp => kvp.Key)
                        .ToArray();

                    foreach (var key in keysToRemove)
                    {
                        state.Remove(key);
                    }
                });
        }
    }

    private void UpdateState(Action<JsonObject> update)
    {
        try
        {
            lock (ExtensionLoadState.SentinelFileLock)
            {
                var sentinelPath = GetSentinelPath();
                var state = LoadState(sentinelPath);
                update(state);
                SaveState(sentinelPath, state);
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"Failed to update crash sentinel state for provider '{_providerId}'.", ex);
        }
    }

    private static JsonObject LoadState(string sentinelPath)
    {
        if (!File.Exists(sentinelPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(sentinelPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                CoreLogger.LogWarning($"Crash sentinel state file '{sentinelPath}' was empty. Treating as empty state.");
                DeleteInvalidStateFile(sentinelPath);
                return [];
            }

            if (JsonNode.Parse(json) is JsonObject state)
            {
                return state;
            }

            CoreLogger.LogError($"Crash sentinel state file '{sentinelPath}' did not contain a JSON object. Treating as empty state.");
            DeleteInvalidStateFile(sentinelPath);
        }
        catch (JsonException ex)
        {
            CoreLogger.LogError($"Failed to parse crash sentinel state from '{sentinelPath}'. Treating as empty state.", ex);
            DeleteInvalidStateFile(sentinelPath);
        }
        catch (IOException ex)
        {
            CoreLogger.LogError($"Failed to read crash sentinel state from '{sentinelPath}'. Treating as empty state.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            CoreLogger.LogError($"Access denied reading crash sentinel state from '{sentinelPath}'. Treating as empty state.", ex);
        }

        return [];
    }

    private static void DeleteInvalidStateFile(string sentinelPath)
    {
        try
        {
            File.Delete(sentinelPath);
        }
        catch (IOException ex)
        {
            CoreLogger.LogError($"Failed to delete invalid crash sentinel state file '{sentinelPath}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            CoreLogger.LogError($"Access denied deleting invalid crash sentinel state file '{sentinelPath}'.", ex);
        }
    }

    private static void SaveState(string sentinelPath, JsonObject state)
    {
        if (state.Count == 0)
        {
            if (File.Exists(sentinelPath))
            {
                File.Delete(sentinelPath);
            }

            return;
        }

        var directory = Path.GetDirectoryName(sentinelPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = sentinelPath + ".tmp";
        File.WriteAllText(tempPath, state.ToJsonString());
        File.Move(tempPath, sentinelPath, overwrite: true);
    }

    private JsonObject GetOrCreateEntry(JsonObject state, string blockId)
    {
        if (state[blockId] is JsonObject existing)
        {
            existing[ExtensionLoadState.ProviderIdKey] = _providerId;
            return existing;
        }

        var entry = new JsonObject
        {
            [ExtensionLoadState.ProviderIdKey] = _providerId,
            [ExtensionLoadState.LoadingKey] = false,
            [ExtensionLoadState.CrashCountKey] = 0,
        };
        state[blockId] = entry;
        return entry;
    }

    private bool IsProviderEntry(string blockId, JsonObject? entry)
    {
        var providerId = entry?[ExtensionLoadState.ProviderIdKey]?.GetValue<string>();
        if (string.Equals(providerId, _providerId, StringComparison.Ordinal))
        {
            return true;
        }

        return blockId.StartsWith(_providerId + ".", StringComparison.Ordinal);
    }

    private string CreateBlockId(string blockSuffix)
    {
        return $"{_providerId}.{blockSuffix}";
    }

    private static string GetSentinelPath()
    {
        return Path.Combine(Utilities.BaseSettingsPath("Microsoft.CmdPal"), ExtensionLoadState.SentinelFileName);
    }
}
