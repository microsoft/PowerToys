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
            switch (file.Extension.ToLower())
            {
                // Image types
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
                case ".dib":
                case ".heic":
                case ".heif":
                case ".hif":
                case ".avif":
                case ".jxr":
                case ".wdp":
                case ".ico":
                case ".thumb":

                // Raw types
                case ".arw":
                case ".cr2":
                case ".crw":
                case ".erf":
                case ".kdc":
                case ".mrw":
                case ".nef":
                case ".nrw":
                case ".orf":
                case ".pef":
                case ".raf":
                case ".raw":
                case ".rw2":
                case ".rwl":
                case ".sr2":
                case ".srw":
                case ".srf":
                case ".dcs":
                case ".dcr":
                case ".drf":
                case ".k25":
                case ".3fr":
                case ".ari":
                case ".bay":
                case ".cap":
                case ".iiq":
                case ".eip":
                case ".fff":
                case ".mef":
                case ".mdc":
                case ".mos":
                case ".R3D":
                case ".rwz":
                case ".x3f":
                case ".ori":
                case ".cr3":
                    return new ImagePreviewer(file);
                default:
                    return null;
            }
        }
    }
}
