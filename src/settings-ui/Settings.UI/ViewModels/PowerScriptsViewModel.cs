// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        private int _pythonModeIndex;
        private string _pythonInterpreterPath;
        private string _wslDistribution;

        public PowerScriptsViewModel(ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> sendConfigMsg)
        {
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            _generalSettingsRepository = generalSettingsRepository;
            _sendConfigMsg = sendConfigMsg;

            // The module-owned config.json override (if present) is authoritative, since the runner
            // strips PowerScripts from settings.json on launch; fall back to the settings flag.
            _isEnabled = ReadEnabledOverride() ?? generalSettingsRepository.SettingsConfig.Enabled.PowerScripts;
            _scriptsFolder = ResolveScriptsFolder();

            Scripts = new ObservableCollection<PowerScriptListItem>();
            WslDistributions = new ObservableCollection<string>();

            LoadPythonSettings();
            LoadWslDistributions();
            ReloadScripts();
        }

        public ObservableCollection<PowerScriptListItem> Scripts { get; }

        /// <summary>The WSL distributions detected via <c>wsl.exe -l -q</c>, offered when Python runs in WSL mode.</summary>
        public ObservableCollection<string> WslDistributions { get; }

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

                    // Also persist the enabled state into the module's own config.json. The runner
                    // rewrites settings.json on launch and drops entries for modules it does not host
                    // (the prototype is not yet a registered runner module), so the config.json flag is
                    // the authoritative, restart-durable gate that every surface (hotkey, context menu,
                    // Advanced Paste) honors.
                    SaveEnabledOverride(value);

                    // Prototype: wire the Explorer right-click submenu directly from Settings, so
                    // enabling/disabling PowerScripts installs/removes the context-menu entries even
                    // without a dedicated runner module.
                    RunHostShellCommand(value ? "shell-install" : "shell-uninstall");

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Python execution mode: 0 = Disabled, 1 = Windows, 2 = WSL. Persisted to the shared
        /// <c>config.json</c>'s <c>python.mode</c> so the Host runs Python PowerScripts the same way from
        /// every surface (a hotkey, the context menu, or Advanced Paste).
        /// </summary>
        public int PythonModeIndex
        {
            get => _pythonModeIndex;
            set
            {
                if (_pythonModeIndex != value)
                {
                    _pythonModeIndex = value;
                    SavePythonSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsWindowsMode));
                    OnPropertyChanged(nameof(IsWslMode));
                }
            }
        }

        public bool IsWindowsMode => _pythonModeIndex == 1;

        public bool IsWslMode => _pythonModeIndex == 2;

        /// <summary>Optional explicit Windows interpreter path; empty means auto-detect (py.exe / python.exe).</summary>
        public string PythonInterpreterPath
        {
            get => _pythonInterpreterPath;
            set
            {
                var normalized = value ?? string.Empty;
                if (_pythonInterpreterPath != normalized)
                {
                    _pythonInterpreterPath = normalized;
                    SavePythonSettings();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>The WSL distribution scripts run in; empty means the default distribution.</summary>
        public string WslDistribution
        {
            get => _wslDistribution;
            set
            {
                var normalized = value ?? string.Empty;
                if (_wslDistribution != normalized)
                {
                    _wslDistribution = normalized;
                    SavePythonSettings();
                    OnPropertyChanged();
                }
            }
        }

        public void SetPythonInterpreterPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                PythonInterpreterPath = path.Trim();
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
            var normalized = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            var config = LoadConfigNode();
            config["scriptsRoot"] = normalized;
            WriteConfigNode(config);
        }

        /// <summary>Reads the module <c>config.json</c> into a mutable node, preserving unknown keys.</summary>
        private static JsonObject LoadConfigNode()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var text = File.ReadAllText(ConfigFilePath);
                    if (JsonNode.Parse(text) is JsonObject existing)
                    {
                        return existing;
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt config is replaced rather than blocking the write.
            }

            return new JsonObject();
        }

        private static void WriteConfigNode(JsonObject config)
        {
            Directory.CreateDirectory(ModuleDirectory);
            File.WriteAllText(ConfigFilePath, config.ToJsonString(WriteJsonOptions));
        }

        /// <summary>Reads the module-owned enabled override from config.json, or null when absent.</summary>
        private static bool? ReadEnabledOverride()
        {
            try
            {
                var config = LoadConfigNode();
                if (config["enabled"] is JsonValue value && value.TryGetValue<bool>(out var enabled))
                {
                    return enabled;
                }
            }
            catch (Exception)
            {
                // A corrupt/unreadable config yields no override.
            }

            return null;
        }

        /// <summary>Persists the enabled override into config.json, preserving all other keys.</summary>
        private static void SaveEnabledOverride(bool enabled)
        {
            var config = LoadConfigNode();
            config["enabled"] = enabled;
            WriteConfigNode(config);
        }

        private void LoadPythonSettings()
        {
            _pythonModeIndex = 0;
            _pythonInterpreterPath = string.Empty;
            _wslDistribution = string.Empty;

            try
            {
                var config = LoadConfigNode();
                if (config["python"] is JsonObject python)
                {
                    var mode = python["mode"]?.GetValue<string>() ?? "disabled";
                    _pythonModeIndex = mode.ToLowerInvariant() switch
                    {
                        "windows" => 1,
                        "wsl" => 2,
                        _ => 0,
                    };

                    _pythonInterpreterPath = python["windows"]?["interpreterPath"]?.GetValue<string>() ?? string.Empty;
                    _wslDistribution = python["wsl"]?["distribution"]?.GetValue<string>() ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // Missing/corrupt config leaves Python disabled with default paths.
            }
        }

        /// <summary>
        /// Persists the Python section into <c>config.json</c>, preserving <c>scriptsRoot</c> and any
        /// timeout the Host may have written. Mode is stored as "disabled"/"windows"/"wsl".
        /// </summary>
        private void SavePythonSettings()
        {
            var config = LoadConfigNode();

            var mode = _pythonModeIndex switch
            {
                1 => "windows",
                2 => "wsl",
                _ => "disabled",
            };

            var existingTimeout = (config["python"] as JsonObject)?["timeoutSeconds"]?.GetValue<int>() ?? 30;

            config["python"] = new JsonObject
            {
                ["mode"] = mode,
                ["windows"] = new JsonObject { ["interpreterPath"] = _pythonInterpreterPath ?? string.Empty },
                ["wsl"] = new JsonObject { ["distribution"] = _wslDistribution ?? string.Empty },
                ["timeoutSeconds"] = existingTimeout,
            };

            WriteConfigNode(config);
        }

        /// <summary>Populates <see cref="WslDistributions"/> from <c>wsl.exe -l -q</c> (best-effort).</summary>
        private void LoadWslDistributions()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "-l -q",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,

                    // wsl.exe emits UTF-16LE for list output.
                    StandardOutputEncoding = Encoding.Unicode,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                foreach (var line in output.Split('\n'))
                {
                    var distro = line.Trim().Trim('\0', '\r');
                    if (!string.IsNullOrWhiteSpace(distro) && !WslDistributions.Contains(distro))
                    {
                        WslDistributions.Add(distro);
                    }
                }
            }
            catch (Exception)
            {
                // WSL not installed / unavailable: leave the list empty.
            }
        }

        private static string ResolveHostPath()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, HostExeName),
                Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
                Path.Combine(ModuleDirectory, HostExeName),
            };

            // Prototype dev fallback: when running an in-repo build, the Host isn't copied next to
            // Settings, so walk up from the base directory and probe the Host project's bin output.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                foreach (var config in new[] { "Debug", "Release" })
                {
                    var hostBin = Path.Combine(
                        dir.FullName,
                        "src",
                        "modules",
                        "PowerScripts",
                        "PowerScripts.Host",
                        "bin",
                        config);

                    if (Directory.Exists(hostBin))
                    {
                        var found = Directory
                            .EnumerateFiles(hostBin, HostExeName, SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(found))
                        {
                            candidates.Add(found);
                        }
                    }
                }

                dir = dir.Parent;
            }

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
