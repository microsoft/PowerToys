// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Models;

[DebuggerDisplay("{Name} IsEnabled={IsEnabled} ShortcutText={ShortcutText}")]
public sealed class PasteFormat
{
    public static readonly IReadOnlyDictionary<PasteFormats, PasteFormatMetadataAttribute> MetadataDict =
        typeof(PasteFormats).GetFields()
                            .Where(field => field.IsLiteral)
                            .ToDictionary(field => (PasteFormats)field.GetRawConstantValue(), field => field.GetCustomAttribute<PasteFormatMetadataAttribute>());

    private PasteFormat(PasteFormats format, ClipboardFormat clipboardFormats, bool isAIServiceEnabled)
    {
        Format = format;
        IsEnabled = SupportsClipboardFormats(clipboardFormats) && (isAIServiceEnabled || !Metadata.RequiresAIService);
    }

    public PasteFormat(PasteFormats format, ClipboardFormat clipboardFormats, bool isAIServiceEnabled, Func<string, string> resourceLoader)
        : this(format, clipboardFormats, isAIServiceEnabled)
    {
        Name = Metadata.ResourceId == null ? string.Empty : resourceLoader(Metadata.ResourceId);
        Prompt = string.Empty;
    }

    public PasteFormat(AdvancedPasteCustomAction customAction, ClipboardFormat clipboardFormats, bool isAIServiceEnabled)
        : this(PasteFormats.Custom, clipboardFormats, isAIServiceEnabled)
    {
        Name = customAction.Name;
        Prompt = customAction.Prompt;
    }

    public PasteFormatMetadataAttribute Metadata => MetadataDict[Format];

    public string IconGlyph => Metadata.IconGlyph;

    public string Name { get; private init; }

    public PasteFormats Format { get; private init; }

    public string Prompt { get; private init; }

    public bool IsEnabled { get; private init; }

    public double Opacity => IsEnabled ? 1 : 0.5;

    public string ToolTip => string.IsNullOrEmpty(Prompt) ? $"{Name} ({ShortcutText})" : Prompt;

    public string Query => string.IsNullOrEmpty(Prompt) ? Name : Prompt;

    public string ShortcutText { get; set; } = string.Empty;

    public bool SupportsClipboardFormats(ClipboardFormat clipboardFormats) => (clipboardFormats & Metadata.SupportedClipboardFormats) != ClipboardFormat.None;
}
