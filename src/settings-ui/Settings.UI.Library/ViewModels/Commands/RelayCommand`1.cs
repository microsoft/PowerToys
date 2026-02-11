// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands
{
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

        public bool CanExecute(object? parameter) => canExecute == null || canExecute((T)parameter!);

        public void Execute(object? parameter) => execute((T)parameter!);

        public void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
