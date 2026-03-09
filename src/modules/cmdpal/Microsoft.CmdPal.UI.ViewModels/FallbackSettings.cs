// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class FallbackSettings
{
    public bool IsEnabled { get; set; } = true;

    public bool IncludeInGlobalResults { get; set; }

    public bool ShowResultsInDedicatedSection { get; set; }

    public bool ShowResultsBeforeMainResults { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? QueryDelayMilliseconds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? MinQueryLength { get; set; }

    public FallbackSettings()
    {
    }

    public FallbackSettings(bool includeInGlobalResults, uint? queryDelayMilliseconds = null, uint? minQueryLength = null)
    {
        IncludeInGlobalResults = includeInGlobalResults;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinQueryLength = minQueryLength;
    }

    [JsonConstructor]
    public FallbackSettings(bool isEnabled, bool includeInGlobalResults, bool showResultsInDedicatedSection, bool showResultsBeforeMainResults, uint? queryDelayMilliseconds = null, uint? minQueryLength = null)
    {
        IsEnabled = isEnabled;
        IncludeInGlobalResults = includeInGlobalResults;
        ShowResultsInDedicatedSection = showResultsInDedicatedSection;
        ShowResultsBeforeMainResults = showResultsBeforeMainResults;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinQueryLength = minQueryLength;
    }
}
