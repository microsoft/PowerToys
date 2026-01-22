// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// TODO! We probably want to have OnPropertyChanged raise the event
// asynchronously, so as to not block the extension app while it's being
// processed in the host app.
// (also consider this for ItemsChanged in ListPage)
public partial class BaseObservable : INotifyPropChanged
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        try
        {
            // TODO #181 - This is dangerous! If the original host goes away,
            // this can crash as we try to invoke the handlers from that process.
            // However, just catching it seems to still raise the event on the
            // new host?
            PropChanged?.Invoke(this, new PropChangedEventArgs(propertyName!));
        }
        catch
        {
        }
    }

    /// <summary>
    /// Sets the backing field to the specified value and raises a property changed
    /// notification if the value is different from the current one.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="field">A reference to the backing field for the property.</param>
    /// <param name="value">The new value to assign to the property.</param>
    /// <param name="propertyName">
    /// The name of the property. This is optional and is usually supplied
    /// automatically by the <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the field was updated and a property changed
    /// notification was raised; otherwise, <see langword="false"/>.
    /// </returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName!);
        return true;
    }
}
