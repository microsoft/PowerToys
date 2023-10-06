// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using EnvironmentVariables.Helpers;

namespace EnvironmentVariables.Models
{
    public partial class Variable : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        private string _name;

        [ObservableProperty]
        private string _values;

        [JsonIgnore]
        public bool IsEditable
        {
            get
            {
                return ParentType != VariablesSetType.System || App.GetService<IElevationHelper>().IsElevated;
            }
        }

        [JsonIgnore]
        public VariablesSetType ParentType { get; set; }

        [ObservableProperty]
        private ObservableCollection<string> _valuesList;

        public bool Valid => Validate();

        public Variable()
        {
        }

        public Variable(string name, string values, VariablesSetType parentType)
        {
            Name = name;
            Values = values;
            ParentType = parentType;

            var splitValues = Values.Split(';').Where(x => x.Length > 0).ToArray();
            if (splitValues.Length > 0)
            {
                ValuesList = new ObservableCollection<string>(splitValues);
            }
        }

        internal Task Update(Variable edited, bool propagateChange)
        {
            bool nameChanged = Name != edited.Name;
            bool success = true;

            var clone = this.Clone();

            // Update state
            Name = edited.Name;
            Values = edited.Values;

            ValuesList = new ObservableCollection<string>(Values.Split(';').Where(x => x.Length > 0).ToArray());

            return Task.Run(() =>
            {
                // Apply changes
                if (propagateChange)
                {
                    if (nameChanged)
                    {
                        success = EnvironmentVariablesHelper.UnsetVariable(clone);
                    }

                    success = EnvironmentVariablesHelper.SetVariable(this);
                }

                if (!success)
                {
                    // Show error
                }
            });
        }

        internal Variable Clone(bool profile = false)
        {
            return new Variable
            {
                Name = Name,
                Values = Values,
                ParentType = profile ? VariablesSetType.Profile : ParentType,
                ValuesList = new ObservableCollection<string>(ValuesList),
            };
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return false;
            }

            return true;
        }
    }
}
