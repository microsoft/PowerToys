// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnvironmentVariables.Helpers;
using EnvironmentVariables.Models;
using ManagedCommon;

namespace EnvironmentVariables.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IEnvironmentVariablesService _environmentVariablesService;

        public DefaultVariablesSet UserDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.UserGuid, ResourceLoaderInstance.ResourceLoader.GetString("User"), VariablesSetType.User);

        public DefaultVariablesSet SystemDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.SystemGuid, ResourceLoaderInstance.ResourceLoader.GetString("System"), VariablesSetType.System);

        public VariablesSet DefaultVariables { get; private set; } = new DefaultVariablesSet(Guid.NewGuid(), ResourceLoaderInstance.ResourceLoader.GetString("DefaultVariables"), VariablesSetType.User);

        [ObservableProperty]
        private ObservableCollection<ProfileVariablesSet> _profiles;

        [ObservableProperty]
        private ObservableCollection<Variable> _appliedVariables = new ObservableCollection<Variable>();

        public ProfileVariablesSet AppliedProfile { get; set; }

        public MainViewModel(IEnvironmentVariablesService environmentVariablesService)
        {
            _environmentVariablesService = environmentVariablesService;
        }

        [RelayCommand]
        public void LoadEnvironmentVariables()
        {
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.Machine, SystemDefaultSet);
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, UserDefaultSet);

            foreach (var variable in UserDefaultSet.Variables)
            {
                DefaultVariables.Variables.Add(variable);
            }

            foreach (var variable in SystemDefaultSet.Variables)
            {
                DefaultVariables.Variables.Add(variable);
            }

            ReadAsync();
        }

        private async void ReadAsync()
        {
            try
            {
                var profiles = await _environmentVariablesService.ReadAsync();
                foreach (var profile in profiles)
                {
                    profile.PropertyChanged += Profile_PropertyChanged;
                }

                var applied = profiles.Where(x => x.IsEnabled).ToList();
                AppliedProfile = applied.Count > 0 ? applied.First() : null;

                Profiles = new ObservableCollection<ProfileVariablesSet>(profiles);

                PopulateAppliedVariables();
            }
            catch (Exception ex)
            {
                // Show some error
                Logger.LogError("Failed to save to profiles.json file", ex);
            }
        }

        private void PopulateAppliedVariables()
        {
            var variables = new List<Variable>();
            if (AppliedProfile != null)
            {
                variables = variables.Concat(AppliedProfile.Variables).ToList();
            }

            variables = variables.Concat(UserDefaultSet.Variables).Concat(SystemDefaultSet.Variables).ToList();
            variables = variables.GroupBy(x => x.Name).Select(y => y.First()).ToList();
            AppliedVariables = new ObservableCollection<Variable>(variables);
        }

        internal void EditVariable(Variable original, Variable edited)
        {
            original.Update(edited);
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

        private async Task SaveAsync()
        {
            try
            {
                await _environmentVariablesService.WriteAsync(Profiles);
            }
            catch (Exception ex)
            {
                // Show some error
                Logger.LogError("Failed to save to profiles.json file", ex);
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
                    }
                    else
                    {
                        UnsetAppliedProfile();
                    }
                }
            }

            _ = Task.Run(SaveAsync);
        }

        private void SetAppliedProfile(ProfileVariablesSet profile)
        {
            profile.Apply();
            AppliedProfile = profile;
            PopulateAppliedVariables();
        }

        private void UnsetAppliedProfile()
        {
            if (AppliedProfile != null)
            {
                var appliedProfile = AppliedProfile;
                appliedProfile.PropertyChanged -= Profile_PropertyChanged;
                AppliedProfile.UnApply();
                AppliedProfile.IsEnabled = false;
                AppliedProfile = null;
                appliedProfile.PropertyChanged += Profile_PropertyChanged;
                PopulateAppliedVariables();
            }
        }
    }
}
