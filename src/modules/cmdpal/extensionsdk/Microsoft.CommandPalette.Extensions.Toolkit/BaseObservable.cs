// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// TODO! We probably want to have OnPropertyChanged raise the event
// asynchronously, so as to not block the extension app while it's being
// processed in the host app.
// (also consider this for ItemsChanged in ListPage)
public partial class BaseObservable : INotifyPropChanged
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        try
        {
            // TODO #181 - This is dangerous! If the original host goes away,
            // this can crash as we try to invoke the handlers from that process.
            // However, just catching it seems to still raise the event on the
            // new host?
            PropChanged?.Invoke(this, new PropChangedEventArgs(propertyName));
        }
        catch
        {
        }
    }
}
