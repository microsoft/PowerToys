// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Models;

public partial class PasteFormat : ObservableObject
{
    [ObservableProperty]
    private string _shortcutText = string.Empty;

    [ObservableProperty]
    private string _toolTip = string.Empty;

    public PasteFormat()
    {
    }

    public PasteFormat(AdvancedPasteCustomAction customAction, string shortcutText)
    {
        IconGlyph = "\uE945";
        Name = customAction.Name;
        Prompt = customAction.Prompt;
        Format = PasteFormats.Custom;
        ShortcutText = shortcutText;
        ToolTip = customAction.Prompt;
    }

    public string IconGlyph { get; init; }

    public string Name { get; init; }

    public PasteFormats Format { get; init; }

    public string Prompt { get; init; } = string.Empty;
}
