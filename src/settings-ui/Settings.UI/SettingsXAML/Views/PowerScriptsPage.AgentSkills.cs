// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Core.AgentSkills;
using SkillTarget = PowerScripts.Core.AgentSkills.AgentSkillTarget;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptsPage
{
    private IReadOnlyList<AgentSkillStatus> _agentSkills = Array.Empty<AgentSkillStatus>();
    private bool _agentSkillBusy;
    private bool _agentSkillPickingFolder;
    private bool _updatingAgentSkillControls;

    private SkillTarget SelectedSkillTarget => (SkillTarget)AgentSkillTarget.SelectedIndex;

    private static AgentSkillManager CreateSkillManager() => new(
        Directory.Exists(Path.Combine(AppContext.BaseDirectory, "PowerScriptsAgentSkill"))
            ? Path.Combine(AppContext.BaseDirectory, "PowerScriptsAgentSkill")
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerScripts", "AgentSkill")),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "PowerScripts",
            "agent-skills.json"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private static string SkillResource(string key) =>
        Helpers.ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_Agent" + key);

    private async Task RefreshAgentSkillsAsync(bool synchronize = false) =>
        await RunSkillActionAsync(manager =>
        {
            if (synchronize)
            {
                manager.SynchronizeAutomaticUpdates();
            }
        });

    private async Task RunSkillActionAsync(Action<AgentSkillManager> action, string success = null)
    {
        if (_agentSkillBusy)
        {
            return;
        }

        _agentSkillBusy = true;
        AgentSkillControls.IsEnabled = false;
        try
        {
            _agentSkills = await Task.Run(() =>
            {
                var manager = CreateSkillManager();
                action(manager);
                return manager.GetStatuses();
            });
            UpdateAgentSkillControls();
            AgentSkillFeedback.IsOpen = success != null;
            if (success != null)
            {
                AgentSkillFeedback.Severity = InfoBarSeverity.Success;
                AgentSkillFeedback.Message = success;
            }
        }
        catch (Exception exception)
        {
            Logger.LogError("Unable to manage the PowerScripts agent skill.", exception);
            AgentSkillFeedback.Severity = InfoBarSeverity.Error;
            AgentSkillFeedback.Message = exception.Message;
            AgentSkillFeedback.IsOpen = true;
        }
        finally
        {
            _agentSkillBusy = false;
            AgentSkillControls.IsEnabled = true;
        }
    }

    private void UpdateAgentSkillControls()
    {
        var status = _agentSkills.Single(item => item.Target == SelectedSkillTarget);
        _updatingAgentSkillControls = true;
        try
        {
            var needsFolder = status.Target == SkillTarget.Custom && string.IsNullOrEmpty(status.DestinationDirectory);
            AgentSkillDestination.Description = needsFolder ? SkillResource("ChooseFolderHelp") : status.DestinationDirectory;
            AgentSkillBrowse.Visibility = status.Target == SkillTarget.Custom ? Visibility.Visible : Visibility.Collapsed;
            AgentSkillStatusText.Text = needsFolder ? SkillResource("ChooseFolderStatus") : SkillResource("State" + status.State)
                .Replace("{0}", status.InstalledVersion ?? string.Empty, StringComparison.Ordinal)
                .Replace("{1}", status.AvailableVersion, StringComparison.Ordinal);
            AgentSkillKeepUpdated.IsChecked = status.KeepUpdated;
            AgentSkillKeepUpdated.IsEnabled = !needsFolder && status.State != AgentSkillInstallState.Conflict;
            AgentSkillAction.Content = SkillResource(status.State == AgentSkillInstallState.NotInstalled ? "InstallLabel" : "UpdateLabel");
            AgentSkillAction.IsEnabled = !needsFolder && status.State is AgentSkillInstallState.NotInstalled or AgentSkillInstallState.UpdateAvailable;
        }
        finally
        {
            _updatingAgentSkillControls = false;
        }
    }

    private async void AgentSkillTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_agentSkillBusy && _agentSkills.Count != 0)
        {
            UpdateAgentSkillControls();
            AgentSkillFeedback.IsOpen = false;
            if (SelectedSkillTarget == SkillTarget.Custom)
            {
                await ChooseAgentSkillFolderAsync();
            }
        }
    }

    private async void AgentSkillAction_Click(object sender, RoutedEventArgs e)
    {
        var target = SelectedSkillTarget;
        var install = _agentSkills.Single(item => item.Target == target).State == AgentSkillInstallState.NotInstalled;
        var keepUpdated = AgentSkillKeepUpdated.IsChecked == true;
        await RunSkillActionAsync(
            manager =>
            {
                if (install)
                {
                    manager.Install(target, keepUpdated);
                }
                else
                {
                    manager.Update(target);
                }
            },
            SkillResource("InstalledHelp"));
    }

    private async void AgentSkillKeepUpdated_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _agentSkillBusy || _updatingAgentSkillControls ||
            !_agentSkills.Any(item => item.Target == SelectedSkillTarget &&
                item.State is AgentSkillInstallState.Installed or AgentSkillInstallState.UpdateAvailable or AgentSkillInstallState.Modified))
        {
            return;
        }

        var target = SelectedSkillTarget;
        var enabled = AgentSkillKeepUpdated.IsChecked == true;
        await RunSkillActionAsync(manager => manager.SetAutomaticUpdates(target, enabled));
    }

    private async void AgentSkillBrowse_Click(object sender, RoutedEventArgs e) =>
        await ChooseAgentSkillFolderAsync();

    private async Task ChooseAgentSkillFolderAsync()
    {
        if (_agentSkillBusy || _agentSkillPickingFolder)
        {
            return;
        }

        _agentSkillPickingFolder = true;
        AgentSkillBrowse.IsEnabled = false;
        try
        {
            var folder = await PickSingleFolderDialog();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                await RunSkillActionAsync(manager => manager.SetCustomDirectory(folder));
            }
        }
        catch (Exception exception)
        {
            Logger.LogError("Unable to select the PowerScripts agent skill folder.", exception);
            AgentSkillFeedback.Severity = InfoBarSeverity.Error;
            AgentSkillFeedback.Message = exception.Message;
            AgentSkillFeedback.IsOpen = true;
        }
        finally
        {
            _agentSkillPickingFolder = false;
            AgentSkillBrowse.IsEnabled = true;
        }
    }
}
