// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

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

    // Per-instance constructor for PowerScripts, whose supported clipboard formats vary per script
    // (derived from the script's own transform contract) rather than from the static enum metadata.
    private PasteFormat(PasteFormats format, ClipboardFormat scriptSupportedFormats, ClipboardFormat availableClipboardFormats, bool perScript)
    {
        _ = perScript;
        Format = format;
        IsEnabled = (availableClipboardFormats & scriptSupportedFormats) != ClipboardFormat.None;
    }

    public static PasteFormat CreateStandardFormat(PasteFormats format, ClipboardFormat clipboardFormats, bool isAIServiceEnabled, Func<string, string> resourceLoader) =>
        new(format, clipboardFormats, isAIServiceEnabled)
        {
            Name = MetadataDict[format].ResourceId == null ? string.Empty : resourceLoader(MetadataDict[format].ResourceId),
            Prompt = string.Empty,
            IsSavedQuery = false,
        };

    public static PasteFormat CreateCustomAIFormat(PasteFormats format, string name, string prompt, bool isSavedQuery, ClipboardFormat clipboardFormats, bool isAIServiceEnabled) =>
        new(format, clipboardFormats, isAIServiceEnabled)
        {
            Name = name,
            Prompt = prompt,
            IsSavedQuery = isSavedQuery,
        };

    /// <summary>
    /// Creates a paste format backed by a user-authored PowerScript. The script's own transform
    /// contract determines which clipboard formats it accepts, so enablement is computed per instance.
    /// </summary>
    public static PasteFormat CreatePowerScriptFormat(string scriptId, string name, ClipboardFormat scriptSupportedFormats, ClipboardFormat availableClipboardFormats) =>
        new(PasteFormats.PowerScript, scriptSupportedFormats, availableClipboardFormats, perScript: true)
        {
            Name = name,
            Prompt = string.Empty,
            IsSavedQuery = false,
            ScriptId = scriptId,
        };

    public PasteFormatMetadataAttribute Metadata => MetadataDict[Format];

    public string IconGlyph => Metadata.IconGlyph;

    public string Name { get; private init; }

    public PasteFormats Format { get; private init; }

    public string Prompt { get; private init; }

    public bool IsSavedQuery { get; private init; }

    /// <summary>The PowerScript id backing this format, when <see cref="Format"/> is <see cref="PasteFormats.PowerScript"/>.</summary>
    public string ScriptId { get; private init; }

    public bool IsEnabled { get; private init; }

    public string AccessibleName => $"{Name} ({ShortcutText})";

    public string ToolTip => string.IsNullOrEmpty(Prompt) ? $"{Name} ({ShortcutText})" : Prompt;

    public string Query => string.IsNullOrEmpty(Prompt) ? Name : Prompt;

    public string ShortcutText { get; set; } = string.Empty;

    public static bool SupportsClipboardFormats(PasteFormats format, ClipboardFormat clipboardFormats)
        => (clipboardFormats & MetadataDict[format].SupportedClipboardFormats) != ClipboardFormat.None;

    public bool SupportsClipboardFormats(ClipboardFormat clipboardFormats) => SupportsClipboardFormats(Format, clipboardFormats);
}
