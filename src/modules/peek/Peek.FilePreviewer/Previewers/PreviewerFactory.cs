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
            if (ImagePreviewer.IsFileTypeSupported(file.Extension))
            {
                return new ImagePreviewer(file);
            }

            // Other previewer types check their supported file types here
            return null;
        }
    }
}
