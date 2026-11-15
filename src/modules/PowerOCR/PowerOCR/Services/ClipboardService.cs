// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

using PowerOCR.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace PowerOCR.Services;

internal sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var package = new DataPackage();
        package.SetText(text);
        await ClipboardWriteOperation.ExecuteAsync(
            () => Clipboard.SetContent(package),
            Clipboard.Flush,
            cancellationToken);
    }
}
