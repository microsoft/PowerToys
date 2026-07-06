// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using Awake.Core.Models;
using ManagedCommon;

namespace Awake.Core
{
    /// <summary>
    /// Reads the <c>agent-status.json</c> snapshot that Intelligent Terminal (its <c>wta</c>
    /// orchestrator) writes for its live CLI agent sessions, and exposes the current agents to
    /// the flyout. This is a decoupled bridge: no COM / <c>WT_COM_CLSID</c> plumbing — Awake
    /// simply watches a well-known file. See <c>agent_sessions.rs</c> in the Intelligent
    /// Terminal repo for the source-of-truth registry.
    /// </summary>
    internal static class AgentStatusStore
    {
        // Override for tests/demo, then the dev/unpackaged path, then packaged LocalState.
        private const string EnvOverride = "AWAKE_AGENT_STATUS_FILE";
        private const string FileName = "agent-status.json";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly object SyncRoot = new();
        private static FileSystemWatcher? _watcher;
        private static string? _watchedPath;

        /// <summary>
        /// Raised (on a background thread) when the snapshot file changes. Subscribers must
        /// marshal to the UI dispatcher themselves.
        /// </summary>
        internal static event EventHandler? Changed;

        /// <summary>
        /// Resolves the snapshot path from the environment override, the dev/unpackaged
        /// Intelligent Terminal folder, or a packaged LocalState folder — first existing wins.
        /// Returns <see langword="null"/> when Intelligent Terminal is not present.
        /// </summary>
        internal static string? ResolveStatusFilePath()
        {
            string? overridePath = Environment.GetEnvironmentVariable(EnvOverride);
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string devPath = Path.Combine(localAppData, "IntelligentTerminal", FileName);
            if (File.Exists(devPath))
            {
                return devPath;
            }

            // Packaged: %LOCALAPPDATA%\Packages\<pfn>\LocalState\IntelligentTerminal\agent-status.json
            try
            {
                string packagesRoot = Path.Combine(localAppData, "Packages");
                if (Directory.Exists(packagesRoot))
                {
                    foreach (string packageDir in Directory.EnumerateDirectories(packagesRoot, "*IntelligentTerminal*"))
                    {
                        string packaged = Path.Combine(packageDir, "LocalState", "IntelligentTerminal", FileName);
                        if (File.Exists(packaged))
                        {
                            return packaged;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"AgentStatusStore: failed to probe packaged status files: {ex.Message}");
            }

            // Fall back to the dev path even if it doesn't exist yet, so the watcher can pick it
            // up once Intelligent Terminal starts writing it.
            return devPath;
        }

        internal static IReadOnlyList<AgentInfo> GetAgents()
        {
            string? path = ResolveStatusFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return Array.Empty<AgentInfo>();
            }

            AgentStatusSnapshot? snapshot = ReadSnapshot(path);
            if (snapshot?.Agents is null)
            {
                return Array.Empty<AgentInfo>();
            }

            return snapshot.Agents
                .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Id))
                .Select(ToAgentInfo)
                .Where(a => a.IsLive)
                .OrderByDescending(a => a.IsWorking)
                .ThenBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the current activity for a single agent by id, or <see cref="AgentActivity.Ended"/>
        /// when it is no longer present in the snapshot (so the monitor can release the hold).
        /// </summary>
        internal static AgentActivity GetActivity(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                return AgentActivity.Ended;
            }

            string? path = ResolveStatusFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return AgentActivity.Ended;
            }

            AgentStatusSnapshot? snapshot = ReadSnapshot(path);
            AgentStatusEntry? entry = snapshot?.Agents?.FirstOrDefault(
                a => string.Equals(a.Id, agentId, StringComparison.Ordinal));

            return entry is null ? AgentActivity.Ended : AgentInfo.ParseStatus(entry.Status);
        }

        /// <summary>
        /// Starts (or retargets) a <see cref="FileSystemWatcher"/> on the resolved snapshot folder
        /// so <see cref="Changed"/> fires as agents come, go, and change state.
        /// </summary>
        internal static void EnsureWatching()
        {
            string? path = ResolveStatusFilePath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_watcher is not null && string.Equals(_watchedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _watcher?.Dispose();
                _watcher = null;

                try
                {
                    Directory.CreateDirectory(dir);

                    var watcher = new FileSystemWatcher(dir, FileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        EnableRaisingEvents = true,
                    };

                    watcher.Changed += OnFileEvent;
                    watcher.Created += OnFileEvent;
                    watcher.Deleted += OnFileEvent;
                    watcher.Renamed += OnFileEvent;

                    _watcher = watcher;
                    _watchedPath = path;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"AgentStatusStore: failed to start watching '{dir}': {ex.Message}");
                }
            }
        }

        internal static void StopWatching()
        {
            lock (SyncRoot)
            {
                _watcher?.Dispose();
                _watcher = null;
                _watchedPath = null;
            }
        }

        private static void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static AgentStatusSnapshot? ReadSnapshot(string path)
        {
            // The writer may hold the file briefly; share read/write and retry once.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return JsonSerializer.Deserialize<AgentStatusSnapshot>(stream, SerializerOptions);
                }
                catch (IOException)
                {
                    Thread.Sleep(30);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"AgentStatusStore: failed to read/parse '{path}': {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        private static AgentInfo ToAgentInfo(AgentStatusEntry entry) => new()
        {
            Id = entry.Id ?? string.Empty,
            Source = entry.Source ?? string.Empty,
            Title = entry.Title ?? string.Empty,
            Cwd = entry.Cwd ?? string.Empty,
            Status = AgentInfo.ParseStatus(entry.Status),
            CurrentTool = entry.CurrentTool,
            AttentionReason = entry.AttentionReason,
        };

        private sealed class AgentStatusSnapshot
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("agents")]
            public List<AgentStatusEntry>? Agents { get; set; }
        }

        private sealed class AgentStatusEntry
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("cwd")]
            public string? Cwd { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("currentTool")]
            public string? CurrentTool { get; set; }

            [JsonPropertyName("attentionReason")]
            public string? AttentionReason { get; set; }
        }
    }
}
