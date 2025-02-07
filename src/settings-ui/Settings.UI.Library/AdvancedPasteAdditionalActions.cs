// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteAdditionalActions
{
    public static class PropertyNames
    {
        public const string ImageToText = "image-to-text";
        public const string PasteAsFile = "paste-as-file";
    }

    [JsonPropertyName(PropertyNames.ImageToText)]
    public AdvancedPasteAdditionalAction ImageToText { get; init; } = new();

    [JsonPropertyName(PropertyNames.PasteAsFile)]
    public AdvancedPastePasteAsFileAction PasteAsFile { get; init; } = new();

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> AllActions => new IAdvancedPasteAction[] { ImageToText, PasteAsFile }.Concat(PasteAsFile.SubActions);
}
