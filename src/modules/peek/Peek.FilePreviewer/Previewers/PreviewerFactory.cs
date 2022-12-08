// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System.Threading;
    using Peek.Common.Models;

    public class PreviewerFactory
    {
        public IPreviewer Create(File file, CancellationToken cancellationToken)
        {
            if (ImagePreviewer.IsFileTypeSupported(file.Extension))
            {
                return new ImagePreviewer(file, cancellationToken);
            }
            else if (HtmlPreviewer.IsFileTypeSupported(file.Extension))
            {
                return new HtmlPreviewer(file, cancellationToken);
            }

            // Other previewer types check their supported file types here
            return CreateDefaultPreviewer(file, cancellationToken);
        }

        public IPreviewer CreateDefaultPreviewer(File file, CancellationToken cancellationToken)
        {
            return new UnsupportedFilePreviewer(file, cancellationToken);
        }
    }
}
