// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using EnvironmentVariablesUILib.Helpers;

namespace EnvironmentVariablesUILib.Models
{
    public partial class ProfileVariablesSet : VariablesSet
    {
        [ObservableProperty]
        private bool _isEnabled;

        public ProfileVariablesSet()
            : base()
        {
            Type = VariablesSetType.Profile;
            IconPath = ProfileIconPath;
        }

        public ProfileVariablesSet(Guid id, string name)
            : base(id, name, VariablesSetType.Profile)
        {
            IconPath = ProfileIconPath;
        }

        public Task Apply()
        {
            if (Variables == null)
            {
                return Task.CompletedTask;
            }

            var profileName = (Name ?? string.Empty).Trim();

            return Task.Run(() =>
            {
                foreach (var variable in Variables)
                {
                    if (variable == null)
                    {
                        continue;
                    }

                    // Get existing variable with the same name if it exist
                    var variableToOverride = EnvironmentVariablesHelper.GetExisting((variable.Name ?? string.Empty).Trim());

                    // It exists. Rename it to preserve it.
                    if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                    {
                        var backupName = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, profileName);
                        var backupVariableToSave = new Variable(variableToOverride.Name, variableToOverride.Values, variableToOverride.ParentType)
                        {
                            Name = backupName,
                        };

                        if (!backupVariableToSave.Validate())
                        {
                            LoggerInstance.Logger.LogError("Invalid backup variable name. Skipping profile backup.");
                        }

                        // Only create a backup variable if there isn't one already for this profile.
                        else if (EnvironmentVariablesHelper.GetExisting(backupName) == null)
                        {
                            // Backup the variable
                            if (!EnvironmentVariablesHelper.SetProfileVariableWithoutNotify(backupVariableToSave))
                            {
                                LoggerInstance.Logger.LogError("Failed to set backup variable.");
                            }
                        }
                        else
                        {
                            LoggerInstance.Logger.LogError("Cannot back up user variable because backup already exists.");
                        }
                    }

                    if (!EnvironmentVariablesHelper.SetProfileVariableWithoutNotify(variable))
                    {
                        LoggerInstance.Logger.LogError("Failed to set profile variable.");
                    }
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange();
            });
        }

        public Task UnApply()
        {
            if (Variables == null)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                foreach (var variable in Variables)
                {
                    if (variable == null)
                    {
                        continue;
                    }

                    UnapplyVariable(variable);
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange();
            });
        }

        public void UnapplyVariable(Variable variable)
        {
            if (variable == null)
            {
                return;
            }

            // Unset the variable
            if (!EnvironmentVariablesHelper.UnsetProfileVariableWithoutNotify(variable))
            {
                LoggerInstance.Logger.LogError("Failed to unset variable.");
            }

            var originalName = variable.Name;
            var backupName = EnvironmentVariablesHelper.GetBackupVariableName(variable, (Name ?? string.Empty).Trim());

            // Get backup variable if it exist
            var backupVariable = EnvironmentVariablesHelper.GetExisting(backupName);

            if (backupVariable != null)
            {
                var variableToRestore = new Variable(originalName, backupVariable.Values, backupVariable.ParentType);

                if (!EnvironmentVariablesHelper.UnsetProfileVariableWithoutNotify(backupVariable))
                {
                    LoggerInstance.Logger.LogError("Failed to unset backup variable.");
                }

                if (!EnvironmentVariablesHelper.SetProfileVariableWithoutNotify(variableToRestore))
                {
                    LoggerInstance.Logger.LogError("Failed to restore backup variable.");
                }
            }
        }

        public bool IsCorrectlyApplied()
        {
            if (!IsEnabled)
            {
                return false;
            }

            if (Variables == null)
            {
                return false;
            }

            foreach (var variable in Variables)
            {
                if (variable == null)
                {
                    continue;
                }

                var applied = EnvironmentVariablesHelper.GetExisting((variable.Name ?? string.Empty).Trim());
                if (applied != null
                    && string.Equals((applied.Name ?? string.Empty).Trim(), (variable.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                    && EnvironmentVariablesHelper.IsEquivalentVariableValue(applied.Values, variable.Values)
                    && applied.ParentType == VariablesSetType.User)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public bool IsApplicable()
        {
            if (Variables == null)
            {
                return true;
            }

            foreach (var variable in Variables)
            {
                if (variable == null)
                {
                    continue;
                }

                if (!variable.Validate())
                {
                    return false;
                }

                // Get existing variable with the same name if it exist
                var variableToOverride = EnvironmentVariablesHelper.GetExisting((variable.Name ?? string.Empty).Trim());

                // It exists. Backup is needed.
                if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                {
                    var backupName = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, (Name ?? string.Empty).Trim());
                    var backupVariable = new Variable(variableToOverride.Name, variableToOverride.Values, variableToOverride.ParentType)
                    {
                        Name = backupName,
                    };

                    if (!backupVariable.Validate())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public ProfileVariablesSet Clone()
        {
            var clone = new ProfileVariablesSet(this.Id, (this.Name ?? string.Empty).Trim());
            var variables = this.Variables
                ?.Select(variable => variable == null ? null : variable.Clone(profile: true))
                .ToList() ?? new System.Collections.Generic.List<Variable>();
            clone.Variables = new ObservableCollection<Variable>(variables);
            clone.IsEnabled = this.IsEnabled;

            return clone;
        }
    }
}
