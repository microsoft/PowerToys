// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Input;
using ManagedCommon;

namespace PowerDisplay.Commands
{
    /// <summary>
    /// Basic relay command implementation for parameterless actions
    /// </summary>
    public partial class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            try
            {
                return _canExecute.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanExecute failed: {ex.Message}");
                return false;
            }
        }

        public void Execute(object? parameter)
        {
            try
            {
                _execute();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Command execution failed: {ex.Message}");
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generic relay command implementation for parameterized actions
    /// </summary>
    /// <typeparam name="T">Type of the command parameter</typeparam>
    public partial class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            try
            {
                return _canExecute.Invoke((T?)parameter);
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanExecute<T> failed: {ex.Message}");
                return false;
            }
        }

        public void Execute(object? parameter)
        {
            try
            {
                _execute((T?)parameter);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Command<T> execution failed: {ex.Message}");
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
