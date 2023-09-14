// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using EnvironmentVariables.Helpers;

namespace EnvironmentVariables.Models
{
    public partial class Variable : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _values;

        public bool Editable { get; private set; }

        public VariablesSetType ParentType { get; set; }

        public List<string> ValuesList { get; set; }

        public Variable()
        {
        }

        public Variable(string name, string values, VariablesSetType parentType)
        {
            Name = name;
            Values = values;
            ParentType = parentType;
            Editable = ParentType == VariablesSetType.User || (ParentType == VariablesSetType.System && App.GetService<IElevationHelper>().IsElevated);

            var splitValues = Values.Split(';');
            if (splitValues.Length > 0)
            {
                ValuesList = new List<string>(splitValues);
            }
        }

        internal void Update(Variable edited)
        {
            bool changed = Name != edited.Name || Values != edited.Values;
            bool nameChanged = Name != edited.Name;
            bool success = false;

            if (changed)
            {
                // Apply changes
                if (nameChanged)
                {
                    success = EnvironmentVariablesHelper.UnsetVariable(this);
                }

                success = EnvironmentVariablesHelper.SetVariable(edited);

                // Update state
                if (success)
                {
                    Name = edited.Name;
                    Values = edited.Values;

                    ValuesList = new List<string>();

                    var splitValues = Values.Split(';');
                    if (splitValues.Length > 0)
                    {
                        foreach (var splitValue in splitValues)
                        {
                            ValuesList.Add(splitValue);
                        }
                    }
                }
                else
                {
                    // show error
                }
            }
        }

        internal Variable Clone(bool profile = false)
        {
            return new Variable
            {
                Name = Name,
                Values = Values,
                ParentType = profile ? VariablesSetType.Profile : ParentType,
                ValuesList = new List<string>(ValuesList),
            };
        }
    }
}
