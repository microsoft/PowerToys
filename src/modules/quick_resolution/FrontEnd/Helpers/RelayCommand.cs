namespace MenusWPF.Helpers
{
    using System;
    using System.Windows.Input;

    namespace WpfApp1
    {
        public class RelayCommand<T> : ICommand
        {
            private readonly Predicate<T> _canExecute;
            private readonly Action<T> _execute;

            public RelayCommand(Action<T> execute)
               : this(execute, null)
            {
                _execute = execute;
            }

            public RelayCommand(Action<T> execute, Predicate<T> canExecute)
            {
                if (execute == null)
                {
                    throw new ArgumentNullException("execute");
                }
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute == null || _canExecute((T)parameter);
            }

            public void Execute(object parameter)
            {
                _execute((T)parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }

        public class RelayCommand : ICommand
        {
            private readonly Predicate<object> _canExecute;
            private readonly Action<object> _execute;

            public RelayCommand(Action<object> execute)
               : this(execute, null)
            {
                _execute = execute;
            }

            public RelayCommand(Action<object> execute, Predicate<object> canExecute)
            {
                if (execute == null)
                {
                    throw new ArgumentNullException("execute");
                }
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute == null || _canExecute(parameter);
            }

            public void Execute(object parameter)
            {
                _execute(parameter);
            }

            // Ensures WPF commanding infrastructure asks all RelayCommand objects whether their
            // associated views should be enabled whenever a command is invoked 
            public event EventHandler CanExecuteChanged
            {
                add
                {
                    CommandManager.RequerySuggested += value;
                    CanExecuteChangedInternal += value;
                }
                remove
                {
                    CommandManager.RequerySuggested -= value;
                    CanExecuteChangedInternal -= value;
                }
            }

            private event EventHandler CanExecuteChangedInternal;

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChangedInternal.Raise(this);
            }
        }
    }

}
