// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Reads the shared provider crash sentinel file at startup, increments crash
/// counts for blocks left marked as active, and determines which providers
/// should be soft-disabled for the current session.
/// </summary>
public sealed class ProviderLoadGuard
{
    private const int MaxConsecutiveCrashes = 2;

    private readonly string _sentinelPath;
    private readonly HashSet<string> _disabledProviders = [];

    public ProviderLoadGuard(string configDirectory)
    {
        _sentinelPath = Path.Combine(configDirectory, ExtensionLoadState.SentinelFileName);
        DetectCrashes();
    }

    /// <summary>
    /// Returns true if the provider has been disabled due to repeated crashes
    /// in one of its tracked guarded blocks.
    /// </summary>
    public bool IsProviderDisabled(string providerId) => _disabledProviders.Contains(providerId);

    /// <summary>
    /// Call immediately before attempting a guarded operation.
    /// Marks the block as "loading" in the sentinel file so that a
    /// subsequent native crash leaves evidence on disk.
    /// </summary>
    public void Enter(string blockId, string providerId)
    {
        UpdateState(state =>
        {
            var entry = GetOrCreateEntry(state, blockId, providerId);
            entry[ExtensionLoadState.LoadingKey] = true;
        });
    }

    /// <summary>
    /// Call after a guarded operation succeeds or fails gracefully via managed
    /// exception. Clears the loading flag and removes the block entry.
    /// </summary>
    public void Exit(string blockId)
    {
        UpdateState(state => state.Remove(blockId));
    }

    /// <summary>
    /// Removes any persisted crash state for a provider so it can be retried
    /// on the next launch.
    /// </summary>
    public void ClearProvider(string providerId)
    {
        _disabledProviders.Remove(providerId);
        UpdateState(state =>
        {
            var keysToRemove = state
                .Where(kvp => TryGetProviderId(kvp.Key, kvp.Value as JsonObject) == providerId)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var key in keysToRemove)
            {
                state.Remove(key);
            }
        });
    }

    private void DetectCrashes()
    {
        // Read the sentinel file once at startup to detect providers that
        // crashed on the previous launch, then write back the updated state.
        lock (ExtensionLoadState.SentinelFileLock)
        {
            var state = ReadState();

            var keysToCheck = state.Select(kvp => kvp.Key).ToArray();

            foreach (var key in keysToCheck)
            {
                if (state[key] is not JsonObject entry)
                {
                    continue;
                }

                var providerId = TryGetProviderId(key, entry);
                var wasLoading = entry[ExtensionLoadState.LoadingKey]?.GetValue<bool>() ?? false;

                if (wasLoading)
                {
                    // The guarded block was active when the process died.
                    var crashCount = (entry[ExtensionLoadState.CrashCountKey]?.GetValue<int>() ?? 0) + 1;
                    entry[ExtensionLoadState.CrashCountKey] = crashCount;
                    entry[ExtensionLoadState.LoadingKey] = false;

                    if (crashCount >= MaxConsecutiveCrashes)
                    {
                        _disabledProviders.Add(providerId);
                        CoreLogger.LogError($"Provider '{providerId}' disabled after {crashCount} consecutive crash(es) in guarded block '{key}'.");
                    }
                    else
                    {
                        CoreLogger.LogWarning($"Guarded block '{key}' for provider '{providerId}' crashed on previous launch (crash {crashCount}/{MaxConsecutiveCrashes}). Will retry.");
                    }
                }

                var currentCrashCount = entry[ExtensionLoadState.CrashCountKey]?.GetValue<int>() ?? 0;
                if (currentCrashCount >= MaxConsecutiveCrashes)
                {
                    // Persist disabled state from a previous session.
                    _disabledProviders.Add(providerId);
                }

                if (!(entry[ExtensionLoadState.LoadingKey]?.GetValue<bool>() ?? false) && currentCrashCount == 0)
                {
                    state.Remove(key);
                }
            }

            WriteState(state);
        }
    }

    /// <summary>
    /// Reads the sentinel file, applies a mutation, and writes it back
    /// under <see cref="ExtensionLoadState.SentinelFileLock"/> to prevent
    /// concurrent writers from clobbering each other's entries.
    /// </summary>
    private void UpdateState(Action<JsonObject> mutate)
    {
        try
        {
            lock (ExtensionLoadState.SentinelFileLock)
            {
                var state = ReadState();
                mutate(state);
                WriteState(state);
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to update extension load sentinel file.", ex);
        }
    }

    private static JsonObject GetOrCreateEntry(JsonObject state, string blockId, string providerId)
    {
        if (state[blockId] is JsonObject existing)
        {
            existing[ExtensionLoadState.ProviderIdKey] = providerId;
            return existing;
        }

        var entry = new JsonObject
        {
            [ExtensionLoadState.ProviderIdKey] = providerId,
            [ExtensionLoadState.LoadingKey] = false,
            [ExtensionLoadState.CrashCountKey] = 0,
        };
        state[blockId] = entry;
        return entry;
    }

    private static string TryGetProviderId(string blockId, JsonObject? entry)
    {
        var providerId = entry?[ExtensionLoadState.ProviderIdKey]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            return providerId;
        }

        var separatorIndex = blockId.IndexOf('.');
        return separatorIndex > 0 ? blockId[..separatorIndex] : blockId;
    }

    private JsonObject ReadState()
    {
        try
        {
            if (File.Exists(_sentinelPath))
            {
                var json = File.ReadAllText(_sentinelPath);
                return JsonNode.Parse(json)?.AsObject() ?? [];
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to read extension load sentinel file.", ex);
        }

        return [];
    }

    private void WriteState(JsonObject state)
    {
        try
        {
            if (state.Count == 0)
            {
                if (File.Exists(_sentinelPath))
                {
                    File.Delete(_sentinelPath);
                }

                return;
            }

            var directory = Path.GetDirectoryName(_sentinelPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _sentinelPath + ".tmp";
            File.WriteAllText(tempPath, state.ToJsonString());
            File.Move(tempPath, _sentinelPath, overwrite: true);
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to write extension load sentinel file.", ex);
        }
    }
}
