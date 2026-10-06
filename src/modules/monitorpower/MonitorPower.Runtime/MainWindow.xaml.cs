// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using MonitorPower;
using Windows.System;

namespace MonitorPower.Runtime;

public sealed partial class MainWindow : Window
{
    private const ushort ControllerAButton = 0x1000;
    private const ushort ControllerBButton = 0x2000;
    private const ushort ControllerDpadUp = 0x0001;
    private const ushort ControllerDpadDown = 0x0002;
    private readonly string _topologyAtOpen;

    public MainWindow()
    {
        InitializeComponent();
        _topologyAtOpen = GetActiveTopologySignature();

        var profiles = DisplayHelpers.GetSavedProfiles()
            .Select(profile => new RuntimeProfile(profile.FileName, profile.Name))
            .ToList();
        ProfileList.ItemsSource = profiles;
        ProfileList.DisplayMemberPath = nameof(RuntimeProfile.Name);
        ProfileList.SelectedIndex = profiles.Count == 0 ? -1 : 0;
        ProfileList.Focus(FocusState.Programmatic);
        ApplyButton.IsEnabled = profiles.Count > 0;
        StatusText.Text = profiles.Count == 0
            ? "No saved profiles. Save a profile in Monitor Power Settings first."
            : "Use the arrow keys or controller D-pad to select a profile. Press A or Apply to confirm; B or Cancel to close.";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        await ApplySelectedProfileAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ProfileList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            _ = ApplySelectedProfileAsync();
            e.Handled = true;
        }
    }

    public void HandleControllerButtons(ushort buttons)
    {
        if ((buttons & ControllerBButton) != 0)
        {
            Close();
        }
        else if ((buttons & ControllerAButton) != 0)
        {
            _ = ApplySelectedProfileAsync();
        }
        else if ((buttons & ControllerDpadUp) != 0 && ProfileList.Items.Count > 0)
        {
            ProfileList.SelectedIndex = Math.Max(0, ProfileList.SelectedIndex - 1);
        }
        else if ((buttons & ControllerDpadDown) != 0 && ProfileList.Items.Count > 0)
        {
            ProfileList.SelectedIndex = Math.Min(ProfileList.Items.Count - 1, ProfileList.SelectedIndex + 1);
        }
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private async Task ApplySelectedProfileAsync()
    {
        if (ProfileList.SelectedItem is not RuntimeProfile profile)
        {
            SetStatus("Select a saved display profile first.");
            return;
        }

        ApplyButton.IsEnabled = false;
        SetStatus($"Applying '{profile.Name}'...");
        try
        {
            var result = await Task.Run(() =>
            {
                if (!string.Equals(_topologyAtOpen, GetActiveTopologySignature(), StringComparison.Ordinal))
                {
                    return null;
                }

                return DisplayHelpers.ApplyNamedProfile(
                    profile.FileName,
                    message => DispatcherQueue.TryEnqueue(() => SetStatus(message)));
            });
            if (result is null)
            {
                SetStatus("The connected display topology changed while the selector was open. Close this window and reopen it to review the current displays.");
                return;
            }

            SetStatus(result);
            if (result.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                ApplyButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Could not apply '{profile.Name}': {ex.Message}");
            ApplyButton.IsEnabled = true;
        }
    }

    private static string GetActiveTopologySignature()
    {
        var (paths, modes) = DisplayHelpers.GetActivePaths();
        return string.Join(
            "|",
            paths.Select(path =>
            {
                var modeIndex = path.sourceInfo.sourceModeInfoIdx;
                var mode = modeIndex < modes.Length ? modes[modeIndex] : default;
                var geometry = mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                    ? $"{mode.modeInfo.sourceMode.width}x{mode.modeInfo.sourceMode.height}@{mode.modeInfo.sourceMode.position.x},{mode.modeInfo.sourceMode.position.y}"
                    : "unknown";
                return $"{path.targetInfo.adapterId}:{path.targetInfo.id}:{path.targetInfo.rotation}:{geometry}";
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal));
    }

    private sealed record RuntimeProfile(string FileName, string Name);
}
