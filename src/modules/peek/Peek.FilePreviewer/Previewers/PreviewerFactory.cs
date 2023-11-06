// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Peek.Common.Extensions;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Archives;
using Peek.UI.Telemetry.Events;

namespace Peek.FilePreviewer.Previewers
{
    public class PreviewerFactory
    {
        private readonly IPreviewSettings _previewSettings;

        public PreviewerFactory()
        {
            _previewSettings = Application.Current.GetService<IPreviewSettings>();
        }

        public IPreviewer Create(IFileSystemItem file)
        {
            if (ImagePreviewer.IsFileTypeSupported(file.Extension))
            {
                return new ImagePreviewer(file);
            }
            else if (VideoPreviewer.IsFileTypeSupported(file.Extension))
            {
                return new VideoPreviewer(file);
            }
            else if (WebBrowserPreviewer.IsFileTypeSupported(file.Extension))
            {
                return new WebBrowserPreviewer(file, _previewSettings);
            }
            else if (ArchivePreviewer.IsFileTypeSupported(file.Extension))
            {
                return new ArchivePreviewer(file);
            }
            else if (ShellPreviewHandlerPreviewer.IsFileTypeSupported(file.Extension))
            {
                return new ShellPreviewHandlerPreviewer(file);
            }

            // Other previewer types check their supported file types here
            return CreateDefaultPreviewer(file);
        }

        public IPreviewer CreateDefaultPreviewer(IFileSystemItem file)
        {
            PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { Failure = ErrorEvent.FailureType.FileNotSupported });
            return new UnsupportedFilePreviewer(file);
        }
    }
}
