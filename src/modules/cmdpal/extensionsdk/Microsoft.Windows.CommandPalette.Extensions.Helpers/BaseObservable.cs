using Windows.Foundation;

namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

// TODO! We probably want to have OnPropertyChanged raise the event
// asynchonously, so as to not block the extension app while it's being
// processed in the host app.
public class BaseObservable : INotifyPropChanged
{
    public event TypedEventHandler<object, PropChangedEventArgs>? PropChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        if (PropChanged != null)
            PropChanged.Invoke(this, new Microsoft.Windows.CommandPalette.Extensions.PropChangedEventArgs(propertyName));
    }
}
