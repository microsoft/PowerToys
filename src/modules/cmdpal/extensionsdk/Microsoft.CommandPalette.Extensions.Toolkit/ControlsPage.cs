// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class ControlsPage : Page, IControlsPage
{
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public abstract IControlsSection[] GetSections();

    protected void RaiseItemsChanged(int totalItems = -1)
    {
        try
        {
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}
