// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using AdvancedPaste.Core;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers;

internal static class JsonHelper
{
    internal static async Task<string> ToJsonFromXmlOrCsvAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();

        if (!clipboardData.Contains(StandardDataFormats.Text))
        {
            Logger.LogWarning("Clipboard does not contain text data");
            return string.Empty;
        }

        try
        {
            return JsonConverter.Convert(await clipboardData.GetTextAsync());
        }
        catch (System.Exception ex)
        {
            Logger.LogError("Failed converting clipboard text to JSON", ex);
            return string.Empty;
        }
    }
}
