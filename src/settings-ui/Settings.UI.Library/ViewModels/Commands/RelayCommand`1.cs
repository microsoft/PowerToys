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
    public class RelayCommand<T> : Microsoft.PowerToys.Settings.UI.Library.ICommand
    {
        private readonly Action<T> execute;

        private readonly Func<T, bool>? canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            // AOT-friendly: no reflection, just direct cast
            // The null-forgiving operator is safe here because we're just checking CanExecute
            return canExecute == null || canExecute((T)parameter!);
        }

        public void Execute(object? parameter)
        {
            // AOT-friendly: simple cast, no reflection or type checking
            // This matches the original main branch behavior exactly
            execute((T)parameter!);
        }

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
