// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Page : Command, IPage
{
    private bool _loading;
    private string _title = string.Empty;
    private OptionalColor _accentColor;

    public virtual bool IsLoading
    {
        get => _loading;
        set
        {
            _loading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public virtual string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public virtual OptionalColor AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            OnPropertyChanged(nameof(AccentColor));
        }
    }
}
