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
                DefaultVariables.Variables.Add(variable);
                if (AppliedProfile != null)
                {
                    if (AppliedProfile.Variables.Where(
                        x => (x.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase) && x.Values.Equals(variable.Values, StringComparison.OrdinalIgnoreCase))
                            || variable.Name.Equals(EnvironmentVariablesHelper.GetBackupVariableName(x, AppliedProfile.Name), StringComparison.OrdinalIgnoreCase)).Any())
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
                var profiles = _environmentVariablesService.ReadProfiles();
                foreach (var profile in profiles)
                {
                    profile.PropertyChanged += Profile_PropertyChanged;

                    foreach (var variable in profile.Variables)
                    {
                        variable.ParentType = VariablesSetType.Profile;
                    }
                }

                var appliedProfiles = profiles.Where(x => x.IsEnabled).ToList();
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

                Profiles = new ObservableCollection<ProfileVariablesSet>(profiles);
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
            if (AppliedProfile != null)
            {
                variables = variables.Concat(AppliedProfile.Variables.Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values), VariablesSetType.Profile)).OrderBy(x => x.Name)).ToList();
            }

            // Variables are expanded to be shown in the applied variables section, so the user sees their actual values.
            variables = variables.Concat(UserDefaultSet.Variables.Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values), VariablesSetType.User)).OrderBy(x => x.Name))
                                 .Concat(SystemDefaultSet.Variables.Select(x => new Variable(x.Name, Environment.ExpandEnvironmentVariables(x.Values), VariablesSetType.System)).OrderBy(x => x.Name))
                                 .ToList();

            // Handle PATH variable - add USER value to the end of the SYSTEM value
            var profilePath = variables.Where(x => x.Name.Equals("PATH", StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.Profile).FirstOrDefault();
            var userPath = variables.Where(x => x.Name.Equals("PATH", StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.User).FirstOrDefault();
            var systemPath = variables.Where(x => x.Name.Equals("PATH", StringComparison.OrdinalIgnoreCase) && x.ParentType == VariablesSetType.System).FirstOrDefault();

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

            variables = variables.GroupBy(x => x.Name).Select(y => y.First()).ToList();

            // Find duplicates
            var duplicates = variables.Where(x => !x.Name.Equals("PATH", StringComparison.OrdinalIgnoreCase)).GroupBy(x => x.Name.ToLower(CultureInfo.InvariantCulture)).Where(g => g.Count() > 1);
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
            if (type == VariablesSetType.User)
            {
                UserDefaultSet.Variables.Add(variable);
                UserDefaultSet.Variables = new ObservableCollection<Variable>(UserDefaultSet.Variables.OrderBy(x => x.Name).ToList());
            }
            else if (type == VariablesSetType.System)
            {
                SystemDefaultSet.Variables.Add(variable);
                SystemDefaultSet.Variables = new ObservableCollection<Variable>(SystemDefaultSet.Variables.OrderBy(x => x.Name).ToList());
            }

            EnvironmentVariablesHelper.SetVariable(variable);
            PopulateAppliedVariables();
        }

        internal void EditVariable(Variable original, Variable edited, ProfileVariablesSet variablesSet)
        {
            bool propagateChange = variablesSet == null /* not a profile */ || variablesSet.Id.Equals(AppliedProfile?.Id);
            bool changed = original.Name != edited.Name || original.Values != edited.Values;
            if (changed)
            {
                var task = original.Update(edited, propagateChange, variablesSet);
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
            var existingProfile = Profiles.Where(x => x.Id == updatedProfile.Id).FirstOrDefault();
            if (existingProfile != null)
            {
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
            if (profile != null)
            {
                if (!profile.IsApplicable())
                {
                    profile.PropertyChanged -= Profile_PropertyChanged;
                    profile.IsEnabled = false;
                    profile.PropertyChanged += Profile_PropertyChanged;

                    EnvironmentState = EnvironmentState.ProfileNotApplicable;

                    return;
                }
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

        internal void RemoveProfile(ProfileVariablesSet profile)
        {
            if (profile.IsEnabled)
            {
                UnsetAppliedProfile();
            }

            Profiles.Remove(profile);

            _ = Task.Run(SaveAsync);
        }

        internal void DeleteVariable(Variable variable, ProfileVariablesSet profile)
        {
            bool propagateChange = true;

            if (profile != null)
            {
                // Profile variable
                profile.Variables.Remove(variable);

                if (!profile.IsEnabled)
                {
                    propagateChange = false;
                }

                _ = Task.Run(SaveAsync);
            }
            else
            {
                if (variable.ParentType == VariablesSetType.User)
                {
                    UserDefaultSet.Variables.Remove(variable);
                }
                else if (variable.ParentType == VariablesSetType.System)
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
    }
}
