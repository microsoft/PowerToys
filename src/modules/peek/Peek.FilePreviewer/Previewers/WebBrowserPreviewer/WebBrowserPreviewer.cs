// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Peek.FilePreviewer.Controls;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    public partial class WebBrowserPreviewer : ObservableObject, IBrowserPreview
    {
        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
            // Web
            ".html",
            ".htm",

            // Document
            ".pdf",
        };

        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private bool isPreviewLoaded;

        public WebBrowserPreviewer(File file)
        {
            File = file;
        }

        private File File { get; }

        public Task<Size> GetPreviewSizeAsync()
        {
            // TODO: define how to proper window size on HTML content.
            var size = new Size(1280, 720);
            return Task.FromResult(size);
        }

        public Task LoadPreviewAsync()
        {
            Preview = new Uri(File.Path);

            return Task.CompletedTask;
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }
    }
}
