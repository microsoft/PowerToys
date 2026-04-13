// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels.Commands
{
    public partial class ButtonClickCommand : Microsoft.PowerToys.Settings.UI.Library.ICommand
    {
        private readonly Action _execute;

        public ButtonClickCommand(Action execute)
        {
            _execute = execute;
        }

        // Occurs when changes occur that affect whether or not the command should execute.
        public event EventHandler? CanExecuteChanged;

        // Defines the method that determines whether the command can execute in its current state.
        public bool CanExecute(object? parameter)
        {
            return true;
        }

        // Defines the method to be called when the command is invoked.
        public void Execute(object? parameter)
        {
            _execute();
        }

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
