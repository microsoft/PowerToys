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
            return Apply(WindowsEnvironmentVariablesEnvironment.Instance, action => Task.Run(action));
        }

        internal Task Apply(IEnvironmentVariablesEnvironment environment, Func<Action, Task> run)
        {
            return run(() =>
            {
                foreach (var variable in Variables)
                {
                    // Get existing variable with the same name if it exist
                    var variableToOverride = EnvironmentVariablesHelper.GetExisting(variable.Name, environment);

                    // It exists. Rename it to preserve it.
                    if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                    {
                        variableToOverride.Name = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, this.Name);

                        // Backup the variable
                        if (!EnvironmentVariablesHelper.SetBackupVariableWithoutNotify(variableToOverride, environment))
                        {
                            LoggerInstance.Logger.LogError("Failed to set backup variable.");
                        }
                    }

                    if (!EnvironmentVariablesHelper.SetProfileVariableWithoutNotify(variable, environment))
                    {
                        LoggerInstance.Logger.LogError("Failed to set profile variable.");
                    }
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange(environment);
            });
        }

        public Task UnApply()
        {
            return UnApply(WindowsEnvironmentVariablesEnvironment.Instance, action => Task.Run(action));
        }

        internal Task UnApply(IEnvironmentVariablesEnvironment environment, Func<Action, Task> run)
        {
            return run(() =>
            {
                foreach (var variable in Variables)
                {
                    UnapplyVariable(variable, environment);
                }

                EnvironmentVariablesHelper.NotifyEnvironmentChange(environment);
            });
        }

        public void UnapplyVariable(Variable variable)
        {
            UnapplyVariable(variable, WindowsEnvironmentVariablesEnvironment.Instance);
        }

        internal void UnapplyVariable(Variable variable, IEnvironmentVariablesEnvironment environment)
        {
            // Unset the variable
            if (!EnvironmentVariablesHelper.UnsetProfileVariableWithoutNotify(variable, environment))
            {
                LoggerInstance.Logger.LogError("Failed to unset variable.");
            }

            var originalName = variable.Name;
            var backupName = EnvironmentVariablesHelper.GetBackupVariableName(variable, this.Name);

            // Get backup variable if it exist
            var backupVariable = EnvironmentVariablesHelper.GetExisting(backupName, environment);

            if (backupVariable != null)
            {
                var variableToRestore = new Variable(originalName, backupVariable.Values, backupVariable.ParentType);

                if (!EnvironmentVariablesHelper.UnsetBackupVariableWithoutNotify(backupVariable, environment))
                {
                    LoggerInstance.Logger.LogError("Failed to unset backup variable.");
                }

                if (!EnvironmentVariablesHelper.SetProfileVariableWithoutNotify(variableToRestore, environment))
                {
                    LoggerInstance.Logger.LogError("Failed to restore backup variable.");
                }
            }
        }

        public bool IsCorrectlyApplied()
        {
            return IsCorrectlyApplied(WindowsEnvironmentVariablesEnvironment.Instance);
        }

        internal bool IsCorrectlyApplied(IEnvironmentVariablesEnvironment environment)
        {
            if (!IsEnabled || !IsApplicable(environment))
            {
                return false;
            }

            foreach (var variable in Variables)
            {
                var applied = EnvironmentVariablesHelper.GetExisting(variable.Name, environment);
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
            return IsApplicable(WindowsEnvironmentVariablesEnvironment.Instance);
        }

        internal bool IsApplicable(IEnvironmentVariablesEnvironment environment)
        {
            if (!Valid)
            {
                return false;
            }

            foreach (var variable in Variables)
            {
                if (!variable.Validate())
                {
                    return false;
                }

                // Get existing variable with the same name if it exists.
                var variableToOverride = EnvironmentVariablesHelper.GetExisting(variable.Name, environment);

                // It exists. Backup is needed.
                if (variableToOverride != null && variableToOverride.ParentType == VariablesSetType.User)
                {
                    string backupName = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, this.Name);

                    if (!EnvironmentVariablesHelper.TryValidateBackupVariable(backupName, variableToOverride.Values, out string errorMessage))
                    {
                        LoggerInstance.Logger.LogError(
                            $"The variable '{variable.Name}' cannot be applied because the backup variable '{backupName}' would be invalid: {errorMessage}");
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
