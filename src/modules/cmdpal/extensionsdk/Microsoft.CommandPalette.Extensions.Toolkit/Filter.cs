// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Filter : BaseObservable, IFilter
{
    public virtual IIconInfo Icon
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

= new IconInfo();

    public virtual string Id
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Id));
        }
    }

= string.Empty;

    public virtual string Name
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Name));
        }
    }

= string.Empty;
}
