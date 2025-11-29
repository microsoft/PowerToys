// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ImageContent : BaseObservable, IImageContent
{
    public IIconInfo? Image
    {
        get;
        set
        {
            if (!ReferenceEquals(value, field))
            {
                field = value;
                OnPropertyChanged(nameof(Image));
            }
        }
    }

    public int MaxHeight
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;
                OnPropertyChanged(nameof(MaxHeight));
            }
        }
    }

        = -1;

    public int MaxWidth
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;
                OnPropertyChanged(nameof(MaxWidth));
            }
        }
    }

        = -1;

    public ImageContent()
    {
    }

    public ImageContent(IIconInfo? image)
    {
        Image = image;
    }
}
