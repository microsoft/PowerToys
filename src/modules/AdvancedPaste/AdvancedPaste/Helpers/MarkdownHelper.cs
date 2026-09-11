// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using AdvancedPaste.Core;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers;

internal static class MarkdownHelper
{
    internal static async Task<string> ToMarkdownAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();

        var data = clipboardData.Contains(StandardDataFormats.Html) ? await clipboardData.GetHtmlFormatAsync()
                 : clipboardData.Contains(StandardDataFormats.Text) ? await clipboardData.GetTextAsync()
                 : string.Empty;

        return string.IsNullOrEmpty(data) ? string.Empty : MarkdownConverter.Convert(data);
    }
}
