// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ImageContent : BaseObservable, IImageContent
{
    public const int Unlimited = -1;

    public IIconInfo? Image { get; set => SetProperty(ref field, value); }

    public int MaxHeight { get; set => SetProperty(ref field, value); } = Unlimited;

    public int MaxWidth { get; set => SetProperty(ref field, value); } = Unlimited;

    public ImageContent()
    {
    }

    public ImageContent(IIconInfo? image)
    {
        Image = image;
    }
}
