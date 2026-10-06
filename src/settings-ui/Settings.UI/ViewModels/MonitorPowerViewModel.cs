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
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using MonitorPower;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class MonitorPowerViewModel : Observable
    {
        private bool _xboxControllerEnabled = LoadXboxControllerEnabled();
        private readonly ObservableCollection<MonitorDisplayInfo> _displays = new();
        private readonly ObservableCollection<ProfileInfo> _profiles = new();
        private MonitorDisplayInfo? _selectedDisplay;
        private ProfileInfo? _selectedProfile;
        private string _newProfileName = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoading;
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
                    _xboxControllerEnabled = value;
                    SaveXboxControllerEnabled();
                    OnPropertyChanged(nameof(XboxControllerEnabled));
                    UpdateControllerPolling(value);
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

        public bool IsApplying
        {
            get => _isApplying;
            set => Set(ref _isApplying, value);
        }

        public string ActivationShortcut
        {
            get => _activationShortcut;
            set => Set(ref _activationShortcut, value);
        }

        public string ControllerShortcut
        {
            get => _controllerShortcut;
            set => Set(ref _controllerShortcut, value);
        }

        public MonitorPowerViewModel()
        {
            LoadDisplays();
            LoadProfiles();
        }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonitorPower",
            "settings.json");

        private static bool LoadXboxControllerEnabled()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return true;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                return !document.RootElement.TryGetProperty("xboxGuideViewEnabled", out var enabled) || enabled.GetBoolean();
            }
            catch
            {
                return true;
            }
        }

        private void SaveXboxControllerEnabled()
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { xboxGuideViewEnabled = _xboxControllerEnabled }));
        }

        private void UpdateControllerPolling(bool enabled)
        {
            if (enabled)
            {
                DisplayHelpers.EnableXboxGuideViewCombo();
                StatusMessage = "Controller shortcut enabled (Guide + View)";
            }
            else
            {
                DisplayHelpers.DisableXboxGuideViewCombo();
                StatusMessage = "Controller shortcut disabled";
            }
        }

        public async Task LoadDisplaysAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var (paths, modes) = DisplayHelpers.GetActivePaths();
                    var displayInfos = new List<MonitorDisplayInfo>();

                    for (int i = 0; i < paths.Length; i++)
                    {
                        var targetInfo = paths[i].targetInfo;
                        var name = DisplayHelpers.GetTargetFriendlyName(targetInfo);
                        var tech = DisplayHelpers.GetOutputTechnologyName(targetInfo.outputTechnology);
                        var modeInfo = modes.Length > paths[i].sourceInfo.sourceModeInfoIdx ? modes[paths[i].sourceInfo.sourceModeInfoIdx] : default;

                        displayInfos.Add(new MonitorDisplayInfo
                        {
                            Index = i + 1,
                            Name = name,
                            Technology = tech,
                            AdapterId = targetInfo.adapterId.ToString(),
                            TargetId = targetInfo.id,
                            IsActive = (paths[i].flags & 1) != 0,
                            Resolution = modeInfo.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx > 0
                                ? $"{modeInfo.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx}x{modeInfo.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy}"
                                : "Unknown",
                            IsInternal = DisplayHelpers.IsInternalTechnology(targetInfo.outputTechnology)
                        });
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _displays.Clear();
                        foreach (var d in displayInfos)
                        {
                            _displays.Add(d);
                        }
                        StatusMessage = $"Found {displayInfos.Count} active display(s)";
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading displays: {ex.Message}";
                }
            });
        }

        public void LoadDisplays()
        {
            _ = LoadDisplaysAsync();
        }

        public async Task LoadProfilesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var profiles = DisplayHelpers.GetSavedProfiles();
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _profiles.Clear();
                        foreach (var (fileName, name) in profiles)
                        {
                            _profiles.Add(new ProfileInfo { FileName = fileName, Name = name });
                        }
                        StatusMessage = $"Loaded {profiles.Count} saved profile(s)";
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading profiles: {ex.Message}";
                }
            });
        }

        public void LoadProfiles()
        {
            _ = LoadProfilesAsync();
        }

        public async Task SaveProfileAsync()
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
                    var result = DisplayHelpers.SaveNamedProfile(NewProfileName, selectedTargets);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Error saving profile: {ex.Message}";
                    });
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsApplying = false);
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
                        System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = progress);
                    });
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = result;
                        LoadDisplays();
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Error applying profile: {ex.Message}";
                    });
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsApplying = false);
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

        public async Task TestControllerAsync()
        {
            StatusMessage = "Testing controller... Press Guide + View or move sticks";
            DisplayHelpers.EnableXboxGuideViewCombo();

            await Task.Delay(5000);

            DisplayHelpers.DisableXboxGuideViewCombo();
            StatusMessage = "Controller test completed. Check diagnostics log for details.";
        }

        public void SetCurrentAsBaseTopology()
        {
            try
            {
                DisplayHelpers.SaveState();
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
                var result = DisplayHelpers.RestoreState();
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
