using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PropertyChanged;

namespace Wox.Plugin
{
    [ImplementPropertyChanged]
    public class BaseModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));  
        }
    }
}