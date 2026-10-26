// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.ViewModels
{
    internal sealed class EnvironmentVariablesPageActions
    {
        private readonly Func<MainViewModel> _readViewModel;
        private readonly Func<object> _readEditDraft;
        private readonly Func<object> _readProfileDraft;
        private readonly Func<object> _readDefaultDraft;
        private readonly Func<Task> _beginNewProfile;
        private readonly Action _addProfileVariable;
        private readonly Action _cancelProfileVariable;

        internal EnvironmentVariablesPageActions(
            Func<MainViewModel> readViewModel,
            Func<object> readEditDraft,
            Func<object> readProfileDraft,
            Func<object> readDefaultDraft,
            Func<Task> beginNewProfile,
            Action addProfileVariable,
            Action cancelProfileVariable)
        {
            _readViewModel = readViewModel;
            _readEditDraft = readEditDraft;
            _readProfileDraft = readProfileDraft;
            _readDefaultDraft = readDefaultDraft;
            _beginNewProfile = beginNewProfile;
            _addProfileVariable = addProfileVariable;
            _cancelProfileVariable = cancelProfileVariable;
        }

        internal ICommand EditCommand => new RelayCommand<EditTarget>(param =>
        {
            var variableSet = param.Set as ProfileVariablesSet;
            var original = param.Variable;
            var edited = _readEditDraft() as Variable;
            _readViewModel().EditVariable(original, edited, variableSet);
        });

        internal ICommand NewProfileCommand => new AsyncRelayCommand(_beginNewProfile);

        internal ICommand AddProfileCommand => new RelayCommand(() =>
        {
            var profile = _readProfileDraft() as ProfileVariablesSet;
            _readViewModel().AddProfile(profile);
        });

        internal ICommand UpdateProfileCommand => new RelayCommand(() =>
        {
            var updatedProfile = _readProfileDraft() as ProfileVariablesSet;
            _readViewModel().UpdateProfile(updatedProfile);
        });

        internal ICommand AddVariableCommand => new RelayCommand(_addProfileVariable);

        internal ICommand CancelAddVariableCommand => new RelayCommand(_cancelProfileVariable);

        internal ICommand AddDefaultVariableCommand => new RelayCommand<DefaultVariablesSet>(set =>
        {
            var variable = _readDefaultDraft() as Variable;
            var type = set.Type;
            _readViewModel().AddDefaultVariable(variable, type);
        });

        internal static async Task ShowVariableEditAsync(
            Variable original,
            Action configureDialog,
            Action<Variable> setDraft,
            Action updateGate,
            Func<Task> show)
        {
            configureDialog();
            var clone = original.Clone();
            setDraft(clone);
            updateGate();
            await show();
        }

        internal static async Task ShowNewProfileAsync(
            Action selectNewMode,
            Action configureDialog,
            Action<ProfileVariablesSet> setDraft,
            Func<Task> show)
        {
            selectNewMode();
            configureDialog();
            setDraft(new ProfileVariablesSet(Guid.NewGuid(), string.Empty));
            await show();
        }

        internal static async Task ShowProfileEditAsync(
            Action selectNewMode,
            Func<ProfileVariablesSet> readTarget,
            Action configureDialog,
            Action<ProfileVariablesSet> setDraft,
            Func<Task> show)
        {
            selectNewMode();
            var profile = readTarget();
            if (profile != null)
            {
                configureDialog();
                setDraft(profile.Clone());
                await show();
            }
        }

        internal static async Task ShowDefaultAddAsync(
            DefaultVariablesSet set,
            Action configureDialog,
            Action<Variable> setDraft,
            Action updateGate,
            Func<Task> show)
        {
            configureDialog();
            var variableType = set.Id == VariablesSet.SystemGuid ? VariablesSetType.System : VariablesSetType.User;
            setDraft(new Variable(string.Empty, string.Empty, variableType));
            updateGate();
            await show();
        }

        internal static async Task ConfirmRemoveProfileAsync(
            Func<MainViewModel> readViewModel,
            ProfileVariablesSet profile,
            Func<Func<string>, Action, Task> showConfirmation)
        {
            if (profile != null)
            {
                await showConfirmation(() => profile.Name, () => readViewModel().RemoveProfile(profile));
            }
        }

        internal static async Task ConfirmDeleteVariableAsync(
            Func<MainViewModel> readViewModel,
            Variable variable,
            ProfileVariablesSet profile,
            Func<Func<string>, Action, Task> showConfirmation)
        {
            if (variable != null)
            {
                await showConfirmation(() => variable.Name, () => readViewModel().DeleteVariable(variable, profile));
            }
        }

        internal static void AddProfileVariable(
            Func<ProfileVariablesSet> readProfile,
            Func<string> readMode,
            Func<string> readName,
            Func<string> readValue,
            Func<IEnumerable<object>> readSelection,
            Action cleanup)
        {
            var profile = readProfile();
            if (profile != null)
            {
                if (readMode() == "NewVariable")
                {
                    VariableEditorState.AddProfileVariable(
                        profile, true, readName(), readValue(), Array.Empty<object>());
                }
                else
                {
                    VariableEditorState.AddProfileVariable(
                        profile, false, null, null, readSelection());
                }
            }

            cleanup();
        }

        internal static void CancelAddVariable(
            Action clearName,
            Action clearValue,
            Action detachSelection,
            Action clearSelection,
            Action attachSelection,
            Action hide)
        {
            clearName();
            clearValue();
            detachSelection();
            clearSelection();
            attachSelection();
            hide();
        }

        internal static void FlyoutClosed(Action cancelAdd, Action disableAdd)
        {
            cancelAdd();
            disableAdd();
        }

        internal static void SelectionChanged(
            ProfileVariablesSet profile,
            int addedCount,
            Func<IEnumerable<Variable>> readSelected,
            Func<object> readFirstRemoved,
            Func<int> readRemovedCount,
            Action detach,
            Action<Variable> removeSelected,
            Action attach,
            Action updateGate)
        {
            if (addedCount > 0)
            {
                var duplicates = VariableEditorState.GetSecondSelectionsFromDuplicateNameGroups(readSelected());
                foreach (var duplicate in duplicates)
                {
                    detach();
                    removeSelected(duplicate);
                    attach();
                }
            }

            if (readRemovedCount() > 0)
            {
                var removedVariable = readFirstRemoved() as Variable;
                VariableEditorState.RemoveFirstEquivalent(profile, removedVariable);
            }

            updateGate();
        }

        internal static void LoadSelection(
            ProfileVariablesSet profile,
            IEnumerable<Variable> items,
            IEnumerable<Variable> selected,
            Action detach,
            Action<Variable> addSelected,
            Action attach)
        {
            foreach (var item in VariableEditorState.LoadSelection(profile, items, selected))
            {
                detach();
                addSelected(item);
                attach();
            }
        }

        internal static void EditValueTextChanged(
            Func<Variable> readDraft, Func<string> readText, Action updateGate)
        {
            var draft = readDraft();
            draft.Values = readText();
            draft.ValuesList = Variable.ValuesStringToValuesListItemCollection(readText());
            updateGate();
        }

        internal static void SetRawTextWithHandlerSuppressed(
            string text, Action detach, Action<string> setText, Action attach)
        {
            detach();
            setText(text);
            attach();
        }

        internal static void Reload(MainViewModel vm)
        {
            vm.LoadEnvironmentVariables();
            vm.EnvironmentState = EnvironmentState.Unchanged;
        }

        internal static void DismissWarning(MainViewModel vm)
        {
            vm.EnvironmentState = EnvironmentState.Unchanged;
        }

        internal sealed class EditTarget
        {
            internal EditTarget(Variable variable, VariablesSet set)
            {
                Variable = variable;
                Set = set;
            }

            public Variable Variable { get; set; }

            public VariablesSet Set { get; set; }
        }
    }
}
