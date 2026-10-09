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

    public string FormatFileType(string? fileType) => FormatField("UnsupportedFile_FileType", fileType);

    public string FormatFileSize(string? fileSize) => FormatField("UnsupportedFile_FileSize", fileSize);

    public string FormatFileDateModified(string? fileDateModified) => FormatField("UnsupportedFile_DateModified", fileDateModified);

    private static string FormatField(string resourceId, string? fieldValue)
    {
        return string.IsNullOrWhiteSpace(fieldValue) ? string.Empty : ReadableStringHelper.FormatResourceString(resourceId, fieldValue);
    }
}
