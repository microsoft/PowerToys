// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class GalleryGridLayout : BaseObservable, IGalleryGridLayout
{
    public virtual bool ShowTitle
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(ShowTitle));
        }
    }

    = true;

    public virtual bool ShowSubtitle
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(ShowSubtitle));
        }
    }

    = true;
}
