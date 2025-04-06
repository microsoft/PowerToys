// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class ContentPage : Page, IContentPage
{
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public virtual IDetails? Details
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Details));
        }
    }

    public virtual IContextItem[] Commands { get; set; } = [];

    public abstract IContent[] GetContent();

    protected void RaiseItemsChanged(int totalItems = -1)
    {
        try
        {
            // TODO #181 - This is the same thing that BaseObservable has to deal with.
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}
