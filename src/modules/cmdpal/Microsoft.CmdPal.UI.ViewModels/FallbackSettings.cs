// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record FallbackSettings
{
    public bool IsEnabled { get; init; } = true;

    public bool IncludeInGlobalResults { get; init; }

    public uint? QueryDelayMilliseconds { get; init; }

    public uint? MinimumQueryLength { get; init; }

    public uint? MaximumVisibleItemCount { get; init; }

    public FallbackSettings()
    {
    }

    public FallbackSettings(
        bool isBuiltIn,
        uint? queryDelayMilliseconds = null,
        uint? minimumQueryLength = null,
        uint? maximumVisibleItemCount = null)
    {
        IncludeInGlobalResults = isBuiltIn;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinimumQueryLength = minimumQueryLength;
        MaximumVisibleItemCount = maximumVisibleItemCount;
    }

    [JsonConstructor]
    public FallbackSettings(
        bool isEnabled,
        bool includeInGlobalResults,
        uint? queryDelayMilliseconds,
        uint? minimumQueryLength,
        uint? maximumVisibleItemCount)
    {
        IsEnabled = isEnabled;
        IncludeInGlobalResults = includeInGlobalResults;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinimumQueryLength = minimumQueryLength;
        MaximumVisibleItemCount = maximumVisibleItemCount;
    }
}
