// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Models;

public class ClipboardItem
{
    public string? Content { get; init; }

    public required ClipboardHistoryItem Item { get; init; }

    public required ISettingOptions Settings { get; init; }

    public DateTimeOffset Timestamp => Item?.Timestamp ?? DateTimeOffset.MinValue;

    public RandomAccessStreamReference? ImageData { get; set; }

    public string GetDataType()
    {
        // Check if there is valid image data
        if (IsImage)
        {
            return "Image";
        }

        // Check if there is valid text content
        return IsText ? "Text" : "Unknown";
    }

    [MemberNotNullWhen(true, nameof(ImageData))]
    internal bool IsImage => ImageData is not null;

    [MemberNotNullWhen(true, nameof(Content))]
    internal bool IsText => !string.IsNullOrEmpty(Content);
}
