// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record FallbackSettings
{
    /// <summary>
    /// Largest query delay, in milliseconds, that a user or an extension can ask for.
    /// </summary>
    public const uint MaximumQueryDelayMilliseconds = 2000;

    /// <summary>
    /// Largest minimum query length that a user or an extension can ask for.
    /// </summary>
    public const uint MaximumMinimumQueryLength = 100;

    /// <summary>
    /// Smallest number of items that one fallback can show at one time.
    /// </summary>
    public const uint MinimumItemCount = 1;

    /// <summary>
    /// Largest number of items that one fallback can show at one time.
    /// </summary>
    public const uint MaximumItemCount = 100;

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
