// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Common.UI;

namespace Peek.FilePreviewer.Previewers
{
    public class MarkdownHelper
    {
        /// <summary>
        /// Prepares temp html for the previewing
        /// </summary>
        public static string PreviewTempFile(string fileText, string filePath, string tempFolder)
        {
            string theme = ThemeManager.GetWindowsBaseColor().ToLowerInvariant();
            string markdownHTML = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(fileText, theme, filePath, ImageBlockedCallback);

            string filename = tempFolder + "\\" + Guid.NewGuid().ToString() + ".html";
            File.WriteAllText(filename, markdownHTML);
            return filename;
        }

        private static void ImageBlockedCallback()
        {
        }
    }
}
