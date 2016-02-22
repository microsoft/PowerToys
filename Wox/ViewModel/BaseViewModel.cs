using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wox.ViewModel
{
    public class BaseViewModel : INotifyPropertyChanged
    {

        protected void OnPropertyChanged(string propertyName)
        {
            if (null != PropertyChanged)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

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
            if (null != _action)
            {
                _action(parameter);
            }
        }
    }
}
