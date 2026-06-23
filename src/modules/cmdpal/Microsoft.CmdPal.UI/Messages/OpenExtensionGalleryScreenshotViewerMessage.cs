// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Gallery;

namespace Microsoft.CmdPal.UI.Messages;

public record OpenExtensionGalleryScreenshotViewerMessage(
    IReadOnlyList<ExtensionGalleryScreenshotViewModel> Screenshots,
    ExtensionGalleryScreenshotViewModel Screenshot)
{
    public const string ConnectedAnimationKey = "ExtensionGalleryScreenshotOpenAnimation";
}
