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
using EnvironmentVariablesUILib.Helpers;

namespace EnvironmentVariablesUILib.Models
{
    public partial class Variable : ObservableObject, IJsonOnDeserialized
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        [NotifyPropertyChangedFor(nameof(ShowAsList))]
        private string _name;

        [ObservableProperty]
        private string _values;

        [ObservableProperty]
        private bool _applyToSystem;

        [JsonIgnore]
        [property: JsonIgnore]
        [ObservableProperty]
        private bool _isAppliedFromProfile; // Used to mark that a variable in a default set is applied by a profile. Used to disable editing / mark it in the UI.

        [JsonIgnore]
        public bool IsEditable
        {
            get
            {
                return (ParentType != VariablesSetType.System || ElevationHelper.ElevationHelperInstance.IsElevated) && !IsAppliedFromProfile;
            }
        }

        [JsonIgnore]
        public VariablesSetType ParentType { get; set; }

        // To store the strings in the Values List with actual objects that can be referenced and identity compared
        public class ValuesListItem
        {
            public string Text { get; set; }
        }

        [ObservableProperty]
        [property: JsonIgnore]
        [JsonIgnore]
        private ObservableCollection<ValuesListItem> _valuesList;

        [JsonIgnore]
        public bool Valid => Validate();

        [JsonIgnore]
        public bool ShowAsList => IsList();

        private bool IsList()
        {
            List<string> listVariables = new() { "PATH", "PATHEXT", "PSMODULEPATH" };

            foreach (var name in listVariables)
            {
                if (Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void OnDeserialized()
        {
            // No need to save ValuesList to the Json, so we are generating it after deserializing
            ValuesList = ValuesStringToValuesListItemCollection(Values);
        }

        public Variable()
        {
        }

        public Variable(string name, string values, VariablesSetType parentType)
        {
            Name = name;
            Values = values;
            ParentType = parentType;

            ValuesList = ValuesStringToValuesListItemCollection(Values);
        }

        internal static ObservableCollection<ValuesListItem> ValuesStringToValuesListItemCollection(string values)
        {
            return new ObservableCollection<ValuesListItem>(values.Split(';').Select(x => new ValuesListItem { Text = x }));
        }

        internal Task Update(Variable edited, bool propagateChange, ProfileVariablesSet parentProfile)
        {
            bool nameChanged = Name != edited.Name;

            var clone = this.Clone();

            // Update state
            Name = edited.Name;
            Values = edited.Values;

            ValuesList = ValuesStringToValuesListItemCollection(Values);

            return Task.Run(() =>
            {
                // Apply changes
                if (propagateChange)
                {
                    if (nameChanged)
                    {
                        if (!EnvironmentVariablesHelper.UnsetVariable(clone))
                        {
                            LoggerInstance.Logger.LogError("Failed to unset original variable.");
                        }

                        if (parentProfile != null)
                        {
                            var backupName = EnvironmentVariablesHelper.GetBackupVariableName(clone, parentProfile.Name);

                            // Get backup variable if it exist
                            var backupVariable = EnvironmentVariablesHelper.GetExisting(backupName);
                            if (backupVariable != null)
                            {
                                var variableToRestore = new Variable(clone.Name, backupVariable.Values, backupVariable.ParentType);

                                if (!EnvironmentVariablesHelper.UnsetVariableWithoutNotify(backupVariable))
                                {
                                    LoggerInstance.Logger.LogError("Failed to unset backup variable.");
                                }

                                if (!EnvironmentVariablesHelper.SetVariableWithoutNotify(variableToRestore))
                                {
                                    LoggerInstance.Logger.LogError("Failed to restore backup variable.");
                                }
                            }
                        }
                    }

                    // Get existing variable with the same name if it exist
                    var variableToOverride = EnvironmentVariablesHelper.GetExisting(Name);

                    // It exists. Rename it to preserve it.
                    if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User && parentProfile != null)
                    {
                        // Gets which name the backup variable should have.
                        variableToOverride.Name = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, parentProfile.Name);

                        // Only create a backup variable if there's not one already, to avoid overriding. (solves Path nuking errors, for example, after editing path on an enabled profile)
                        if (EnvironmentVariablesHelper.GetExisting(variableToOverride.Name) == null)
                        {
                            // Backup the variable
                            if (!EnvironmentVariablesHelper.SetVariableWithoutNotify(variableToOverride))
                            {
                                LoggerInstance.Logger.LogError("Failed to set backup variable.");
                            }
                        }
                    }

                    if (!EnvironmentVariablesHelper.SetVariable(this))
                    {
                        LoggerInstance.Logger.LogError("Failed to set new variable.");
                    }
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
                ValuesList = ValuesStringToValuesListItemCollection(Values),
            };
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return false;
            }

            const int MaxUserEnvVariableLength = 255; // User-wide env vars stored in the registry have names limited to 255 chars
            if (ParentType != VariablesSetType.System && Name.Length >= MaxUserEnvVariableLength)
            {
                LoggerInstance.Logger.LogError("Variable name too long.");
                return false;
            }

            return true;
        }
    }
}
