// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using EnvironmentVariables.Helpers;
using ManagedCommon;

namespace EnvironmentVariables.Models
{
    public partial class ProfileVariablesSet : VariablesSet
    {
        [ObservableProperty]
        private bool _isEnabled;

        public ProfileVariablesSet()
            : base()
        {
        }

        public ProfileVariablesSet(Guid id, string name)
            : base(id, name, VariablesSetType.Profile)
        {
        }

        public void Apply()
        {
            foreach (var variable in Variables)
            {
                // Get existing variable with the same name if it exist
                var variableToOverride = EnvironmentVariablesHelper.GetExisting(variable);

                // It exists. Rename it to preserve it.
                if (variableToOverride != null)
                {
                    variableToOverride.Name = EnvironmentVariablesHelper.GetBackupVariableName(variableToOverride, this.Name);

                    // Backup the variable
                    if (!EnvironmentVariablesHelper.SetVariable(variableToOverride))
                    {
                        Logger.LogError("Failed to set backup variable.");
                    }
                }

                if (!EnvironmentVariablesHelper.SetVariable(variable))
                {
                    Logger.LogError("Failed to set profile variable.");
                }
            }
        }

        public void UnApply()
        {
            foreach (var variable in Variables)
            {
                // Unset the variable
                if (!EnvironmentVariablesHelper.UnsetVariable(variable))
                {
                    Logger.LogError("Failed to unset variable.");
                }

                var originalName = variable.Name;

                variable.Name = EnvironmentVariablesHelper.GetBackupVariableName(variable, this.Name);

                // Get backup variable if it exist
                var backupVariable = EnvironmentVariablesHelper.GetExisting(variable);

                if (backupVariable != null)
                {
                    var variableToRestore = new Variable(originalName, backupVariable.Values, backupVariable.ParentType);

                    if (!EnvironmentVariablesHelper.UnsetVariable(backupVariable))
                    {
                        Logger.LogError("Failed to unset backup variable.");
                    }

                    if (!EnvironmentVariablesHelper.SetVariable(variableToRestore))
                    {
                        Logger.LogError("Failed to restore backup variable.");
                    }
                }
            }
        }
    }
}
