// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Helpers;
using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class InformationalPreviewControl : UserControl
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(UnsupportedFilePreviewData),
            typeof(InformationalPreviewControl),
            new PropertyMetadata(null));

        public UnsupportedFilePreviewData? Source
        {
            get { return (UnsupportedFilePreviewData)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public InformationalPreviewControl()
        {
            InitializeComponent();
        }

        public string FormatFileType(string? fileType) => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileType", fileType);

        public string FormatFileSize(string? fileSize) => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileSize", fileSize);

        public string FormatFileDateModified(string? fileDateModified) => ReadableStringHelper.FormatResourceString("UnsupportedFile_DateModified", fileDateModified);
    }
}
