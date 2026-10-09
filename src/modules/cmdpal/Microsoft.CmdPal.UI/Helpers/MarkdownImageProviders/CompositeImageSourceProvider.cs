// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;

internal sealed partial class CompositeImageSourceProvider : IImageSourceProvider
{
    private readonly IImageSourceProvider[] _imageProviders =
    [
        new HttpImageSourceProvider(),
        new LocalImageSourceProvider(),
        new DataImageSourceProvider()
    ];

    public Task<ImageSourceInfo> GetImageSource(string url)
    {
        var provider = _imageProviders.FirstOrDefault(p => p.ShouldUseThisProvider(url));
        if (provider == null)
        {
            throw new NotSupportedException($"No image provider found for URL: {url}");
        }

        return provider.GetImageSource(url);
    }

    public bool ShouldUseThisProvider(string url)
    {
        return _imageProviders.Any(provider => provider.ShouldUseThisProvider(url));
    }
}
