// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Services;

internal sealed record PowerScriptClipboard(bool HasText, bool HasBitmap, IReadOnlyList<string> Files)
{
    public static PowerScriptClipboard Empty { get; } = new(false, false, []);

    public static async Task<PowerScriptClipboard> ReadAsync(DataPackageView data)
    {
        if (data is null)
        {
            return Empty;
        }

        var files = new List<string>();
        if (data.Contains(StandardDataFormats.StorageItems))
        {
            var items = await data.GetStorageItemsAsync();
            if (items.All(item => item is StorageFile))
            {
                files.AddRange(items.Select(item => item.Path));
            }
        }

        return new(
            data.Contains(StandardDataFormats.Text) || data.Contains(StandardDataFormats.Html),
            data.Contains(StandardDataFormats.Bitmap),
            files);
    }
}
