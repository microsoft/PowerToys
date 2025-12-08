// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.UI.Helpers.MarkdownImageProviders;

internal interface IImageSourceProvider
{
    Task<ImageSourceInfo> GetImageSource(string url);

    bool ShouldUseThisProvider(string url);
}
