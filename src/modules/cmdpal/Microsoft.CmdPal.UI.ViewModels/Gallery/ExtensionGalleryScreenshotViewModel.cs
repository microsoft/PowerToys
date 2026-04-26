// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Resources = Microsoft.CmdPal.UI.ViewModels.Properties.Resources;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed class ExtensionGalleryScreenshotViewModel
{
    private static readonly CompositeFormat DisplayNameFormat
        = CompositeFormat.Parse(Resources.gallery_screenshot_display_name!);

    public ExtensionGalleryScreenshotViewModel(Uri uri, int index)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri = uri;
        Index = index;
        DisplayName = string.Format(System.Globalization.CultureInfo.CurrentCulture, DisplayNameFormat, index + 1);
    }

    public Uri Uri { get; }

    public int Index { get; }

    public string DisplayName { get; }

    public ImageSource ImageSource => field ??= CreateImageSource(Uri);

    private static ImageSource CreateImageSource(Uri uri)
    {
        BitmapImage bitmap = new();
        bitmap.DecodePixelWidth = 720;
        bitmap.UriSource = uri;
        return bitmap;
    }
}
