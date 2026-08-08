// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Markdig;
using Windows.ApplicationModel.DataTransfer;
using ManagedCommon;

namespace AdvancedPaste.Helpers
{
    internal static class RichTextHelper
    {
        public static async Task<string> ToRichTextAsync(DataPackageView clipboardData)
        {
            Logger.LogTrace();

            var markdown = await clipboardData.GetTextOrEmptyAsync();
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Markdown.ToHtml(markdown, pipeline);
        }
    }
}
