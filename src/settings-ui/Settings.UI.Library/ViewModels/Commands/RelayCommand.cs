// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands
{
    // Preserve for AOT - ensure command execution methods are not trimmed
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class RelayCommand : Microsoft.PowerToys.Settings.UI.Library.ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

        public void Execute(object? parameter) => _execute();

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
