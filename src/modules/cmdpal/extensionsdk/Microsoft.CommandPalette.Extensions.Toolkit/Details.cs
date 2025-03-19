// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Details : BaseObservable, IDetails
{
    public virtual IIconInfo HeroImage
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(HeroImage));
        }
    }

= new IconInfo();

    public virtual string Title
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Title));
        }
    }

= string.Empty;

    public virtual string Body
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Body));
        }
    }

= string.Empty;

    public virtual IDetailsElement[] Metadata
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Metadata));
        }
    }

= [];
}
