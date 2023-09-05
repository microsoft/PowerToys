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
            UserDefaultSet.Variables.Add(new Variable("user1", "value1"));
            UserDefaultSet.Variables.Add(new Variable("user2", "value2"));

            SystemDefaultSet.Variables.Add(new Variable("system1", "svalue1"));
            SystemDefaultSet.Variables.Add(new Variable("system2", "svalue2"));

            var profile1 = new ProfileVariablesSet(Guid.NewGuid(), "profile1");
            profile1.Variables.Add(new Variable("profile11", "pvalue1"));
            profile1.Variables.Add(new Variable("profile12", "pvalue2"));
            var profile2 = new ProfileVariablesSet(Guid.NewGuid(), "profile2");
            profile2.Variables.Add(new Variable("profile21", "pvalue11"));
            profile2.Variables.Add(new Variable("profile22", "pvalue22"));

            Profiles.Add(profile1);
            Profiles.Add(profile2);
        }
    }
}
