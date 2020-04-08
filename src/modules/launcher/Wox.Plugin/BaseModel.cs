using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using JetBrains.Annotations;

namespace Wox.Plugin
{
    public class BaseModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            else
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }));
        }
    }
}