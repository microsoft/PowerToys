// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteTranscodeAction : Observable, IAdvancedPasteAction
{
    public static class PropertyNames
    {
        public const string TranscodeToMp3 = "transcode-to-mp3";
        public const string TranscodeToMp4 = "transcode-to-mp4";
    }

    private AdvancedPasteAdditionalAction _transcodeToMp3 = new();
    private AdvancedPasteAdditionalAction _transcodeToMp4 = new();
    private bool _isShown = true;

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
    }

    [JsonPropertyName(PropertyNames.TranscodeToMp3)]
    public AdvancedPasteAdditionalAction TranscodeToMp3
    {
        get => _transcodeToMp3;
        init => Set(ref _transcodeToMp3, value);
    }

    [JsonPropertyName(PropertyNames.TranscodeToMp4)]
    public AdvancedPasteAdditionalAction TranscodeToMp4
    {
        get => _transcodeToMp4;
        init => Set(ref _transcodeToMp4, value);
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [TranscodeToMp3, TranscodeToMp4];
}
