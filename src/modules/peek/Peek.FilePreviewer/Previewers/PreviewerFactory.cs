// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using Peek.Common.Models;

    public class PreviewerFactory
    {
        public IPreviewer? Create(File file)
        {
            // TODO: investigate performance of reflection to resolve previewer type
            switch (file.Extension)
            {
                case ".bmp":
                case ".gif":
                case ".jpg":
                case ".jfif":
                case ".jfi":
                case ".jif":
                case ".jpeg":
                case ".jpe":
                case ".png":
                case ".tif":
                case ".tiff":
                    return new ImagePreviewer(file);
                case ".html":
                    return new HtmlPreviewer(file);
                default:
                    return null;
            }
        }
    }
}
