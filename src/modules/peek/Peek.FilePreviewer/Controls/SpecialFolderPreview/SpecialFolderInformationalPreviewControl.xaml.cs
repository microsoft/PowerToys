// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Helpers;
using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Controls;

public sealed partial class SpecialFolderInformationalPreviewControl : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(SpecialFolderPreviewData),
        typeof(SpecialFolderInformationalPreviewControl),
        new PropertyMetadata(null));

    public SpecialFolderPreviewData? Source
    {
        get { return (SpecialFolderPreviewData)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public SpecialFolderInformationalPreviewControl()
    {
        InitializeComponent();
    }

    public string FormatFileType(string? fileType) => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileType", fileType);

    public string FormatFileSize(string? fileSize) => ReadableStringHelper.FormatResourceString("UnsupportedFile_FileSize", fileSize);

    public string FormatFileDateModified(string? fileDateModified) => ReadableStringHelper.FormatResourceString("UnsupportedFile_DateModified", fileDateModified);
}
