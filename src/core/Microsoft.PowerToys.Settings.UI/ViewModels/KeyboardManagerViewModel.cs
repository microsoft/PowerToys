// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private ICommand remapKeyboardCommand;
        private ICommand editShortcutCommand;

        public ICommand RemapKeyboardCommand => remapKeyboardCommand ?? (remapKeyboardCommand = new RelayCommand(OnRemapKeyboard));

        public ICommand EditShortcutCommand => editShortcutCommand ?? (editShortcutCommand = new RelayCommand(OnEditShortcut));

        public KeyboardManagerViewModel()
        {
        }

        private void OnRemapKeyboard()
        {
            var customAction = new CustomActionDataModel
            {
                Name = "RemapKeyboard",
                Value = "Create Remap Keyboard Window",
            };
            var moduleCustomAction = new ModuleCustomAction
            {
                ModuleAction = customAction,
            };

            var sendCustomAction = new SendCustomAction("Keyboard Manager");
            sendCustomAction.Action = moduleCustomAction;
            var ipcMessage = sendCustomAction.ToJsonString();
            ShellPage.AllowRunnerToForeground();
            ShellPage.DefaultSndMSGCallback(ipcMessage);
        }

        private void OnEditShortcut()
        {
            var customAction = new CustomActionDataModel
            {
                Name = "EditShortcut",
                Value = "Create Edit Shortcut Window",
            };
            var moduleCustomAction = new ModuleCustomAction
            {
                ModuleAction = customAction,
            };

            var sendCustomAction = new SendCustomAction("Keyboard Manager");
            sendCustomAction.Action = moduleCustomAction;
            var ipcMessage = sendCustomAction.ToJsonString();
            ShellPage.AllowRunnerToForeground();
            ShellPage.DefaultSndMSGCallback(ipcMessage);
        }
    }
}
