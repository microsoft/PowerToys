// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.Extensions.Helpers;

// TODO! We probably want to have OnPropertyChanged raise the event
// asynchonously, so as to not block the extension app while it's being
// processed in the host app.
public class BaseObservable : INotifyPropChanged
{
    public event TypedEventHandler<object, PropChangedEventArgs>? PropChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        if (PropChanged != null)
        {
            PropChanged.Invoke(this, new PropChangedEventArgs(propertyName));
        }
    }
}
