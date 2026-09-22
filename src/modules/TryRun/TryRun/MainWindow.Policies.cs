// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

public partial class MainWindow
{
    private readonly Dictionary<bool, PolicyProfile> selectedPolicies = [];
    private PolicyProfileStore policyStore = new();
    private bool refreshingPolicies;

    internal PolicyProfileReference? CurrentPolicyReference
    {
        get
        {
            if (!selectedPolicies.TryGetValue(IsLinuxProfile, out var selected))
            {
                return null;
            }

            var current = CurrentPolicy();
            return PolicySettings.Fields.All(setting => current.Get(setting.Key, IsLinuxProfile) == selected.Policy.Get(setting.Key, IsLinuxProfile))
                ? new PolicyProfileReference(selected.Id, selected.Revision)
                : null;
        }
    }

    internal void LoadSavedPolicy(string id, string revision)
    {
        if (cancellation is not null || showingRun)
        {
            throw new InvalidOperationException("Return to configuration before selecting a saved policy.");
        }

        var profile = policyStore.Load(id, revision);
        if (profile.Linux != IsLinuxProfile)
        {
            throw new ArgumentException("Choose a saved policy for the selected backend.");
        }

        ApplyPolicy(profile.Policy);
        selectedPolicies[IsLinuxProfile] = profile;
        RefreshSavedPolicies();
        Status = $"Loaded {profile.Name}. Review its permissions and select Run.";
    }

    internal PolicyProfile SaveCurrentPolicy(string name, bool updateSelected = false)
    {
        if (cancellation is not null || showingRun)
        {
            throw new InvalidOperationException("Return to configuration before saving a policy.");
        }

        var id = updateSelected && selectedPolicies.TryGetValue(IsLinuxProfile, out var selected) ? selected.Id : null;
        if (updateSelected && id is null)
        {
            throw new InvalidOperationException("Select a saved policy before updating it.");
        }

        if (id is not null)
        {
            // Do not replace a revision changed by another caller since it was selected.
            policyStore.Load(id, selectedPolicies[IsLinuxProfile].Revision);
        }

        var profile = policyStore.Save(name, IsLinuxProfile, CurrentPolicy(), id);
        policyDrafts[IsLinuxProfile] = profile.Policy.Clone();
        selectedPolicies[IsLinuxProfile] = profile;
        RefreshSavedPolicies();
        UpdateRunSummary();
        SetRunning(false);
        Status = $"Saved {profile.Name}. Command Palette and the CLI can reference this policy version.";
        return profile;
    }

    private void RefreshSavedPolicies()
    {
        if (SavedPolicyBox is null)
        {
            return;
        }

        refreshingPolicies = true;
        try
        {
            var choices = policyStore.List().Where(profile => profile.Linux == IsLinuxProfile).Select(profile => new SavedPolicyChoice(profile)).ToList();
            if (selectedPolicies.TryGetValue(IsLinuxProfile, out var selected) && !choices.Any(choice => choice.Profile?.Id == selected.Id && choice.Profile.Revision == selected.Revision))
            {
                // Keep the old pin visible; execution must fail if the stored revision changed.
                choices.Add(new SavedPolicyChoice(selected));
            }

            choices.Insert(0, new SavedPolicyChoice(null));
            SavedPolicyBox.ItemsSource = choices;
            SavedPolicyBox.SelectedItem = selected is null ? choices[0] : choices.First(choice => choice.Profile?.Id == selected.Id && choice.Profile.Revision == selected.Revision);
            UpdateSavedPolicyStatus();
        }
        catch (Exception exception)
        {
            SavedPolicyStatusText.Text = "Could not load saved policies: " + exception.Message;
        }
        finally
        {
            refreshingPolicies = false;
        }
    }

    private void UpdateSavedPolicyStatus()
    {
        if (SavedPolicyStatusText is null)
        {
            return;
        }

        SavedPolicyStatusText.Text = selectedPolicies.TryGetValue(IsLinuxProfile, out var profile)
            ? CurrentPolicyReference is { } reference
                ? $"{profile.Name}\nID: {reference.Id}\nRevision: {reference.Revision[..12]}\nOnly permissions are saved. Programs and inputs are chosen for each run."
                : $"Unsaved changes to {profile.Name}. Save a new policy or update the selected policy before reusing these changes."
            : "Custom permissions for this run. Save them to reuse from Command Palette or another application.";
    }

    private void OnSavedPolicyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingPolicies || SavedPolicyBox.SelectedItem is not SavedPolicyChoice choice)
        {
            return;
        }

        try
        {
            if (choice.Profile is { } profile)
            {
                LoadSavedPolicy(profile.Id, profile.Revision);
            }
            else
            {
                selectedPolicies.Remove(IsLinuxProfile);
                UpdateSavedPolicyStatus();
            }
        }
        catch (Exception exception)
        {
            RefreshSavedPolicies();
            Status = exception.Message;
        }
    }

    private void OnRefreshPolicies(object sender, RoutedEventArgs e) => RefreshSavedPolicies();

    private void OnSavePolicy(object sender, RoutedEventArgs e)
    {
        var dialog = new SavePolicyWindow(selectedPolicies.GetValueOrDefault(IsLinuxProfile)?.Name) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            SaveCurrentPolicy(dialog.PolicyName, dialog.UpdateSelected);
        }
        catch (Exception exception)
        {
            Status = "Could not save policy: " + exception.Message;
        }
    }

    private sealed record SavedPolicyChoice(PolicyProfile? Profile)
    {
        public string DisplayName => Profile is { } profile ? $"{profile.Name} · {profile.Revision[..8]}" : "Custom permissions";
    }
}
