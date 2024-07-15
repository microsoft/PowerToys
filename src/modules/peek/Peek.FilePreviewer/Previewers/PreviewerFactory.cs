// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Peek.Common.Extensions;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Archives;
using Peek.FilePreviewer.Previewers.Drive;
using Peek.FilePreviewer.Previewers.MediaPreviewer;
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

        public IPreviewer Create(IFileSystemItem item)
        {
            if (ImagePreviewer.IsItemSupported(item))
            {
                return new ImagePreviewer(item);
            }
            else if (VideoPreviewer.IsItemSupported(item))
            {
                return new VideoPreviewer(item);
            }
            else if (AudioPreviewer.IsItemSupported(item))
            {
                return new AudioPreviewer(item);
            }
            else if (WebBrowserPreviewer.IsItemSupported(item))
            {
                return new WebBrowserPreviewer(item, _previewSettings);
            }
            else if (ArchivePreviewer.IsItemSupported(item))
            {
                return new ArchivePreviewer(item);
            }
            else if (ShellPreviewHandlerPreviewer.IsItemSupported(item))
            {
                return new ShellPreviewHandlerPreviewer(item);
            }
            else if (DrivePreviewer.IsItemSupported(item))
            {
                return new DrivePreviewer(item);
            }
            else if (SpecialFolderPreviewer.IsItemSupported(item))
            {
                return new SpecialFolderPreviewer(item);
            }

            // Other previewer types check their supported file types here
            return CreateDefaultPreviewer(item);
        }

        public IPreviewer CreateDefaultPreviewer(IFileSystemItem file)
        {
            PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { Failure = ErrorEvent.FailureType.FileNotSupported });
            return new UnsupportedFilePreviewer(file);
        }
    }
}
