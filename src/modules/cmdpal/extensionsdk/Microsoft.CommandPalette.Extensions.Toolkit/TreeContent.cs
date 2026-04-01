// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class TreeContent : BaseObservable, ITreeContent
{
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public IContent[] Children { get; set; } = [];

    public virtual IContent? RootContent { get; set => SetProperty(ref field, value); }

    public virtual IContent[] GetChildren() => Children;

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
