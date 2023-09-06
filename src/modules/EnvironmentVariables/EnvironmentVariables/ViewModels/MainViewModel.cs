// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using EnvironmentVariables.Helpers;
using EnvironmentVariables.Models;

namespace EnvironmentVariables.ViewModels
{
    public partial class MainViewModel
    {
        public DefaultVariablesSet UserDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.UserGuid, ResourceLoaderInstance.ResourceLoader.GetString("User"), VariablesSetType.User);

        public DefaultVariablesSet SystemDefaultSet { get; private set; } = new DefaultVariablesSet(VariablesSet.SystemGuid, ResourceLoaderInstance.ResourceLoader.GetString("System"), VariablesSetType.System);

        public ObservableCollection<ProfileVariablesSet> Profiles { get; private set; } = new ObservableCollection<ProfileVariablesSet>();

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
            var profile2 = new ProfileVariablesSet(Guid.NewGuid(), "profile2");
            profile2.Variables.Add(new Variable("profile21", "pvalue11", VariablesSetType.Profile));
            profile2.Variables.Add(new Variable("profile22", "pvalue22", VariablesSetType.Profile));

            Profiles.Add(profile1);
            Profiles.Add(profile2);
        }

        internal void EditVariable(Variable original, Variable edited)
        {
            original.Update(edited);
        }
    }
}
