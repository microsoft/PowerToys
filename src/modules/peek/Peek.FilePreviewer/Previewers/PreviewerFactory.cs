// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly PreviewerDefinition[] _previewers;

        internal readonly record struct PreviewerDefinition(
            Type Type,
            Func<IFileSystemItem, bool> IsSupported,
            Func<IFileSystemItem, IPreviewer> Create);

        public PreviewerFactory()
            : this(Application.Current.GetService<IPreviewSettings>())
        {
        }

        internal PreviewerFactory(IPreviewSettings previewSettings, IEnumerable<PreviewerDefinition>? previewers = null)
        {
            // Order matters - first matching previewer wins. UnsupportedFilePreviewer matches
            // all remaining file types.
            _previewers = previewers?.ToArray() ??
            [
                new(typeof(ImagePreviewer), ImagePreviewer.IsItemSupported, item => new ImagePreviewer(item)),
                new(typeof(VideoPreviewer), VideoPreviewer.IsItemSupported, item => new VideoPreviewer(item)),
                new(typeof(AudioPreviewer), AudioPreviewer.IsItemSupported, item => new AudioPreviewer(item)),
                new(typeof(WebBrowserPreviewer), WebBrowserPreviewer.IsItemSupported, item => new WebBrowserPreviewer(item, previewSettings)),
                new(typeof(ArchivePreviewer), ArchivePreviewer.IsItemSupported, item => new ArchivePreviewer(item)),
                new(typeof(ShellPreviewHandlerPreviewer), ShellPreviewHandlerPreviewer.IsItemSupported, item => new ShellPreviewHandlerPreviewer(item)),
                new(typeof(DrivePreviewer), DrivePreviewer.IsItemSupported, item => new DrivePreviewer(item)),
                new(typeof(SpecialFolderPreviewer), SpecialFolderPreviewer.IsItemSupported, item => new SpecialFolderPreviewer(item)),
                new(typeof(UnsupportedFilePreviewer), _ => true, CreateDefaultPreviewer),
            ];
        }

        private PreviewerDefinition GetCompatiblePreviewerDefinition(IFileSystemItem item) => _previewers.First(config => config.IsSupported(item));

        /// <summary>
        /// Returns the <see cref="Type"/> of the previewer that would be used for
        /// <paramref name="item"/>, without constructing an instance. Used to decide whether the
        /// current previewer can be reused.
        /// </summary>
        public Type GetCompatiblePreviewerType(IFileSystemItem item) => GetCompatiblePreviewerDefinition(item).Type;

        /// <summary>
        /// Returns a new instance of the previewer that can handle <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The file system item to create a previewer for.</param>
        /// <returns>A new instance of a compatible previewer.</returns>
        public IPreviewer Create(IFileSystemItem item) => GetCompatiblePreviewerDefinition(item).Create(item);

        /// <summary>
        /// Returns a new instance of the default previewer for unsupported file types. This is
        /// used when a file type is not supported by any of the registered previewers or a file
        /// fails to load. It also logs a telemetry event.
        /// </summary>
        /// <param name="file">The file system item to create a default previewer for.</param>
        /// <returns>A new instance of the default previewer.</returns>
        public static IPreviewer CreateDefaultPreviewer(IFileSystemItem file)
        {
            PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { Failure = ErrorEvent.FailureType.FileNotSupported });
            return new UnsupportedFilePreviewer(file);
        }
    }
}
