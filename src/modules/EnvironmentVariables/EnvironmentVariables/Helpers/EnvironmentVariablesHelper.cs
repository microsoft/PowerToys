// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Security;
using EnvironmentVariables.Models;

namespace EnvironmentVariables.Helpers
{
    internal sealed class EnvironmentVariablesHelper
    {
        internal static string GetBackupVariableName(Variable variable, string profileName)
        {
            return variable.Name + "_PowerToys_" + profileName;
        }

        internal static Variable GetExisting(string variableName)
        {
            var userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);

            if (userVariables.Contains(variableName))
            {
                return new Variable(variableName, userVariables[variableName] as string, VariablesSetType.User);
            }

            var systemVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);

            if (systemVariables.Contains(variableName))
            {
                return new Variable(variableName, userVariables[variableName] as string, VariablesSetType.System);
            }

            return null;
        }

        internal static void GetVariables(EnvironmentVariableTarget target, VariablesSet set)
        {
            var variables = Environment.GetEnvironmentVariables(target);

            foreach (DictionaryEntry variable in variables)
            {
                string key = variable.Key as string;
                string value = variable.Value as string;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                Variable entry = new Variable(key, value, set.Type);
                set.Variables.Add(entry);
            }
        }

        internal static bool SetVariable(Variable variable)
        {
            EnvironmentVariableTarget target = variable.ParentType switch
            {
                VariablesSetType.Profile => EnvironmentVariableTarget.User,
                VariablesSetType.User => EnvironmentVariableTarget.User,
                VariablesSetType.System => EnvironmentVariableTarget.Machine,
                _ => throw new NotImplementedException(),
            };

            try
            {
                Environment.SetEnvironmentVariable(variable.Name, variable.Values, target);
            }
            catch (SecurityException)
            {
                // Permission denied (e.g. trying to change System variable while running non-elevated)
                // Show specific error message
                return false;
            }
            catch (Exception)
            {
                // Something else went wrong
                return false;
            }

            return true;
        }

        internal static bool UnsetVariable(Variable variable)
        {
            EnvironmentVariableTarget target = variable.ParentType switch
            {
                VariablesSetType.Profile => EnvironmentVariableTarget.User,
                VariablesSetType.User => EnvironmentVariableTarget.User,
                VariablesSetType.System => EnvironmentVariableTarget.Machine,
                _ => throw new NotImplementedException(),
            };

            try
            {
                Environment.SetEnvironmentVariable(variable.Name, string.Empty, target);
            }
            catch (SecurityException)
            {
                // Permission denied (e.g. trying to change System variable while running non-elevated)
                // Show specific error message
                return false;
            }
            catch (Exception)
            {
                // Something else went wrong
                return false;
            }

            return true;
        }
    }
}
