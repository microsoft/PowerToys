// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
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
            return Task.Run(() =>
            {
                foreach (var variable in Variables)
                {
                    var applyToSystem = variable.ApplyToSystem;

                    // Get existing variable with the same name if it exist
                    var variableToOverride = EnvironmentVariablesHelper.GetExisting(variable.Name);

                    // It exists. Rename it to preserve it.
                    if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                    {
                        variableToOverride.Name = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, this.Name);

                        // Backup the variable
                        if (!EnvironmentVariablesHelper.SetVariableWithoutNotify(variableToOverride))
                        {
                            LoggerInstance.Logger.LogError("Failed to set backup variable.");
                        }
                    }

                    if (!EnvironmentVariablesHelper.SetVariableWithoutNotify(variable))
                    {
                        LoggerInstance.Logger.LogError("Failed to set profile variable.");
                    }
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange();
            });
        }

        public Task UnApply()
        {
            return Task.Run(() =>
            {
                foreach (var variable in Variables)
                {
                    UnapplyVariable(variable);
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange();
            });
        }

        public void UnapplyVariable(Variable variable)
        {
            // Unset the variable
            if (!EnvironmentVariablesHelper.UnsetVariableWithoutNotify(variable))
            {
                LoggerInstance.Logger.LogError("Failed to unset variable.");
            }

            var originalName = variable.Name;
            var backupName = EnvironmentVariablesHelper.GetBackupVariableName(variable, this.Name);

            // Get backup variable if it exist
            var backupVariable = EnvironmentVariablesHelper.GetExisting(backupName);

            if (backupVariable != null)
            {
                var variableToRestore = new Variable(originalName, backupVariable.Values, backupVariable.ParentType);

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

        public bool IsCorrectlyApplied()
        {
            if (!IsEnabled)
            {
                return false;
            }

            foreach (var variable in Variables)
            {
                var applied = EnvironmentVariablesHelper.GetExisting(variable.Name);
                if (applied != null && applied.Values == variable.Values && applied.ParentType == VariablesSetType.User)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public bool IsApplicable()
        {
            foreach (var variable in Variables)
            {
                if (!variable.Validate())
                {
                    return false;
                }

                // Get existing variable with the same name if it exist
                var variableToOverride = EnvironmentVariablesHelper.GetExisting(variable.Name);

                // It exists. Backup is needed.
                if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                {
                    variableToOverride.Name = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, this.Name);
                    if (!variableToOverride.Validate())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public ProfileVariablesSet Clone()
        {
            var clone = new ProfileVariablesSet(this.Id, this.Name);
            clone.Variables = new ObservableCollection<Variable>(this.Variables);
            clone.IsEnabled = this.IsEnabled;

            return clone;
        }
    }
}
