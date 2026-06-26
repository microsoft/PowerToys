// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerScriptsViewModel : Observable
    {
        private const string HostExeName = "PowerScripts.Host.exe";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly JsonSerializerOptions WriteJsonOptions = new() { WriteIndented = true };

        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly Func<string, int> _sendConfigMsg;

        private bool _isEnabled;
        private string _scriptsFolder;

        public PowerScriptsViewModel(ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> sendConfigMsg)
        {
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            _generalSettingsRepository = generalSettingsRepository;
            _sendConfigMsg = sendConfigMsg;
            _isEnabled = generalSettingsRepository.SettingsConfig.Enabled.PowerScripts;
            _scriptsFolder = ResolveScriptsFolder();

            Scripts = new ObservableCollection<PowerScriptListItem>();
            ReloadScripts();
        }

        public ObservableCollection<PowerScriptListItem> Scripts { get; }

        public bool HasScripts => Scripts.Count > 0;

        /// <summary>
        /// The folder PowerScripts scans for <c>&lt;id&gt;\manifest.json</c> script folders. Persisted to
        /// the shared <c>config.json</c> so every surface (Settings, the Explorer context menu, and the
        /// Keyboard Manager mapping) resolves the same folder.
        /// </summary>
        public string ScriptsFolder
        {
            get => _scriptsFolder;
            private set
            {
                if (_scriptsFolder != value)
                {
                    _scriptsFolder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomFolder));
                }
            }
        }

        public bool IsCustomFolder =>
            !string.Equals(ScriptsFolder, DefaultScriptsFolder, StringComparison.OrdinalIgnoreCase);

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;

                    GeneralSettings generalSettings = _generalSettingsRepository.SettingsConfig;
                    generalSettings.Enabled.PowerScripts = value;

                    if (_sendConfigMsg != null)
                    {
                        var outgoing = new OutGoingGeneralSettings(generalSettings);
                        _sendConfigMsg(outgoing.ToString());
                    }

                    // Prototype: wire the Explorer right-click submenu directly from Settings, so
                    // enabling/disabling PowerScripts installs/removes the context-menu entries even
                    // without a dedicated runner module.
                    RunHostShellCommand(value ? "shell-install" : "shell-uninstall");

                    OnPropertyChanged();
                }
            }
        }

        public void ReloadScripts()
        {
            Scripts.Clear();
            foreach (var script in LoadScriptsFromHost())
            {
                Scripts.Add(script);
            }

            OnPropertyChanged(nameof(HasScripts));
        }

        /// <summary>Persists a user-chosen scripts folder and refreshes every surface that reads it.</summary>
        public void SetScriptsFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            SaveConfiguredScriptsRoot(folder.Trim());
            ScriptsFolder = ResolveScriptsFolder();
            ReloadScripts();

            // Re-register the Explorer submenu so right-click entries reflect the new folder's scripts.
            if (_isEnabled)
            {
                RunHostShellCommand("shell-install");
            }
        }

        /// <summary>Clears the override so the default folder under %LOCALAPPDATA% is used again.</summary>
        public void ResetScriptsFolder()
        {
            SaveConfiguredScriptsRoot(null);
            ScriptsFolder = ResolveScriptsFolder();
            ReloadScripts();

            if (_isEnabled)
            {
                RunHostShellCommand("shell-install");
            }
        }

        private static string ModuleDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "PowerScripts");

        private static string ConfigFilePath => Path.Combine(ModuleDirectory, "config.json");

        private static string DefaultScriptsFolder => Path.Combine(ModuleDirectory, "scripts");

        private static string ResolveScriptsFolder()
        {
            var fromEnv = Environment.GetEnvironmentVariable("POWERSCRIPTS_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv;
            }

            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    using var stream = File.OpenRead(ConfigFilePath);
                    using var document = JsonDocument.Parse(stream);
                    if (document.RootElement.TryGetProperty("scriptsRoot", out var value) &&
                        value.ValueKind == JsonValueKind.String)
                    {
                        var root = value.GetString();
                        if (!string.IsNullOrWhiteSpace(root))
                        {
                            return root;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt or unreadable config falls back to the default.
            }

            return DefaultScriptsFolder;
        }

        private static void SaveConfiguredScriptsRoot(string folder)
        {
            Directory.CreateDirectory(ModuleDirectory);
            var normalized = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            var json = JsonSerializer.Serialize(new { scriptsRoot = normalized }, WriteJsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }

        private static string ResolveHostPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, HostExeName),
                Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    "PowerScripts",
                    HostExeName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static void RunHostShellCommand(string command)
        {
            string hostPath = ResolveHostPath();
            if (string.IsNullOrEmpty(hostPath))
            {
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
            }
            catch (Exception)
            {
                // Prototype: best-effort context-menu (un)registration.
            }
        }

        private static IReadOnlyList<PowerScriptListItem> LoadScriptsFromHost()
        {
            string hostPath = ResolveHostPath();
            if (string.IsNullOrEmpty(hostPath))
            {
                return Array.Empty<PowerScriptListItem>();
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = "list --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return Array.Empty<PowerScriptListItem>();
                }

                string json = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                return JsonSerializer.Deserialize<List<PowerScriptListItem>>(json, JsonOptions)
                    ?? new List<PowerScriptListItem>();
            }
            catch (Exception)
            {
                // Prototype: a missing/failed host simply yields an empty list.
                return Array.Empty<PowerScriptListItem>();
            }
        }
    }
}
