// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class PlainTextContent : BaseObservable, IPlainTextContent
{
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string? Text
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(Text));
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs());
        }
    }

    public PlainTextContent()
    {
    }

    public PlainTextContent(string? text)
    {
        Text = text;
    }
}
