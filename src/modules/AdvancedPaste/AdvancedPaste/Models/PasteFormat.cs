// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PowerScripts.Client;

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

    private PasteFormat(PowerScriptInfo script, bool isEnabled)
    {
        Format = PasteFormats.PowerScript;
        Script = script;
        IsEnabled = isEnabled;
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
    public static PasteFormat CreatePowerScriptFormat(PowerScriptInfo script, bool isEnabled) =>
        new(script, isEnabled)
        {
            Name = script.Name,
            Prompt = string.Empty,
            IsSavedQuery = false,
        };

    public PasteFormatMetadataAttribute Metadata => MetadataDict[Format];

    public string IconGlyph => Metadata.IconGlyph;

    public string Name { get; private init; }

    public PasteFormats Format { get; private init; }

    public string Prompt { get; private init; }

    public bool IsSavedQuery { get; private init; }

    /// <summary>The PowerScript id backing this format, when <see cref="Format"/> is <see cref="PasteFormats.PowerScript"/>.</summary>
    public string ScriptId => Script?.Id;

    public PowerScriptInfo Script { get; private init; }

    public string AutomationId => Script is not null ? "PowerScript_" + Script.Id : Format.ToString();

    public bool IsEnabled { get; private init; }

    public string AccessibleName => $"{Name} ({ShortcutText})";

    public string ToolTip => Script is not null ? $"PowerScripts \u00b7 {Script.Runtime} \u00b7 {Script.Input} \u2192 {Script.Output}\n{Script.Description}" :
        string.IsNullOrEmpty(Prompt) ? $"{Name} ({ShortcutText})" : Prompt;

    public string Query => string.IsNullOrEmpty(Prompt) ? Name : Prompt;

    public string ShortcutText { get; set; } = string.Empty;

    public static bool SupportsClipboardFormats(PasteFormats format, ClipboardFormat clipboardFormats)
        => (clipboardFormats & MetadataDict[format].SupportedClipboardFormats) != ClipboardFormat.None;

    public bool SupportsClipboardFormats(ClipboardFormat clipboardFormats) =>
        Script is not null ? IsEnabled : SupportsClipboardFormats(Format, clipboardFormats);
}
