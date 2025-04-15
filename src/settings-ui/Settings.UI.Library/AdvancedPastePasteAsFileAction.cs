// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPastePasteAsFileAction : Observable, IAdvancedPasteAction
{
    public static class PropertyNames
    {
        public const string PasteAsTxtFile = "paste-as-txt-file";
        public const string PasteAsPngFile = "paste-as-png-file";
        public const string PasteAsHtmlFile = "paste-as-html-file";
    }

    private AdvancedPasteAdditionalAction _pasteAsTxtFile = new();
    private AdvancedPasteAdditionalAction _pasteAsPngFile = new();
    private AdvancedPasteAdditionalAction _pasteAsHtmlFile = new();
    private bool _isShown = true;

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
    }

    [JsonPropertyName(PropertyNames.PasteAsTxtFile)]
    public AdvancedPasteAdditionalAction PasteAsTxtFile
    {
        get => _pasteAsTxtFile;
        init => Set(ref _pasteAsTxtFile, value);
    }

    [JsonPropertyName(PropertyNames.PasteAsPngFile)]
    public AdvancedPasteAdditionalAction PasteAsPngFile
    {
        get => _pasteAsPngFile;
        init => Set(ref _pasteAsPngFile, value);
    }

    [JsonPropertyName(PropertyNames.PasteAsHtmlFile)]
    public AdvancedPasteAdditionalAction PasteAsHtmlFile
    {
        get => _pasteAsHtmlFile;
        init => Set(ref _pasteAsHtmlFile, value);
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [PasteAsTxtFile, PasteAsPngFile, PasteAsHtmlFile];
}
