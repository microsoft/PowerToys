// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable annotations

using System.Collections.Generic;
using System.Linq;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.ViewModels
{
    internal static class VariableEditorState
    {
        internal static bool CanAddDefault(Variable draft, VariablesSet sameScope)
        {
            if (draft == null)
            {
                return false;
            }

            bool isDuplicate = sameScope.Variables.Any(x =>
                EnvironmentVariableComparisonHelper.NamesEqual(x.Name, draft.Name));
            return !isDuplicate && draft.Validate();
        }

        internal static bool CanSaveEdit(Variable draft, Variable original, VariablesSet sameScope)
        {
            if (draft == null)
            {
                return false;
            }

            bool hasDuplicate = sameScope != null
                && sameScope.Variables.Any(x =>
                    !ReferenceEquals(x, original)
                    && EnvironmentVariableComparisonHelper.NamesEqual(x.Name, draft.Name));
            return !hasDuplicate && draft.Validate();
        }

        internal static bool CanAddProfileVariable(ProfileVariablesSet profile, bool newMode, string name, string value, IEnumerable<object> selected)
        {
            if (profile == null)
            {
                return false;
            }

            if (newMode)
            {
                var variable = new Variable(name, value, VariablesSetType.Profile);
                return variable.Validate()
                    && !profile.Variables.Any(x =>
                        EnvironmentVariableComparisonHelper.NamesEqual(x.Name, variable.Name));
            }

            return selected.OfType<Variable>().Any(variable => !profile.Variables.Any(x =>
                EnvironmentVariableComparisonHelper.NamesEqual(x.Name, variable.Name)));
        }

        internal static void AddProfileVariable(ProfileVariablesSet profile, bool newMode, string name, string value, IEnumerable<object> selected)
        {
            if (profile != null)
            {
                if (newMode)
                {
                    var variable = new Variable(name, value, VariablesSetType.Profile);
                    if (variable.Validate() && !profile.Variables.Any(x =>
                        EnvironmentVariableComparisonHelper.NamesEqual(x.Name, variable.Name)))
                    {
                        profile.Variables.Add(variable);
                    }
                }
                else
                {
                    foreach (Variable variable in selected.Cast<Variable>())
                    {
                        if (!profile.Variables.Any(x =>
                            EnvironmentVariableComparisonHelper.NamesEqual(x.Name, variable.Name)))
                        {
                            var clone = variable.Clone(true);
                            profile.Variables.Add(clone);
                        }
                    }
                }
            }
        }

        internal static Variable[] GetSecondSelectionsFromDuplicateNameGroups(IEnumerable<Variable> selected)
        {
            return EnvironmentVariableComparisonHelper.GetDuplicateNameGroups(selected)
                .Select(group => group.ElementAt(1)).ToArray();
        }

        internal static void RemoveFirstEquivalent(ProfileVariablesSet profile, Variable removed)
        {
            for (int i = 0; i < profile.Variables.Count; i++)
            {
                if (EnvironmentVariableComparisonHelper.EntriesEqual(profile.Variables[i], removed))
                {
                    profile.Variables.RemoveAt(i);
                    break;
                }
            }
        }

        internal static Variable[] LoadSelection(ProfileVariablesSet profile, IEnumerable<Variable> items, IEnumerable<Variable> alreadySelected)
        {
            var selected = alreadySelected.ToList();
            var additions = new List<Variable>();
            foreach (Variable item in items)
            {
                if (item != null)
                {
                    foreach (var profileItem in profile.Variables)
                    {
                        if (EnvironmentVariableComparisonHelper.EntriesEqual(profileItem, item))
                        {
                            if (selected.Any(x =>
                                EnvironmentVariableComparisonHelper.NamesEqual(x.Name, profileItem.Name)))
                            {
                                continue;
                            }

                            additions.Add(item);
                            selected.Add(item);
                        }
                    }
                }
            }

            return additions.ToArray();
        }

        internal static string? MoveUp(Variable draft, Variable.ValuesListItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            var index = draft.ValuesList.IndexOf(selected);
            if (index > 0)
            {
                draft.ValuesList.Move(index, index - 1);
            }

            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }

        internal static string? MoveDown(Variable draft, Variable.ValuesListItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            var index = draft.ValuesList.IndexOf(selected);
            if (index < draft.ValuesList.Count - 1)
            {
                draft.ValuesList.Move(index, index + 1);
            }

            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }

        internal static string? DeleteEntry(Variable draft, Variable.ValuesListItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            draft.ValuesList.Remove(selected);
            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }

        internal static string? InsertBefore(Variable draft, Variable.ValuesListItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            var index = draft.ValuesList.IndexOf(selected);
            draft.ValuesList.Insert(index, new Variable.ValuesListItem { Text = string.Empty });
            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }

        internal static string? InsertAfter(Variable draft, Variable.ValuesListItem selected)
        {
            if (selected == null)
            {
                return null;
            }

            var index = draft.ValuesList.IndexOf(selected);
            draft.ValuesList.Insert(index + 1, new Variable.ValuesListItem { Text = string.Empty });
            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }

        internal static string? CommitEntryText(Variable draft, Variable.ValuesListItem selected, string text)
        {
            if (selected == null || selected.Text == text)
            {
                return null;
            }

            selected.Text = text;
            return string.Join(";", draft.ValuesList?.Select(x => x.Text).ToArray());
        }
    }
}
