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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (null != this.PropertyChanged)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class RelayCommand : ICommand
    {

        private Action<object> _action;

        public RelayCommand(Action<object> action)
        {
            this._action = action;
        }

        public virtual bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        public virtual void Execute(object parameter)
        {
            if (null != this._action)
            {
                this._action(parameter);
            }
        }
    }
}
