using System;
using System.Windows.Input;

namespace Wox.ViewModel
{
    public class RelayCommand : ICommand
    {
        private Action<object> _action;

        public RelayCommand(Action<object> action)
        {
            _action = action;
        }

        public virtual bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        public virtual void Execute(object parameter)
        {
            _action?.Invoke(parameter);
        }
    }
}
