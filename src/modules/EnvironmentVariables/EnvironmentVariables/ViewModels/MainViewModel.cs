// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using EnvironmentVariables.Helpers;
using EnvironmentVariables.Models;
using Windows.Security.EnterpriseData;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnvironmentVariables.ViewModels
{
    public partial class MainViewModel
    {
        public DefaultVariablesSet UserDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.UserGuid, ResourceLoaderInstance.ResourceLoader.GetString("User"), VariablesSetType.User);

        public DefaultVariablesSet SystemDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.SystemGuid, ResourceLoaderInstance.ResourceLoader.GetString("System"), VariablesSetType.System);

        public ObservableCollection<ProfileVariablesSet> Profiles { get; private set; } = new ObservableCollection<ProfileVariablesSet>();

        public ProfileVariablesSet AppliedProfile { get; set; }

        public MainViewModel()
        {
        }

        [RelayCommand]
        public void LoadEnvironmentVariables()
        {
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.Machine, SystemDefaultSet);
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, UserDefaultSet);

            var profile1 = new ProfileVariablesSet(Guid.NewGuid(), "profile1");
            profile1.Variables.Add(new Variable("profile11", "pvalue1", VariablesSetType.Profile));
            profile1.Variables.Add(new Variable("profile12", "pvalue2", VariablesSetType.Profile));
            profile1.PropertyChanged += Profile_PropertyChanged;

            var profile2 = new ProfileVariablesSet(Guid.NewGuid(), "profile2");
            profile2.Variables.Add(new Variable("profile21", "pvalue11", VariablesSetType.Profile));
            profile2.Variables.Add(new Variable("profile22", "pvalue22", VariablesSetType.Profile));
            profile2.PropertyChanged += Profile_PropertyChanged;

            Profiles.Add(profile1);
            Profiles.Add(profile2);
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
                SetAppliedProfile(profile);
            }

            Profiles.Add(profile);
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
                }
            }
        }

        private void SetAppliedProfile(ProfileVariablesSet profile)
        {
            profile.Apply();
            AppliedProfile = profile;
        }

        private void UnsetAppliedProfile()
        {
            if (AppliedProfile != null)
            {
                AppliedProfile.UnApply();
                AppliedProfile.IsEnabled = false;
            }

            AppliedProfile = null;
        }
    }
}
