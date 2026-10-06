// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#pragma warning disable CA1846, SA1214, SA1402, SA1413, SA1513, SA1516

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Dispatching;
using MonitorPower;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class MonitorPowerViewModel : Observable
    {
        private bool _xboxControllerEnabled = true;
        private readonly DispatcherQueue _dispatcher;
        private readonly ObservableCollection<MonitorDisplayInfo> _displays = new();
        private readonly ObservableCollection<ProfileInfo> _profiles = new();
        private MonitorDisplayInfo? _selectedDisplay;
        private ProfileInfo? _selectedProfile;
        private string _newProfileName = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoading;
        private bool _isProfilesLoading;
        private bool _hasDisplays;
        private bool _hasProfiles;
        private string _controllerStatus = "Controller status has not been checked.";
        private string _diagnosticsText = "No diagnostics loaded.";
        private bool _isApplying;
        private string _activationShortcut = "Win + Shift + P";
        private string _controllerShortcut = "Guide + View";

        public bool XboxControllerEnabled
        {
            get => _xboxControllerEnabled;
            set
            {
                if (_xboxControllerEnabled != value)
                {
                    var previousValue = _xboxControllerEnabled;
                    _xboxControllerEnabled = value;
                    if (!TrySaveSettings())
                    {
                        _xboxControllerEnabled = previousValue;
                        return;
                    }

                    OnPropertyChanged(nameof(XboxControllerEnabled));
                }
            }
        }

        public ObservableCollection<MonitorDisplayInfo> Displays => _displays;

        public ObservableCollection<ProfileInfo> Profiles => _profiles;

        public MonitorDisplayInfo? SelectedDisplay
        {
            get => _selectedDisplay;
            set => Set(ref _selectedDisplay, value);
        }

        public ProfileInfo? SelectedProfile
        {
            get => _selectedProfile;
            set => Set(ref _selectedProfile, value);
        }

        public string NewProfileName
        {
            get => _newProfileName;
            set => Set(ref _newProfileName, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public bool IsProfilesLoading
        {
            get => _isProfilesLoading;
            set => Set(ref _isProfilesLoading, value);
        }

        public bool HasDisplays
        {
            get => _hasDisplays;
            private set => Set(ref _hasDisplays, value);
        }

        public bool HasProfiles
        {
            get => _hasProfiles;
            private set => Set(ref _hasProfiles, value);
        }

        public bool IsApplying
        {
            get => _isApplying;
            set => Set(ref _isApplying, value);
        }

        public string ActivationShortcut
        {
            get => _activationShortcut;
            set
            {
                if (!KeyboardActivationShortcut.TryParse(value, out var parsed, out var error))
                {
                    StatusMessage = error;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    return;
                }

                var previousValue = _activationShortcut;
                if (Set(ref _activationShortcut, parsed!.Text))
                {
                    if (!TrySaveSettings())
                    {
                        _activationShortcut = previousValue;
                        OnPropertyChanged(nameof(ActivationShortcut));
                        return;
                    }
                }
            }
        }

        public string ControllerShortcut
        {
            get => _controllerShortcut;
            set
            {
                if (!ControllerChord.TryParse(value, out _))
                {
                    StatusMessage = "Enter exactly two supported controller buttons, for example 'Guide + View'.";
                    OnPropertyChanged(nameof(ControllerShortcut));
                    return;
                }

                var previousValue = _controllerShortcut;
                if (Set(ref _controllerShortcut, value))
                {
                    if (!TrySaveSettings())
                    {
                        _controllerShortcut = previousValue;
                        OnPropertyChanged(nameof(ControllerShortcut));
                    }
                }
            }
        }

        public string ControllerStatus
        {
            get => _controllerStatus;
            set => Set(ref _controllerStatus, value);
        }

        public string DiagnosticsText
        {
            get => _diagnosticsText;
            set => Set(ref _diagnosticsText, value);
        }

        public MonitorPowerViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            LoadSettings();
        }

        public void OnPageLoaded()
        {
            EnsureRuntimeHostStarted();
            LoadDisplays();
            LoadProfiles();
            RefreshDiagnostics();
        }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonitorPower",
            "settings.json");

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                if (document.RootElement.TryGetProperty("xboxGuideViewEnabled", out var enabled) &&
                    (enabled.ValueKind == JsonValueKind.True || enabled.ValueKind == JsonValueKind.False))
                {
                    _xboxControllerEnabled = enabled.GetBoolean();
                }

                if (document.RootElement.TryGetProperty("controllerShortcut", out var controllerShortcut) &&
                    controllerShortcut.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(controllerShortcut.GetString()))
                {
                    _controllerShortcut = controllerShortcut.GetString()!;
                }

                if (document.RootElement.TryGetProperty("activationShortcut", out var activationShortcut) &&
                    activationShortcut.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(activationShortcut.GetString()))
                {
                    var configuredShortcut = activationShortcut.GetString();
                    if (KeyboardActivationShortcut.TryParse(configuredShortcut, out var parsedShortcut, out _))
                    {
                        _activationShortcut = parsedShortcut!.Text;
                    }
                    else
                    {
                        StatusMessage = "The saved activation shortcut is invalid; the default shortcut is in use.";
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                StatusMessage = $"Could not load Monitor Power settings: {ex.Message}";
                Logger.LogError("Could not load Monitor Power settings.", ex);
            }
        }

        private void SaveSettings()
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = File.Exists(SettingsPath)
                ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject()
                : new JsonObject();
            settings["xboxGuideViewEnabled"] = _xboxControllerEnabled;
            settings["controllerShortcut"] = ControllerShortcut;
            settings["activationShortcut"] = ActivationShortcut;
            var tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, settings.ToJsonString());
            File.Move(tempPath, SettingsPath, overwrite: true);
        }

        private bool TrySaveSettings()
        {
            try
            {
                SaveSettings();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                StatusMessage = $"Could not save Monitor Power settings: {ex.Message}";
                Logger.LogError("Could not save Monitor Power settings.", ex);
                return false;
            }
        }

        private void EnsureRuntimeHostStarted()
        {
            var runtimePath = Path.Combine(AppContext.BaseDirectory, "PowerToys.MonitorPower.Runtime.exe");
            if (!File.Exists(runtimePath))
            {
                StatusMessage = "The Monitor Power runtime host is not installed beside Settings.";
                Logger.LogError($"Monitor Power runtime host was not found: {runtimePath}");
                return;
            }

            try
            {
                using var runtime = Process.Start(new ProcessStartInfo
                {
                    FileName = runtimePath,
                    WorkingDirectory = Path.GetDirectoryName(runtimePath)!,
                    UseShellExecute = true,
                });
                if (runtime is null)
                {
                    StatusMessage = "Could not start the Monitor Power runtime host.";
                    Logger.LogError("Process.Start returned no process for the Monitor Power runtime host.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                StatusMessage = $"Could not start the Monitor Power runtime host: {ex.Message}";
                Logger.LogError("Could not start the Monitor Power runtime host.", ex);
            }
        }

        public async Task LoadDisplaysAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading display topology...";
            await Task.Run(() =>
            {
                try
                {
                    var (paths, modes) = DisplayHelpers.GetAllPathsWithModes();
                    var displayInfos = new List<MonitorDisplayInfo>();
                    var topologyPaths = paths
                        .Where(path => path.targetInfo.targetAvailable != 0 || (path.flags & 1) != 0)
                        .GroupBy(path => new DisplayHelpers.DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id))
                        .Select(group => group.OrderByDescending(path => (path.flags & 1) != 0).First())
                        .OrderBy(path => path.sourceInfo.id)
                        .ThenBy(path => path.targetInfo.id)
                        .ToArray();

                    for (int i = 0; i < topologyPaths.Length; i++)
                    {
                        var path = topologyPaths[i];
                        var targetInfo = path.targetInfo;
                        var name = DisplayHelpers.GetTargetFriendlyName(targetInfo);
                        var tech = DisplayHelpers.GetOutputTechnologyName(targetInfo.outputTechnology);
                        var sourceModeInfoIndex = path.sourceInfo.sourceModeInfoIdx;
                        var sourceMode = sourceModeInfoIndex < modes.Length
                            ? modes[sourceModeInfoIndex]
                            : default;
                        var active = (path.flags & 1) != 0;
                        var width = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                            ? sourceMode.modeInfo.sourceMode.width
                            : 0;
                        var height = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                            ? sourceMode.modeInfo.sourceMode.height
                            : 0;
                        if (width == 0 || height == 0)
                        {
                            var targetModeInfoIndex = path.targetInfo.targetModeInfoIdx;
                            if (targetModeInfoIndex < modes.Length &&
                                modes[targetModeInfoIndex].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Target)
                            {
                                width = modes[targetModeInfoIndex].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx;
                                height = modes[targetModeInfoIndex].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy;
                            }
                        }

                        displayInfos.Add(new MonitorDisplayInfo
                        {
                            Index = i + 1,
                            Name = name,
                            Technology = tech,
                            AdapterId = targetInfo.adapterId.ToString(),
                            TargetId = targetInfo.id,
                            IsActive = active,
                            Resolution = width > 0
                                ? $"{width}x{height}"
                                : "Unknown",
                            Orientation = targetInfo.rotation switch
                            {
                                DISPLAYCONFIG_ROTATION.Rotate90 => "90°",
                                DISPLAYCONFIG_ROTATION.Rotate180 => "180°",
                                DISPLAYCONFIG_ROTATION.Rotate270 => "270°",
                                _ => "0°",
                            },
                            IsPrimary = active &&
                                sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source &&
                                sourceMode.modeInfo.sourceMode.position.x == 0 &&
                                sourceMode.modeInfo.sourceMode.position.y == 0,
                            PositionX = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                                ? sourceMode.modeInfo.sourceMode.position.x
                                : 0,
                            PositionY = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                                ? sourceMode.modeInfo.sourceMode.position.y
                                : 0,
                            PixelWidth = width,
                            PixelHeight = height,
                            IsInternal = DisplayHelpers.IsInternalTechnology(targetInfo.outputTechnology)
                        });
                    }

                    var activeDisplays = displayInfos.Where(display => display.IsActive).ToArray();
                    if (activeDisplays.Length > 0)
                    {
                        var minX = activeDisplays.Min(display => display.PositionX);
                        var minY = activeDisplays.Min(display => display.PositionY);
                        var maxX = activeDisplays.Max(display => display.PositionX + (int)display.PixelWidth);
                        var maxY = activeDisplays.Max(display => display.PositionY + (int)display.PixelHeight);
                        var scale = Math.Min(680d / Math.Max(1, maxX - minX), 210d / Math.Max(1, maxY - minY));
                        foreach (var display in activeDisplays)
                        {
                            display.LayoutLeft = 10 + ((display.PositionX - minX) * scale);
                            display.LayoutTop = 10 + ((display.PositionY - minY) * scale);
                            display.LayoutWidth = Math.Max(48, display.PixelWidth * scale);
                            display.LayoutHeight = Math.Max(36, display.PixelHeight * scale);
                        }

                        var inactiveLeft = 10d;
                        foreach (var display in displayInfos.Where(display => !display.IsActive))
                        {
                            display.LayoutLeft = inactiveLeft;
                            display.LayoutTop = 222;
                            display.LayoutWidth = 120;
                            display.LayoutHeight = 32;
                            inactiveLeft += 128;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < displayInfos.Count; i++)
                        {
                            displayInfos[i].LayoutLeft = 10 + (i * 128);
                            displayInfos[i].LayoutTop = 10;
                            displayInfos[i].LayoutWidth = 120;
                            displayInfos[i].LayoutHeight = 32;
                        }
                    }

                    RunOnUiThread(() =>
                    {
                        _displays.Clear();
                        foreach (var d in displayInfos)
                        {
                            _displays.Add(d);
                        }
                        HasDisplays = displayInfos.Count > 0;
                        StatusMessage = displayInfos.Count == 0
                            ? "No displays are currently available."
                            : $"Found {activeDisplays.Length} active display(s) and {displayInfos.Count - activeDisplays.Length} inactive display(s).";
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        _displays.Clear();
                        HasDisplays = false;
                        StatusMessage = $"Error loading displays: {ex.Message}";
                    });
                }
                finally
                {
                    RunOnUiThread(() => IsLoading = false);
                }
            });
        }

        public void LoadDisplays()
        {
            _ = LoadDisplaysAsync();
        }

        public async Task LoadProfilesAsync()
        {
            IsProfilesLoading = true;
            await Task.Run(() =>
            {
                try
                {
                    var profiles = DisplayHelpers.GetSavedProfiles();
                    RunOnUiThread(() =>
                    {
                        _profiles.Clear();
                        foreach (var (fileName, name) in profiles)
                        {
                            _profiles.Add(new ProfileInfo { FileName = fileName, Name = name });
                        }
                        HasProfiles = profiles.Count > 0;
                        StatusMessage = $"Loaded {profiles.Count} saved profile(s)";
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() => StatusMessage = $"Error loading profiles: {ex.Message}");
                }
                finally
                {
                    RunOnUiThread(() => IsProfilesLoading = false);
                }
            });
        }

        public void LoadProfiles()
        {
            _ = LoadProfilesAsync();
        }

        public bool ProfileExists(string name)
        {
            return DisplayHelpers.GetSavedProfiles().Any(
                profile => string.Equals(profile.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task SaveProfileAsync(bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
            {
                StatusMessage = "Please enter a profile name";
                return;
            }

            var selectedTargets = _displays.Where(d => d.IsSelected).Select(d => new DisplayHelpers.DisplayTargetId(
                ParseLuid(d.AdapterId), d.TargetId)).ToList();

            if (selectedTargets.Count == 0)
            {
                StatusMessage = "Please select at least one display";
                return;
            }

            IsApplying = true;
            StatusMessage = "Saving profile...";

            await Task.Run(() =>
            {
                try
                {
                    var result = DisplayHelpers.SaveNamedProfile(NewProfileName, selectedTargets, overwrite);
                    RunOnUiThread(() =>
                    {
                        StatusMessage = result;
                        if (!result.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                        {
                            NewProfileName = string.Empty;
                            LoadProfiles();
                            foreach (var d in _displays)
                            {
                                d.IsSelected = false;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        StatusMessage = $"Error saving profile: {ex.Message}";
                    });
                }
                finally
                {
                    RunOnUiThread(() => IsApplying = false);
                }
            });
        }

        public async Task ApplyProfileAsync()
        {
            if (SelectedProfile == null)
            {
                StatusMessage = "Please select a profile";
                return;
            }

            IsApplying = true;
            StatusMessage = $"Applying profile '{SelectedProfile.Name}'...";

            await Task.Run(() =>
            {
                try
                {
                    var result = DisplayHelpers.ApplyNamedProfile(SelectedProfile.FileName, progress =>
                    {
                        RunOnUiThread(() => StatusMessage = progress);
                    });
                    RunOnUiThread(() =>
                    {
                        StatusMessage = result;
                        LoadDisplays();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        StatusMessage = $"Error applying profile: {ex.Message}";
                    });
                }
                finally
                {
                    RunOnUiThread(() => IsApplying = false);
                }
            });
        }

        public void DeleteProfile()
        {
            if (SelectedProfile == null)
            {
                StatusMessage = "Please select a profile to delete";
                return;
            }

            try
            {
                DisplayHelpers.DeleteSavedProfile(SelectedProfile.FileName);
                StatusMessage = $"Deleted profile '{SelectedProfile.Name}'";
                LoadProfiles();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting profile: {ex.Message}";
            }
        }

        public async Task OverwriteProfileAsync(ProfileInfo profile)
        {
            var selectedTargets = GetSelectedTargets();
            if (selectedTargets.Count == 0)
            {
                StatusMessage = "Select at least one display before overwriting a profile.";
                return;
            }

            IsApplying = true;
            StatusMessage = $"Overwriting profile '{profile.Name}'...";
            try
            {
                var result = await Task.Run(() =>
                    DisplayHelpers.OverwriteNamedProfile(profile.FileName, profile.Name, selectedTargets));
                StatusMessage = result;
                await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error overwriting profile: {ex.Message}";
            }
            finally
            {
                IsApplying = false;
            }
        }

        public async Task RenameProfileAsync(ProfileInfo profile, string newName)
        {
            try
            {
                var result = await Task.Run(() => DisplayHelpers.RenameSavedProfile(profile.FileName, newName));
                StatusMessage = result;
                await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error renaming profile: {ex.Message}";
            }
        }

        public async Task DuplicateProfileAsync(ProfileInfo profile, string newName)
        {
            try
            {
                var result = await Task.Run(() => DisplayHelpers.DuplicateSavedProfile(profile.FileName, newName));
                StatusMessage = result;
                await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error duplicating profile: {ex.Message}";
            }
        }

        public async Task TestControllerAsync()
        {
            ControllerStatus = "Looking for a supported XInput controller...";
            try
            {
                var controllers = DisplayHelpers.GetConnectedControllerInputs();
                if (controllers.Count == 0)
                {
                    ControllerStatus = "No supported XInput controller detected. Connect an Xbox-compatible controller and try again.";
                    StatusMessage = ControllerStatus;
                    return;
                }

                ControllerStatus = $"Detected {controllers.Count} controller(s). Press any controller button within 5 seconds.";
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    var current = DisplayHelpers.GetConnectedControllerInputs();
                    var pressed = current.FirstOrDefault(controller => controller.Buttons != 0);
                    if (pressed.Buttons != 0)
                    {
                        ControllerStatus = $"Controller {pressed.Index} input detected (buttons 0x{pressed.Buttons:X4}).";
                        StatusMessage = ControllerStatus;
                        return;
                    }

                    await Task.Delay(100);
                }

                ControllerStatus = "Controller detected, but no button input was received.";
                StatusMessage = ControllerStatus;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                ControllerStatus = "Controller input is unsupported on this system (XInput is unavailable).";
                StatusMessage = ControllerStatus;
                Logger.LogError("Monitor Power controller diagnostics are unavailable.", ex);
            }
        }

        public void RefreshDiagnostics()
        {
            try
            {
                var diagnostics = DisplayHelpers.ReadDiagnostics();
                DiagnosticsText = string.Join(Environment.NewLine, diagnostics
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .TakeLast(100));
            }
            catch (Exception ex)
            {
                DiagnosticsText = $"Could not read diagnostics: {ex.Message}";
                Logger.LogError("Could not read Monitor Power diagnostics.", ex);
            }
        }

        private List<DisplayHelpers.DisplayTargetId> GetSelectedTargets()
        {
            return _displays
                .Where(display => display.IsSelected)
                .Select(display => new DisplayHelpers.DisplayTargetId(
                    ParseLuid(display.AdapterId),
                    display.TargetId))
                .ToList();
        }

        public void SetCurrentAsBaseTopology()
        {
            try
            {
                DisplayHelpers.SaveState();
                DisplayHelpers.SaveBaseTopology();
                StatusMessage = "Current display configuration saved as base topology (ESC to restore)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving base topology: {ex.Message}";
            }
        }

        public void RestoreBaseTopology()
        {
            try
            {
                var result = DisplayHelpers.RestoreBaseTopology();
                StatusMessage = result;
                LoadDisplays();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restoring base topology: {ex.Message}";
            }
        }

        public void SetTopologyExternalOnly()
        {
            try
            {
                var result = DisplayHelpers.SetExternalOnly();
                StatusMessage = result;
                LoadDisplays();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public void SetTopologyInternalOnly()
        {
            try
            {
                var result = DisplayHelpers.SetInternalOnly();
                StatusMessage = result;
                LoadDisplays();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public void SetTopologyClone()
        {
            try
            {
                var result = DisplayHelpers.SetClone();
                StatusMessage = result;
                LoadDisplays();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public void SetTopologyExtend()
        {
            try
            {
                var result = DisplayHelpers.SetExtend();
                StatusMessage = result;
                LoadDisplays();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private static LUID ParseLuid(string luidStr)
        {
            if (uint.TryParse(luidStr.Substring(8), System.Globalization.NumberStyles.HexNumber, null, out var lowPart) &&
                int.TryParse(luidStr.Substring(0, 8), System.Globalization.NumberStyles.HexNumber, null, out var highPart))
            {
                return new LUID { LowPart = lowPart, HighPart = highPart };
            }
            return default;
        }

        private void RunOnUiThread(Action action)
        {
            if (_dispatcher.HasThreadAccess)
            {
                action();
                return;
            }

            if (!_dispatcher.TryEnqueue(() => action()))
            {
                throw new InvalidOperationException("The Settings UI dispatcher is no longer available.");
            }
        }
    }

    public class MonitorDisplayInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Technology { get; set; } = string.Empty;
        public string AdapterId { get; set; } = string.Empty;
        public uint TargetId { get; set; }
        public bool IsActive { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public uint PixelWidth { get; set; }
        public uint PixelHeight { get; set; }
        public string Orientation { get; set; } = "0°";
        public bool IsPrimary { get; set; }
        public double LayoutLeft { get; set; }
        public double LayoutTop { get; set; }
        public double LayoutWidth { get; set; }
        public double LayoutHeight { get; set; }
        public string State => IsPrimary ? "Primary" : IsActive ? "Active" : "Inactive";
        public string AccessibilityDescription => $"{Name}, {Technology}, {Resolution}, {Orientation}, {State}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProfileInfo : INotifyPropertyChanged
    {
        public string FileName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

#pragma warning restore CA1846, SA1214, SA1402, SA1413, SA1513, SA1516
