// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#pragma warning disable CA1846, CA1863, SA1214, SA1402, SA1413, SA1513, SA1516

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Dispatching;
using MonitorPower;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class MonitorPowerViewModel : Observable
    {
        private static bool _runtimeHostStartAttempted;
        private bool _xboxControllerEnabled = true;
        private readonly DispatcherQueue _dispatcher;
        private readonly ObservableCollection<MonitorDisplayInfo> _displays = new();
        private readonly ObservableCollection<MonitorDisplayInfo> _previewDisplays = new();
        private readonly ObservableCollection<ProfileInfo> _profiles = new();
        private MonitorDisplayInfo? _selectedDisplay;
        private ProfileInfo? _selectedProfile;
        private string _statusMessage = string.Empty;
        private bool _isLoading;
        private bool _isProfilesLoading;
        private bool _hasDisplays;
        private string _controllerStatus = GetResourceString("DisplayProfiles_ControllerStatus_NotChecked", "Controller status has not been checked.");
        private bool _isApplying;
        private string _activationShortcut = "Win + Shift + P";
        private HotkeySettings _activationHotkeySettings = new(win: true, ctrl: false, alt: false, shift: true, code: 'P');
        private string _controllerShortcut = "View + A";

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

        public ObservableCollection<MonitorDisplayInfo> PreviewDisplays => _previewDisplays;

        public ObservableCollection<ProfileInfo> Profiles => _profiles;

        public MonitorDisplayInfo? SelectedDisplay
        {
            get => _selectedDisplay;
            set => Set(ref _selectedDisplay, value);
        }

        public ProfileInfo? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (Set(ref _selectedProfile, value))
                {
                    foreach (var profile in Profiles)
                    {
                        profile.IsCurrent = ReferenceEquals(profile, value);
                    }
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (Set(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatusMessage));
                }
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

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

        public bool IsApplying
        {
            get => _isApplying;
            set
            {
                if (Set(ref _isApplying, value))
                {
                    OnPropertyChanged(nameof(CanSaveProfile));
                }
            }
        }

        public bool CanSaveProfile => !IsApplying && _displays.Any(display => display.IsSelected);

        public string ActivationShortcut
        {
            get => _activationShortcut;
        }

        public HotkeySettings ActivationHotkeySettings
        {
            get => _activationHotkeySettings;
            set
            {
                if (value is null)
                {
                    return;
                }

                if (value.IsEmpty())
                {
                    var previousClearedHotkeySettings = _activationHotkeySettings;
                    var previousClearedShortcut = _activationShortcut;
                    _activationHotkeySettings = value;
                    _activationShortcut = string.Empty;
                    OnPropertyChanged(nameof(ActivationHotkeySettings));
                    OnPropertyChanged(nameof(ActivationShortcut));
                    if (!TrySaveSettings())
                    {
                        _activationHotkeySettings = previousClearedHotkeySettings;
                        _activationShortcut = previousClearedShortcut;
                        OnPropertyChanged(nameof(ActivationHotkeySettings));
                        OnPropertyChanged(nameof(ActivationShortcut));
                    }

                    return;
                }

                if (!KeyboardActivationShortcut.TryParse(
                    value.Win,
                    value.Ctrl,
                    value.Alt,
                    value.Shift,
                    value.Code,
                    out var parsed,
                    out var error))
                {
                    StatusMessage = error;
                    OnPropertyChanged(nameof(ActivationHotkeySettings));
                    return;
                }

                var previousShortcut = _activationShortcut;
                var previousHotkeySettings = _activationHotkeySettings;
                _activationHotkeySettings = value;
                _activationShortcut = parsed!.Text;
                OnPropertyChanged(nameof(ActivationHotkeySettings));
                OnPropertyChanged(nameof(ActivationShortcut));
                if (!TrySaveSettings())
                {
                    _activationHotkeySettings = previousHotkeySettings;
                    _activationShortcut = previousShortcut;
                    OnPropertyChanged(nameof(ActivationHotkeySettings));
                    OnPropertyChanged(nameof(ActivationShortcut));
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
                    StatusMessage = GetResourceString("DisplayProfiles_ControllerShortcut_Invalid", "Enter exactly two supported controller buttons, for example 'View + A'.");
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

                    OnPropertyChanged(nameof(ControllerFirstButton));
                    OnPropertyChanged(nameof(ControllerSecondButton));
                    OnPropertyChanged(nameof(HasControllerShortcut));
                }
            }
        }

        public string ControllerFirstButton => GetControllerButton(0);

        public string ControllerSecondButton => GetControllerButton(1);

        public bool HasControllerShortcut =>
            !string.IsNullOrEmpty(ControllerFirstButton) && !string.IsNullOrEmpty(ControllerSecondButton);

        public static string FormatControllerButton(string name) => name switch
        {
            "DPadUp" => "D-pad ↑",
            "DPadDown" => "D-pad ↓",
            "DPadLeft" => "D-pad ←",
            "DPadRight" => "D-pad →",
            "LeftShoulder" => "LB",
            "RightShoulder" => "RB",
            "LeftThumb" => "LS",
            "RightThumb" => "RS",
            _ => name,
        };

        public string ControllerStatus
        {
            get => _controllerStatus;
            set => Set(ref _controllerStatus, value);
        }

        private string GetControllerButton(int index)
        {
            var buttons = ControllerShortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return buttons.Length > index ? FormatControllerButton(buttons[index]) : string.Empty;
        }

        public MonitorPowerViewModel()
        {
            var initializationTimer = Stopwatch.StartNew();
            Logger.LogInfo("Monitor Power view-model initialization started.");
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            LoadSettings();
            Logger.LogInfo($"Monitor Power view-model initialization completed in {initializationTimer.ElapsedMilliseconds} ms.");
        }

        public void OnPageLoaded()
        {
            var pageLoadTimer = Stopwatch.StartNew();
            Logger.LogInfo($"Monitor Power page data initialization started. Runner PID={App.PowerToysPID}.");
            EnsureRuntimeHostStarted();
            LoadDisplays();
            LoadProfiles();
            Logger.LogInfo($"Monitor Power page initialization dispatched in {pageLoadTimer.ElapsedMilliseconds} ms.");
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
                    Logger.LogInfo("Monitor Power settings file does not exist; using defaults.");
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
                    var configuredControllerShortcut = controllerShortcut.GetString()!;
                    if (ControllerChord.TryParse(configuredControllerShortcut, out _))
                    {
                        _controllerShortcut = configuredControllerShortcut;
                    }
                    else
                    {
                        _controllerShortcut = "View + A";
                        StatusMessage = GetResourceString("DisplayProfiles_ControllerShortcut_InvalidSaved", "The saved controller chord used an unsupported button. Capture a new chord; View + A is the default.");
                    }
                }

                if (document.RootElement.TryGetProperty("activationShortcut", out var activationShortcut) &&
                    activationShortcut.ValueKind == JsonValueKind.String)
                {
                    var configuredShortcut = activationShortcut.GetString();
                    if (string.IsNullOrWhiteSpace(configuredShortcut))
                    {
                        _activationShortcut = string.Empty;
                        _activationHotkeySettings = new HotkeySettings();
                    }
                    else if (KeyboardActivationShortcut.TryParse(configuredShortcut, out var parsedShortcut, out _))
                    {
                        _activationShortcut = parsedShortcut!.Text;
                        _activationHotkeySettings = new HotkeySettings(
                            parsedShortcut.Win,
                            parsedShortcut.Control,
                            parsedShortcut.Alt,
                            parsedShortcut.Shift,
                            parsedShortcut.VirtualKey);
                    }
                    else
                    {
                        StatusMessage = GetResourceString("DisplayProfiles_ActivationShortcut_InvalidSaved", "The saved activation shortcut is invalid; the default shortcut is in use.");
                    }
                }

                Logger.LogInfo("Monitor Power settings loaded successfully.");
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                StatusMessage = FormatResourceString("DisplayProfiles_Status_LoadSettingsFailed", "Could not load Display Profiles settings: {0}", ex.Message);
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
                StatusMessage = FormatResourceString("DisplayProfiles_Status_SaveSettingsFailed", "Could not save Display Profiles settings: {0}", ex.Message);
                Logger.LogError("Could not save Monitor Power settings.", ex);
                return false;
            }
        }

        private void EnsureRuntimeHostStarted()
        {
            if (App.PowerToysPID > 0)
            {
                Logger.LogInfo($"Monitor Power runtime is managed by runner PID {App.PowerToysPID}; Settings will not start a duplicate host.");
                return;
            }

            if (_runtimeHostStartAttempted)
            {
                Logger.LogInfo("Monitor Power runtime start was already requested by this Settings process; skipping duplicate launch.");
                return;
            }

            var runtimePath = Path.Combine(AppContext.BaseDirectory, "PowerToys.MonitorPower.Runtime.exe");
            if (!File.Exists(runtimePath))
            {
                StatusMessage = GetResourceString("DisplayProfiles_Status_RuntimeMissing", "The Display Profiles runtime host is not installed beside Settings.");
                Logger.LogError($"Monitor Power runtime host was not found: {runtimePath}");
                return;
            }

            try
            {
                _runtimeHostStartAttempted = true;
                using var runtime = Process.Start(new ProcessStartInfo
                {
                    FileName = runtimePath,
                    Arguments = $"--owner-pid {Environment.ProcessId}",
                    WorkingDirectory = Path.GetDirectoryName(runtimePath)!,
                    UseShellExecute = true,
                });
                if (runtime is null)
                {
                    _runtimeHostStartAttempted = false;
                    StatusMessage = GetResourceString("DisplayProfiles_Status_RuntimeStartFailed", "Could not start the Display Profiles runtime host.");
                    Logger.LogError("Process.Start returned no process for the Monitor Power runtime host.");
                }
                else
                {
                    Logger.LogInfo($"Started Monitor Power runtime host from Settings; PID={runtime.Id}.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                _runtimeHostStartAttempted = false;
                StatusMessage = FormatResourceString("DisplayProfiles_Status_RuntimeStartFailedWithDetails", "Could not start the Display Profiles runtime host: {0}", ex.Message);
                Logger.LogError("Could not start the Monitor Power runtime host.", ex);
            }
        }

        public async Task LoadDisplaysAsync()
        {
            var loadTimer = Stopwatch.StartNew();
            var previouslySelectedTargets = GetSelectedTargets().ToHashSet();
            IsLoading = true;
            StatusMessage = GetResourceString("DisplayProfiles_Status_LoadingDisplays", "Loading display topology...");
            Logger.LogInfo("Monitor Power display topology query started.");
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
                        var sourceWidth = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                            ? sourceMode.modeInfo.sourceMode.width
                            : 0;
                        var sourceHeight = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                            ? sourceMode.modeInfo.sourceMode.height
                            : 0;
                        var targetWidth = 0u;
                        var targetHeight = 0u;
                        var targetModeInfoIndex = path.targetInfo.targetModeInfoIdx;
                        if (targetModeInfoIndex < modes.Length &&
                            modes[targetModeInfoIndex].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Target)
                        {
                            targetWidth = modes[targetModeInfoIndex].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx;
                            targetHeight = modes[targetModeInfoIndex].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy;
                        }

                        var resolutionWidth = targetWidth > 0 ? targetWidth : sourceWidth;
                        var resolutionHeight = targetHeight > 0 ? targetHeight : sourceHeight;
                        var (pixelWidth, pixelHeight) = DisplayHelpers.GetPreviewDimensions(
                            sourceWidth,
                            sourceHeight,
                            resolutionWidth,
                            resolutionHeight,
                            path.targetInfo.rotation);

                        displayInfos.Add(new MonitorDisplayInfo
                        {
                            Index = i + 1,
                            Name = name,
                            Technology = tech,
                            AdapterId = targetInfo.adapterId.ToString(),
                            TargetId = targetInfo.id,
                            SourceKey = $"{path.sourceInfo.adapterId}:{path.sourceInfo.id}",
                            IsActive = active,
                            Resolution = resolutionWidth > 0
                                ? $"{resolutionWidth}x{resolutionHeight}"
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
                            IsSelected = active && previouslySelectedTargets.Contains(
                                new DisplayHelpers.DisplayTargetId(targetInfo.adapterId, targetInfo.id)),
                            PositionX = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                                ? sourceMode.modeInfo.sourceMode.position.x
                                : 0,
                            PositionY = sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                                ? sourceMode.modeInfo.sourceMode.position.y
                                : 0,
                            PixelWidth = pixelWidth,
                            PixelHeight = pixelHeight,
                            IsInternal = DisplayHelpers.IsInternalTechnology(targetInfo.outputTechnology)
                        });
                    }

                    var primaryDisplay = displayInfos
                        .Where(display => display.IsPrimary)
                        .OrderBy(display => display.Index)
                        .FirstOrDefault();
                    if (primaryDisplay != null)
                    {
                        foreach (var display in displayInfos)
                        {
                            display.IsPrimary = ReferenceEquals(display, primaryDisplay);
                        }
                    }

                    var previewDisplays = new List<MonitorDisplayInfo>();
                    foreach (var sourceGroup in displayInfos
                        .Where(display => display.IsActive)
                        .GroupBy(display => display.SourceKey))
                    {
                        var mirroredDisplays = sourceGroup
                            .OrderByDescending(display => display.IsPrimary)
                            .ThenBy(display => display.Index)
                            .ToArray();
                        foreach (var display in mirroredDisplays)
                        {
                            display.MirroredDisplayCount = mirroredDisplays.Length;
                        }

                        previewDisplays.Add(mirroredDisplays[0]);
                    }

                    previewDisplays.AddRange(displayInfos.Where(display => !display.IsActive));
                    var activeDisplays = displayInfos.Where(display => display.IsActive).ToArray();
                    var activePreviewDisplays = previewDisplays.Where(display => display.IsActive).ToArray();
                    var inactivePreviewDisplays = previewDisplays.Where(display => !display.IsActive).ToArray();
                    if (activePreviewDisplays.Length > 0)
                    {
                        var minX = activePreviewDisplays.Min(display => display.PositionX);
                        var minY = activePreviewDisplays.Min(display => display.PositionY);
                        var maxX = activePreviewDisplays.Max(display => display.PositionX + (int)display.PixelWidth);
                        var maxY = activePreviewDisplays.Max(display => display.PositionY + (int)display.PixelHeight);
                        var scale = Math.Min(660d / Math.Max(1, maxX - minX), 205d / Math.Max(1, maxY - minY));
                        foreach (var display in activePreviewDisplays)
                        {
                            display.LayoutLeft = 20 + 4 + ((display.PositionX - minX) * scale);
                            display.LayoutTop = 12 + 4 + ((display.PositionY - minY) * scale);
                            display.LayoutWidth = Math.Max(1, (display.PixelWidth * scale) - 8);
                            display.LayoutHeight = Math.Max(1, (display.PixelHeight * scale) - 8);
                        }

                        for (int i = 0; i < inactivePreviewDisplays.Length; i++)
                        {
                            inactivePreviewDisplays[i].LayoutLeft = 20 + ((i % 5) * 132);
                            inactivePreviewDisplays[i].LayoutTop = 230 + ((i / 5) * 32);
                            inactivePreviewDisplays[i].LayoutWidth = 120;
                            inactivePreviewDisplays[i].LayoutHeight = 28;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < previewDisplays.Count; i++)
                        {
                            previewDisplays[i].LayoutLeft = 20 + ((i % 5) * 132);
                            previewDisplays[i].LayoutTop = 120 + ((i / 5) * 36);
                            previewDisplays[i].LayoutWidth = 120;
                            previewDisplays[i].LayoutHeight = 30;
                        }
                    }

                    if (previewDisplays.Count > 0)
                    {
                        const double previewWidth = 700;
                        const double previewHeight = 296;
                        var minLeft = previewDisplays.Min(display => display.LayoutLeft);
                        var minTop = previewDisplays.Min(display => display.LayoutTop);
                        var maxRight = previewDisplays.Max(display => display.LayoutLeft + display.LayoutWidth);
                        var maxBottom = previewDisplays.Max(display => display.LayoutTop + display.LayoutHeight);
                        var offsetX = ((previewWidth - (maxRight - minLeft)) / 2) - minLeft;
                        var offsetY = ((previewHeight - (maxBottom - minTop)) / 2) - minTop;
                        foreach (var display in previewDisplays)
                        {
                            display.LayoutLeft += offsetX;
                            display.LayoutTop += offsetY;
                        }
                    }

                    foreach (var display in previewDisplays)
                    {
                        Logger.LogInfo($"Monitor Power preview tile: target={display.Index}, source={display.SourceKey}, active={display.IsActive}, primary={display.IsPrimary}, mirrored={display.MirroredDisplayCount}, position={display.PositionX},{display.PositionY}, resolution={display.Resolution}, geometry={display.PixelWidth}x{display.PixelHeight}, tile={display.LayoutLeft:0.##},{display.LayoutTop:0.##} {display.LayoutWidth:0.##}x{display.LayoutHeight:0.##}.");
                    }

                    RunOnUiThread(() =>
                    {
                        _displays.Clear();
                        foreach (var d in displayInfos)
                        {
                            _displays.Add(d);
                        }
                        _previewDisplays.Clear();
                        foreach (var display in previewDisplays)
                        {
                            _previewDisplays.Add(display);
                        }

                        HasDisplays = displayInfos.Count > 0;
                        StatusMessage = displayInfos.Count == 0
                            ? GetResourceString("DisplayProfiles_Status_NoDisplays", "No displays are currently available.")
                            : string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_Status_DisplaysFound", "Found {0} active display(s) and {1} inactive display(s)."), activeDisplays.Length, displayInfos.Count - activeDisplays.Length);
                        Logger.LogInfo($"Monitor Power display topology query completed in {loadTimer.ElapsedMilliseconds} ms. Paths={paths.Length}, targets={displayInfos.Count}, active={activeDisplays.Length}.");
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Monitor Power display topology query failed after {loadTimer.ElapsedMilliseconds} ms.", ex);
                    RunOnUiThread(() =>
                    {
                        _displays.Clear();
                        _previewDisplays.Clear();
                        HasDisplays = false;
                        StatusMessage = FormatResourceString("DisplayProfiles_Status_LoadDisplaysFailed", "Error loading displays: {0}", ex.Message);
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
            var loadTimer = Stopwatch.StartNew();
            IsProfilesLoading = true;
            Logger.LogInfo("Monitor Power profile list query started.");
            await Task.Run(() =>
            {
                try
                {
                    var savedProfiles = DisplayHelpers.GetSavedProfiles();
                    RunOnUiThread(() =>
                    {
                        var previousProfile = SelectedProfile;
                        _profiles.Clear();
                        _profiles.Add(new ProfileInfo
                        {
                            Name = GetResourceString("DisplayProfiles_BuiltIn_AllDisplays", "All displays"),
                            BuiltInProfile = BuiltInDisplayProfile.AllDisplays,
                        });
                        _profiles.Add(new ProfileInfo
                        {
                            Name = GetResourceString("DisplayProfiles_BuiltIn_PrimaryOnly", "Primary display only"),
                            BuiltInProfile = BuiltInDisplayProfile.PrimaryDisplayOnly,
                        });
                        foreach (var (fileName, name) in savedProfiles)
                        {
                            _profiles.Add(new ProfileInfo { FileName = fileName, Name = name });
                        }

                        SelectedProfile = _profiles.FirstOrDefault(profile =>
                                previousProfile != null &&
                                profile.BuiltInProfile == previousProfile.BuiltInProfile &&
                                string.Equals(profile.FileName, previousProfile.FileName, StringComparison.Ordinal))
                            ?? _profiles[0];
                        StatusMessage = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_Status_ProfilesLoaded", "Loaded {0} saved profile(s)"), savedProfiles.Count);
                        Logger.LogInfo($"Monitor Power loaded {savedProfiles.Count} saved profile(s) in {loadTimer.ElapsedMilliseconds} ms.");
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Monitor Power profile list query failed after {loadTimer.ElapsedMilliseconds} ms.", ex);
                    RunOnUiThread(() => StatusMessage = FormatResourceString("DisplayProfiles_Status_LoadProfilesFailed", "Error loading profiles: {0}", ex.Message));
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

        public void TogglePreviewDisplaySelection(MonitorDisplayInfo display)
        {
            if (!display.IsActive)
            {
                return;
            }

            var mirroredDisplays = _displays
                .Where(candidate => candidate.IsActive && string.Equals(candidate.SourceKey, display.SourceKey, StringComparison.Ordinal))
                .ToArray();
            var select = mirroredDisplays.Any(candidate => !candidate.IsSelected);
            foreach (var mirroredDisplay in mirroredDisplays)
            {
                mirroredDisplay.IsSelected = select;
            }

            OnPropertyChanged(nameof(CanSaveProfile));
        }

        public void ClearPreviewSelection()
        {
            foreach (var display in _displays)
            {
                display.IsSelected = false;
            }

            OnPropertyChanged(nameof(CanSaveProfile));
        }

        public bool TryValidateProfileName(string name, out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_Required", "Enter a profile name.");
                return false;
            }

            if (IsBuiltInProfileName(name))
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_Reserved", "Built-in profile names are reserved.");
                return false;
            }

            var selectedTargets = GetSelectedTargets();
            if (selectedTargets.Count == 0)
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_NoDisplaysSelected", "Select at least one active display in the topology preview.");
                return false;
            }

            try
            {
                validationMessage = DisplayHelpers.GetSavedProfileConflict(name.Trim(), selectedTargets) ?? string.Empty;
                return validationMessage.Length == 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_CheckFailed", "Could not check saved profiles: {0}")
                    .Replace("{0}", ex.Message, StringComparison.Ordinal);
                Logger.LogError("Could not check for duplicate Monitor Power profiles.", ex);
                return false;
            }
        }

        public async Task SaveProfileAsync(string name)
        {
            if (!TryValidateProfileName(name, out var validationMessage))
            {
                StatusMessage = validationMessage;
                return;
            }

            var selectedTargets = GetSelectedTargets();
            IsApplying = true;
            StatusMessage = GetResourceString("DisplayProfiles_Status_SavingProfile", "Saving selected displays as a profile...");

            await Task.Run(() =>
            {
                try
                {
                    var result = DisplayHelpers.SaveNamedProfile(name.Trim(), selectedTargets);
                    RunOnUiThread(() =>
                    {
                        StatusMessage = result;
                        if (!DisplayHelpers.IsErrorResult(result))
                        {
                            LoadProfiles();
                        }
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        StatusMessage = FormatResourceString("DisplayProfiles_Status_SaveProfileFailed", "Error saving profile: {0}", ex.Message);
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
            if (IsApplying)
            {
                return;
            }

            var profile = SelectedProfile;
            if (profile == null)
            {
                StatusMessage = GetResourceString("DisplayProfiles_Status_SelectProfile", "Please select a profile");
                return;
            }

            IsApplying = true;
            StatusMessage = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_Status_ApplyingProfile", "Applying profile '{0}'..."), profile.Name);

            await Task.Run(() =>
            {
                try
                {
                    var result = profile.BuiltInProfile switch
                    {
                        BuiltInDisplayProfile.AllDisplays => DisplayHelpers.SetAllDisplays(),
                        BuiltInDisplayProfile.PrimaryDisplayOnly => DisplayHelpers.SetPrimaryDisplayOnly(
                            progress => RunOnUiThread(() => StatusMessage = progress)),
                        _ => DisplayHelpers.ApplyNamedProfile(profile.FileName, progress =>
                        {
                            RunOnUiThread(() => StatusMessage = progress);
                        }),
                    };
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
                        StatusMessage = FormatResourceString("DisplayProfiles_Status_ApplyProfileFailed", "Error applying profile: {0}", ex.Message);
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
            if (SelectedProfile == null || SelectedProfile.IsBuiltIn)
            {
                StatusMessage = GetResourceString("DisplayProfiles_Status_SelectProfileToDelete", "Please select a profile to delete");
                return;
            }

            try
            {
                DisplayHelpers.DeleteSavedProfile(SelectedProfile.FileName);
                StatusMessage = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_Status_ProfileDeleted", "Deleted profile '{0}'"), SelectedProfile.Name);
                LoadProfiles();
            }
            catch (Exception ex)
            {
                StatusMessage = FormatResourceString("DisplayProfiles_Status_DeleteProfileFailed", "Error deleting profile: {0}", ex.Message);
            }
        }

        public bool TryValidateProfileRename(ProfileInfo profile, string newName, out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_Required", "Enter a profile name.");
                return false;
            }

            if (profile.IsBuiltIn || IsBuiltInProfileName(newName))
            {
                validationMessage = GetResourceString("DisplayProfiles_ProfileName_Reserved", "Built-in profile names are reserved.");
                return false;
            }

            var trimmedName = newName.Trim();
            var duplicate = _profiles.FirstOrDefault(other =>
                !ReferenceEquals(other, profile) &&
                string.Equals(other.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                validationMessage = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_Status_DuplicateProfile", "A profile named '{0}' already exists."), duplicate.Name);
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        public async Task RenameProfileAsync(ProfileInfo profile, string newName)
        {
            if (!TryValidateProfileRename(profile, newName, out var validationMessage))
            {
                StatusMessage = validationMessage;
                return;
            }

            try
            {
                var result = await Task.Run(() => DisplayHelpers.RenameSavedProfile(profile.FileName, newName));
                StatusMessage = result;
                await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = FormatResourceString("DisplayProfiles_Status_RenameProfileFailed", "Error renaming profile: {0}", ex.Message);
            }
        }

        public async Task TestControllerAsync()
        {
            var testTimer = Stopwatch.StartNew();
            Logger.LogInfo("Monitor Power controller input test started.");
            if (!ControllerChord.TryParse(ControllerShortcut, out var chord))
            {
                ControllerStatus = GetResourceString("DisplayProfiles_ControllerTest_InvalidChord", "The configured controller chord is invalid. Capture a new chord before testing.");
                StatusMessage = ControllerStatus;
                return;
            }

            ControllerStatus = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_Looking", "Looking for a supported XInput controller. Hold {0} together to test the chord."), ControllerShortcut);
            try
            {
                var controllers = DisplayHelpers.GetConnectedControllerInputs();
                if (controllers.Count == 0)
                {
                    ControllerStatus = GetResourceString("DisplayProfiles_ControllerTest_NoneDetected", "No supported XInput controller detected. Connect an Xbox-compatible controller and try again.");
                    StatusMessage = ControllerStatus;
                    Logger.LogInfo($"Controller input test found no connected controllers in {testTimer.ElapsedMilliseconds} ms.");
                    return;
                }

                ControllerStatus = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_Detected", "Detected {0} XInput controller(s). Hold {1} at the same time within 5 seconds."), controllers.Count, ControllerShortcut);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                var individualInputDetected = false;
                while (DateTime.UtcNow < deadline)
                {
                    var current = DisplayHelpers.GetConnectedControllerInputs();
                    foreach (var controller in current)
                    {
                        if (chord.IsPressed(controller.Buttons))
                        {
                            ControllerStatus = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_Pressed", "Controller {0} detected {1} pressed together."), controller.Index, ControllerShortcut);
                            StatusMessage = ControllerStatus;
                            Logger.LogInfo($"Controller chord test detected '{ControllerShortcut}' on controller {controller.Index} after {testTimer.ElapsedMilliseconds} ms.");
                            return;
                        }

                        if (controller.Buttons != 0 && !individualInputDetected)
                        {
                            individualInputDetected = true;
                            ControllerStatus = string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_IndividualInput", "Individual button input is detected ({0}). Hold {1} together; pressing the buttons separately does not activate the selector."), ControllerChord.DescribeButtons(controller.Buttons), ControllerShortcut);
                        }
                    }

                    await Task.Delay(100);
                }

                ControllerStatus = individualInputDetected
                    ? string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_IndividualTimeout", "Individual buttons are readable, but {0} was not detected. Hold both buttons at the same time."), ControllerShortcut)
                    : string.Format(CultureInfo.CurrentCulture, GetResourceString("DisplayProfiles_ControllerTest_NoInput", "Controller detected, but no button input was received. Hold {0} at the same time."), ControllerShortcut);
                StatusMessage = ControllerStatus;
                Logger.LogInfo($"Monitor Power controller chord test timed out after {testTimer.ElapsedMilliseconds} ms. IndividualInputDetected={individualInputDetected}.");
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                ControllerStatus = GetResourceString("DisplayProfiles_Controller_Unsupported", "Controller input is unsupported on this system (XInput is unavailable).");
                StatusMessage = ControllerStatus;
                Logger.LogError("Monitor Power controller diagnostics are unavailable.", ex);
            }
        }

        public async Task RunControllerCaptureAsync(
            Action<IReadOnlyList<string>> onPressed,
            Action<string> onStatus,
            CancellationToken token)
        {
            var previousEnabledState = XboxControllerEnabled;
            try
            {
                if (previousEnabledState)
                {
                    XboxControllerEnabled = false;
                    if (XboxControllerEnabled)
                    {
                        onStatus(GetResourceString("DisplayProfiles_ControllerCapture_CouldNotPause", "Could not pause the controller listener because the setting could not be saved."));
                        return;
                    }

                    await Task.Delay(1200, token);
                }

                while (!token.IsCancellationRequested)
                {
                    var controllers = await Task.Run(DisplayHelpers.GetConnectedControllerInputs, token);
                    onStatus(controllers.Count > 0 ? string.Empty : GetResourceString("DisplayProfiles_ControllerCapture_NoneDetected", "No XInput controller detected. Connect an Xbox-compatible controller."));

                    ushort buttons = 0;
                    foreach (var controller in controllers)
                    {
                        buttons |= controller.Buttons;
                    }

                    var names = buttons == 0
                        ? Array.Empty<string>()
                        : ControllerChord.DescribeButtons(buttons).Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    onPressed(names);

                    await Task.Delay(30, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                onStatus(GetResourceString("DisplayProfiles_Controller_Unsupported", "Controller input is unsupported on this system (XInput is unavailable)."));
                Logger.LogError("Monitor Power controller chord capture is unavailable.", ex);
            }
            finally
            {
                if (XboxControllerEnabled != previousEnabledState)
                {
                    XboxControllerEnabled = previousEnabledState;
                }
            }
        }

        public void OpenDiagnosticsLog()
        {
            try
            {
                var path = DisplayHelpers.DiagnosticsLogPath;
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    throw new InvalidOperationException("The Monitor Power diagnostics log directory is unavailable.");
                }

                Directory.CreateDirectory(directory);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, string.Empty);
                }

                if (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }) is null)
                {
                    throw new InvalidOperationException("Windows did not open the Monitor Power diagnostics log.");
                }

                StatusMessage = GetResourceString("DisplayProfiles_Diagnostics_LogOpened", "Opened the Display Profiles diagnostics log.");
                Logger.LogInfo($"Opened Monitor Power diagnostics log at '{path}'.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                StatusMessage = FormatResourceString("DisplayProfiles_Diagnostics_LogOpenFailed", "Could not open the Display Profiles diagnostics log: {0}", ex.Message);
                Logger.LogError("Could not open the Monitor Power diagnostics log.", ex);
            }
        }

        private List<DisplayHelpers.DisplayTargetId> GetSelectedTargets()
        {
            return _displays
                .Where(display => display.IsActive && display.IsSelected)
                .Select(display => new DisplayHelpers.DisplayTargetId(
                    ParseLuid(display.AdapterId),
                    display.TargetId))
                .ToList();
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

        private static bool IsBuiltInProfileName(string name)
        {
            var normalizedName = name.Trim();
            return string.Equals(
                    normalizedName,
                    GetResourceString("DisplayProfiles_BuiltIn_AllDisplays", "All displays"),
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    normalizedName,
                    GetResourceString("DisplayProfiles_BuiltIn_PrimaryOnly", "Primary display only"),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string GetResourceString(string resourceKey, string fallback)
        {
            var value = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatResourceString(string resourceKey, string fallback, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, GetResourceString(resourceKey, fallback), arguments);
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

        private static string GetDisplayResourceString(string resourceKey, string fallback)
        {
            var value = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Technology { get; set; } = string.Empty;
        public string AdapterId { get; set; } = string.Empty;
        public uint TargetId { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public uint PixelWidth { get; set; }
        public uint PixelHeight { get; set; }
        public string Orientation { get; set; } = "0°";
        public bool IsPrimary { get; set; }
        public int MirroredDisplayCount { get; set; } = 1;
        public double LayoutLeft { get; set; }
        public double LayoutTop { get; set; }
        public double LayoutWidth { get; set; }
        public double LayoutHeight { get; set; }
        public double PreviewOpacity => IsActive ? 1 : 0.55;
        public string State => IsActive
            ? GetDisplayResourceString("DisplayProfiles_DisplayState_Active", "Active")
            : GetDisplayResourceString("DisplayProfiles_DisplayState_Inactive", "Inactive");
        public string PreviewStatus => IsSelected
            ? IsPrimary
                ? GetDisplayResourceString("DisplayProfiles_DisplayState_SelectedPrimary", "Selected · Primary")
                : GetDisplayResourceString("DisplayProfiles_DisplayState_Selected", "Selected")
            : IsPrimary
                ? GetDisplayResourceString("DisplayProfiles_DisplayState_Primary", "Primary")
                : MirroredDisplayCount > 1
                    ? string.Format(CultureInfo.CurrentCulture, GetDisplayResourceString("DisplayProfiles_DisplayState_Mirrored", "Mirrored x{0}"), MirroredDisplayCount)
                    : State;
        public string Details => string.Format(CultureInfo.CurrentCulture, GetDisplayResourceString("DisplayProfiles_DisplayDetails", "{0} · {1} · {2}"), Technology, Resolution, Orientation);
        public string AccessibilityDescription => string.Format(CultureInfo.CurrentCulture, GetDisplayResourceString("DisplayProfiles_DisplayAccessibility", "{0}, {1}, {2}{3}"), Name, Details, IsSelected ? GetDisplayResourceString("DisplayProfiles_DisplayState_Selected", "Selected") + ", " : string.Empty, IsPrimary ? GetDisplayResourceString("DisplayProfiles_DisplayState_Primary", "Primary") : State);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewStatus));
                OnPropertyChanged(nameof(AccessibilityDescription));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProfileInfo : INotifyPropertyChanged
    {
        public string FileName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public BuiltInDisplayProfile BuiltInProfile { get; set; }
        public bool IsBuiltIn => BuiltInProfile != BuiltInDisplayProfile.None;
        public string Description => IsBuiltIn
            ? ResourceLoaderInstance.ResourceLoader.GetString("DisplayProfiles_ProfileDescription_BuiltIn")
            : ResourceLoaderInstance.ResourceLoader.GetString("DisplayProfiles_ProfileDescription_Saved");

        private bool _isCurrent;

        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent != value)
                {
                    _isCurrent = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

#pragma warning restore CA1846, SA1214, SA1402, SA1413, SA1513, SA1516
