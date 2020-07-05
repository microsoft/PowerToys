using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorPicker.Common
{
    /// <summary>
    /// Base for view models to provide property changed notifications
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
