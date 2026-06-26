// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.Telemetry;
using Microsoft.UI.Dispatching;

namespace EnvironmentVariablesUILib.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IEnvironmentVariablesService _environmentVariablesService;

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        public DefaultVariablesSet UserDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.UserGuid, ResourceLoaderInstance.ResourceLoader.GetString("User"), VariablesSetType.User);

        public DefaultVariablesSet SystemDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.SystemGuid, ResourceLoaderInstance.ResourceLoader.GetString("System"), VariablesSetType.System);

        public VariablesSet DefaultVariables { get; private set; } = new DefaultVariablesSet(Guid.NewGuid(), "DefaultVariables", VariablesSetType.User);

        [ObservableProperty]
        private ObservableCollection<ProfileVariablesSet> _profiles;

        [ObservableProperty]
        private ObservableCollection<Variable> _appliedVariables = new ObservableCollection<Variable>();

        [ObservableProperty]
        private ObservableCollection<Variable> _filteredDefaultVariables = new ObservableCollection<Variable>();

        [ObservableProperty]
        private string _defaultVariablesFilterText = string.Empty;

        [ObservableProperty]
        private bool _isElevated;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInfoBarButtonVisible))]
        private EnvironmentState _environmentState;

        public bool IsInfoBarButtonVisible => EnvironmentState == EnvironmentState.EnvironmentMessageReceived;

        public ProfileVariablesSet AppliedProfile { get; set; }

        public MainViewModel(IElevationHelper elevationHelper, IEnvironmentVariablesService environmentVariablesService, ILogger logger, ITelemetry telemetry)
        {
            _environmentVariablesService = environmentVariablesService;

            ElevationHelper.ElevationHelperInstance = elevationHelper;
            LoggerInstance.Logger = logger;
            TelemetryInstance.Telemetry = telemetry;

            var isElevated = ElevationHelper.ElevationHelperInstance.IsElevated;
            IsElevated = isElevated;
        }

        private void LoadDefaultVariables()
        {
            UserDefaultSet.Variables.Clear();
            SystemDefaultSet.Variables.Clear();
            DefaultVariables.Variables.Clear();

            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.Machine, SystemDefaultSet);
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, UserDefaultSet);

            foreach (var variable in UserDefaultSet.Variables)
            {
                if (variable == null)
                {
                    continue;
                }

                DefaultVariables.Variables.Add(variable);
                if (AppliedProfile != null && AppliedProfile.Variables != null && AppliedProfile.Variables.Any())
                {
                    if (AppliedProfile.Variables.Any(profileVariable => IsVariableAppliedToProfile(variable, profileVariable, AppliedProfile.Name)))
                    {
                        // If it's a user variable that's also in the profile or is a backup variable, mark it as applied from profile.
                        variable.IsAppliedFromProfile = true;
                    }
                }
            }

            foreach (var variable in SystemDefaultSet.Variables)
            {
                DefaultVariables.Variables.Add(variable);
            }

            ApplyDefaultVariablesFilter();
        }

        private static bool IsVariableAppliedToProfile(Variable defaultVariable, Variable profileVariable, string profileName)
        {
            if (defaultVariable == null || profileVariable == null || string.IsNullOrWhiteSpace(defaultVariable.Name))
            {
                return false;
            }

            if (string.Equals((defaultVariable.Name ?? string.Empty).Trim(), (profileVariable.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals((defaultVariable.Name ?? string.Empty).Trim(), EnvironmentVariablesHelper.GetBackupVariableName(profileVariable, profileName).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public void LoadEnvironmentVariables()
        {
            LoadDefaultVariables();
            LoadProfiles();
            PopulateAppliedVariables();
        }

        private void LoadProfiles()
        {
            try
            {
                var profiles = _environmentVariablesService?.ReadProfiles() ?? new List<ProfileVariablesSet>();
                if (profiles == null)
                {
                    profiles = new List<ProfileVariablesSet>();
                }

                var validProfiles = new List<ProfileVariablesSet>();
                var loadedProfileIds = new HashSet<Guid>();
                var loadedProfileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var profile in profiles)
                {
                    if (profile == null || profile.Id == Guid.Empty)
                    {
                        continue;
                    }

                    profile.Name = (profile.Name ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(profile.Name))
                    {
                        continue;
                    }

                    if (profile.Variables == null)
                    {
                        profile.Variables = new ObservableCollection<Variable>();
                    }

                    if (!loadedProfileIds.Add(profile.Id))
                    {
                        continue;
                    }

                    if (!loadedProfileNames.Add(profile.Name))
                    {
                        continue;
                    }

                    DeduplicateProfileVariables(profile);
                    profile.PropertyChanged += Profile_PropertyChanged;

                    foreach (var variable in profile.Variables)
                    {
                        if (variable != null)
                        {
                            variable.ParentType = VariablesSetType.Profile;
                        }
                    }

                    validProfiles.Add(profile);
                }

                var appliedProfiles = validProfiles.Where(x => x != null && x.IsEnabled).ToList();
                if (appliedProfiles.Count > 0)
                {
                    var appliedProfile = appliedProfiles.First();
                    if (appliedProfile.IsCorrectlyApplied())
                    {
                        AppliedProfile = appliedProfile;
                        EnvironmentState = EnvironmentState.Unchanged;
                    }
                    else
                    {
                        EnvironmentState = EnvironmentState.ChangedOnStartup;
                        appliedProfile.IsEnabled = false;
                    }
                }

                Profiles = new ObservableCollection<ProfileVariablesSet>(validProfiles);
            }
            catch (Exception ex)
            {
                // Show some error
                LoggerInstance.Logger.LogError("Failed to load profiles.json file", ex);

                Profiles = new ObservableCollection<ProfileVariablesSet>();
            }
        }

        private void PopulateAppliedVariables()
        {
            LoadDefaultVariables();

            var variables = new List<Variable>();
            if (AppliedProfile != null && AppliedProfile.Variables != null)
            {
                variables = variables.Concat(AppliedProfile.Variables.Where(x => x != null).Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values ?? string.Empty), VariablesSetType.Profile)).OrderBy(x => x.Name)).ToList();
            }

            // Variables are expanded to be shown in the applied variables section, so the user sees their actual values.
            variables = variables.Concat(UserDefaultSet.Variables.Where(x => x != null).Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values ?? string.Empty), VariablesSetType.User)).OrderBy(x => x.Name))
                                 .Concat(SystemDefaultSet.Variables.Where(x => x != null).Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values ?? string.Empty), VariablesSetType.System)).OrderBy(x => x.Name))
                                 .ToList();

            // Handle PATH variable - add USER value to the end of the SYSTEM value
            var profilePath = variables.Where(x => x != null && "PATH".Equals((x.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.Profile).FirstOrDefault();
            var userPath = variables.Where(x => x != null && "PATH".Equals((x.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.User).FirstOrDefault();
            var systemPath = variables.Where(x => x != null && "PATH".Equals((x.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.System).FirstOrDefault();

            if (systemPath != null)
            {
                var clone = systemPath.Clone();
                clone.ParentType = VariablesSetType.Path;

                if (userPath != null)
                {
                    clone.Values += ";" + userPath.Values;
                    variables.Remove(userPath);
                }

                if (profilePath != null)
                {
                    variables.Remove(profilePath);
                }

                variables.Insert(variables.IndexOf(systemPath), clone);
                variables.Remove(systemPath);
            }

            variables = variables.GroupBy(x => (x.Name ?? string.Empty).Trim()).Select(y => y.First()).ToList();

            // Find duplicates
            var duplicates = variables.Where(x => x != null && !string.Equals("PATH", (x.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                                     .GroupBy(x => (x.Name ?? string.Empty).Trim().ToLowerInvariant())
                                     .Where(g => g.Count() > 1);
            foreach (var duplicate in duplicates)
            {
                var userVar = duplicate.ElementAt(0);
                var systemVar = duplicate.ElementAt(1);

                var clone = userVar.Clone();
                clone.ParentType = VariablesSetType.Duplicate;
                clone.Name = systemVar.Name;
                variables.Insert(variables.IndexOf(userVar), clone);
                variables.Remove(userVar);
                variables.Remove(systemVar);
            }

            variables = variables.OrderBy(x => x.ParentType).ToList();
            AppliedVariables = new ObservableCollection<Variable>(variables);
        }

        internal void AddDefaultVariable(Variable variable, VariablesSetType type)
        {
            if (variable == null)
            {
                return;
            }

            if (type != VariablesSetType.User && type != VariablesSetType.System)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(variable.Name) || !variable.Valid)
            {
                return;
            }

            var normalizedName = (variable.Name ?? string.Empty).Trim();
            variable.Name = normalizedName;
            variable.ParentType = type;

            if (type == VariablesSetType.User)
            {
                if (UserDefaultSet.Variables != null && UserDefaultSet.Variables.Any(x => string.Equals((x?.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                UserDefaultSet.Variables.Add(variable);
                UserDefaultSet.Variables = new ObservableCollection<Variable>(UserDefaultSet.Variables.OrderBy(x => x.Name).ToList());
            }
            else if (type == VariablesSetType.System)
            {
                if (SystemDefaultSet.Variables != null && SystemDefaultSet.Variables.Any(x => string.Equals((x?.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                SystemDefaultSet.Variables.Add(variable);
                SystemDefaultSet.Variables = new ObservableCollection<Variable>(SystemDefaultSet.Variables.OrderBy(x => x.Name).ToList());
            }

            EnvironmentVariablesHelper.SetVariable(variable);
            PopulateAppliedVariables();
        }

        internal void EditVariable(Variable original, Variable edited, ProfileVariablesSet variablesSet)
        {
            if (original == null || edited == null)
            {
                return;
            }

            bool isProfileVariable = original.ParentType == VariablesSetType.Profile;
            if (isProfileVariable && variablesSet == null)
            {
                LoggerInstance.Logger.LogError("Invalid edit: cannot edit profile variable without owning profile set.");
                return;
            }

            Variable targetVariable = original;
            if (isProfileVariable)
            {
                targetVariable = ResolveProfileVariable(variablesSet?.Variables, original);
                if (targetVariable == null)
                {
                    LoggerInstance.Logger.LogError("Invalid edit: cannot resolve owning profile variable.");
                    return;
                }
            }

            bool propagateChange = !isProfileVariable || variablesSet.Id.Equals(AppliedProfile?.Id);
            var originalName = (targetVariable.Name ?? string.Empty).Trim();
            var editedName = (edited.Name ?? string.Empty).Trim();
            bool changed = !string.Equals(originalName, editedName, StringComparison.OrdinalIgnoreCase) ||
                !EnvironmentVariablesHelper.IsEquivalentVariableValue(targetVariable.Values, edited.Values);
            if (changed && string.IsNullOrEmpty(edited.Values))
            {
                DeleteVariable(targetVariable, variablesSet);
                return;
            }

            if (changed)
            {
                var task = targetVariable.Update(edited, propagateChange, variablesSet);
                task.ContinueWith(x =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        PopulateAppliedVariables();
                    });
                });

                TelemetryInstance.Telemetry.LogEnvironmentVariablesVariableChangedEvent(original.ParentType);
                _ = Task.Run(SaveAsync);
            }
        }

        internal void AddProfile(ProfileVariablesSet profile)
        {
            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                return;
            }

            profile.Name = profile.Name.Trim();

            if (profile.Variables == null)
            {
                profile.Variables = new ObservableCollection<Variable>();
            }

            if (Profiles == null)
            {
                Profiles = new ObservableCollection<ProfileVariablesSet>();
            }

            if (Profiles.Any(p => p != null && p.Id == profile.Id))
            {
                return;
            }

            if (Profiles.Any(p => p != null && string.Equals((p.Name ?? string.Empty).Trim(), profile.Name, StringComparison.OrdinalIgnoreCase) && p.Id != profile.Id))
            {
                return;
            }

            DeduplicateProfileVariables(profile);
            profile.PropertyChanged += Profile_PropertyChanged;
            if (profile.IsEnabled)
            {
                UnsetAppliedProfile();
                SetAppliedProfile(profile);
            }

            Profiles.Add(profile);

            _ = Task.Run(SaveAsync);
        }

        internal void UpdateProfile(ProfileVariablesSet updatedProfile)
        {
            if (updatedProfile == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(updatedProfile.Name))
            {
                return;
            }

            updatedProfile.Name = updatedProfile.Name.Trim();

            if (Profiles == null)
            {
                return;
            }

            var existingProfile = Profiles.Where(x => x.Id == updatedProfile.Id).FirstOrDefault();
            if (existingProfile != null)
            {
                if (updatedProfile.Variables == null)
                {
                    updatedProfile.Variables = new ObservableCollection<Variable>();
                }

                if (Profiles.Any(x => x != null && x.Id != updatedProfile.Id && string.Equals((x.Name ?? string.Empty).Trim(), updatedProfile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                DeduplicateProfileVariables(updatedProfile);
                if (updatedProfile.IsEnabled)
                {
                    // Let's unset the profile before applying the update. Even if this one is the one that's currently set.
                    UnsetAppliedProfile();
                }

                existingProfile.Name = updatedProfile.Name;
                existingProfile.IsEnabled = updatedProfile.IsEnabled;
                existingProfile.Variables = updatedProfile.Variables;
            }

            _ = Task.Run(SaveAsync);
        }

        private async Task SaveAsync()
        {
            try
            {
                await _environmentVariablesService.WriteAsync(Profiles);
            }
            catch (Exception ex)
            {
                // Show some error
                LoggerInstance.Logger.LogError("Failed to save to profiles.json file", ex);
            }
        }

        private void Profile_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var profile = sender as ProfileVariablesSet;

            if (profile != null)
            {
                if (e.PropertyName == nameof(ProfileVariablesSet.IsEnabled))
                {
                    if (profile.IsEnabled)
                    {
                        UnsetAppliedProfile();
                        SetAppliedProfile(profile);

                        TelemetryInstance.Telemetry.LogEnvironmentVariablesProfileEnabledEvent(true);
                    }
                    else
                    {
                        UnsetAppliedProfile();

                        TelemetryInstance.Telemetry.LogEnvironmentVariablesProfileEnabledEvent(false);
                    }
                }
            }

            _ = Task.Run(SaveAsync);
        }

        private void SetAppliedProfile(ProfileVariablesSet profile)
        {
            if (profile == null)
            {
                return;
            }

            if (!profile.IsApplicable())
            {
                profile.PropertyChanged -= Profile_PropertyChanged;
                profile.IsEnabled = false;
                profile.PropertyChanged += Profile_PropertyChanged;

                EnvironmentState = EnvironmentState.ProfileNotApplicable;

                return;
            }

            var task = profile.Apply();
            task.ContinueWith((a) =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    PopulateAppliedVariables();
                });
            });
            AppliedProfile = profile;
        }

        private void UnsetAppliedProfile()
        {
            if (AppliedProfile != null)
            {
                var appliedProfile = AppliedProfile;
                appliedProfile.PropertyChanged -= Profile_PropertyChanged;
                var task = AppliedProfile.UnApply();
                task.ContinueWith((a) =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        PopulateAppliedVariables();
                    });
                });
                AppliedProfile.IsEnabled = false;
                AppliedProfile = null;
                appliedProfile.PropertyChanged += Profile_PropertyChanged;
            }
        }

        private static void DeduplicateProfileVariables(ProfileVariablesSet profile)
        {
            if (profile?.Variables == null)
            {
                return;
            }

            var cleanedVariables = new List<Variable>();
            foreach (var variable in profile.Variables)
            {
                if (variable == null)
                {
                    continue;
                }

                variable.Name = variable.Name?.Trim();
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    continue;
                }

                cleanedVariables.Add(variable);
            }

            var deduped = cleanedVariables
                .GroupBy(variable => $"{variable.Name?.Trim().ToUpperInvariant()}")
                .Select(group => group.First())
                .ToList();

            if (deduped.Count != profile.Variables.Count)
            {
                profile.Variables = new ObservableCollection<Variable>(deduped);

                foreach (var variable in profile.Variables)
                {
                    variable.ParentType = VariablesSetType.Profile;
                }
            }
        }

        partial void OnDefaultVariablesFilterTextChanged(string value)
        {
            ApplyDefaultVariablesFilter();
        }

        internal void SetDefaultVariablesFilter(string filterText)
        {
            DefaultVariablesFilterText = filterText;
        }

        internal void ClearDefaultVariablesFilter()
        {
            DefaultVariablesFilterText = string.Empty;
        }

        private void ApplyDefaultVariablesFilter()
        {
            if (DefaultVariables == null || DefaultVariables.Variables == null)
            {
                FilteredDefaultVariables = new ObservableCollection<Variable>();
                return;
            }

            if (string.IsNullOrWhiteSpace(DefaultVariablesFilterText))
            {
                FilteredDefaultVariables = new ObservableCollection<Variable>(DefaultVariables.Variables);
                return;
            }

            var query = DefaultVariablesFilterText.Trim();
            var filtered = DefaultVariables.Variables
                .Where(variable =>
                    (!string.IsNullOrWhiteSpace(variable?.Name) && variable.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(variable?.Values) && variable.Values.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            FilteredDefaultVariables = new ObservableCollection<Variable>(filtered);
        }

        internal void RemoveProfile(ProfileVariablesSet profile)
        {
            if (profile == null)
            {
                return;
            }

            if (Profiles == null)
            {
                return;
            }

            if (profile.IsEnabled)
            {
                UnsetAppliedProfile();
            }

            Profiles.Remove(profile);

            _ = Task.Run(SaveAsync);
        }

        internal void DeleteVariable(Variable variable, ProfileVariablesSet profile)
        {
            if (variable == null)
            {
                return;
            }

            if (variable.ParentType == VariablesSetType.Profile && profile == null)
            {
                LoggerInstance.Logger.LogError("Invalid delete: cannot delete profile variable without owning profile set.");
                return;
            }

            bool propagateChange = true;

            if (profile != null)
            {
                if (profile.Variables == null)
                {
                    return;
                }

                var targetVariable = ResolveProfileVariable(profile.Variables, variable);
                if (targetVariable == null)
                {
                    LoggerInstance.Logger.LogError("Invalid delete: unable to resolve owning profile variable.");
                    return;
                }

                // Profile variable
                var removed = profile.Variables.Remove(targetVariable);
                if (!removed)
                {
                    LoggerInstance.Logger.LogError("Failed to remove profile variable from profile list.");
                    return;
                }

                if (!profile.IsEnabled)
                {
                    propagateChange = false;
                }

                _ = Task.Run(SaveAsync);
            }
            else
            {
                if (variable.ParentType == VariablesSetType.User && UserDefaultSet?.Variables != null)
                {
                    UserDefaultSet.Variables.Remove(variable);
                }
                else if (variable.ParentType == VariablesSetType.System && SystemDefaultSet?.Variables != null)
                {
                    SystemDefaultSet.Variables.Remove(variable);
                }
            }

            if (propagateChange)
            {
                var task = Task.Run(() =>
                {
                    if (profile == null)
                    {
                        EnvironmentVariablesHelper.UnsetVariable(variable);
                    }
                    else
                    {
                        profile.UnapplyVariable(variable);
                    }
                });
                task.ContinueWith((a) =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        PopulateAppliedVariables();
                    });
                });
            }
        }

        private static Variable ResolveProfileVariable(ObservableCollection<Variable> variables, Variable target)
        {
            if (variables == null || target == null)
            {
                return null;
            }

            var exactMatch = variables.FirstOrDefault(x => ReferenceEquals(x, target));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var normalizedName = (target.Name ?? string.Empty).Trim();
            return variables
                .Where(x => x != null && string.Equals((x.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase))
                .Where(x => EnvironmentVariablesHelper.IsEquivalentVariableValue(x?.Values, target.Values))
                .FirstOrDefault();
        }
    }
}
