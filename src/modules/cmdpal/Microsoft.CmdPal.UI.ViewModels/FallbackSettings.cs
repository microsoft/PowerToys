// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record FallbackSettings
{
    /// <summary>
    /// Largest query delay, in milliseconds, that a user can ask for.
    /// </summary>
    public const uint MaximumQueryDelayMilliseconds = 2000;

    /// <summary>
    /// Largest minimum query length that a user can ask for.
    /// </summary>
    public const uint MaximumMinimumQueryLength = 100;

    public bool IsEnabled { get; init; } = true;

    public bool IncludeInGlobalResults { get; init; }

    public uint? QueryDelayMilliseconds { get; init; }

    public uint? MinimumQueryLength { get; init; }

    public FallbackSettings()
    {
    }

    public FallbackSettings(
        bool isBuiltIn,
        uint? queryDelayMilliseconds = null,
        uint? minimumQueryLength = null)
    {
        IncludeInGlobalResults = isBuiltIn;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinimumQueryLength = minimumQueryLength;
    }

    [JsonConstructor]
    public FallbackSettings(
        bool isEnabled,
        bool includeInGlobalResults,
        uint? queryDelayMilliseconds,
        uint? minimumQueryLength)
    {
        IsEnabled = isEnabled;
        IncludeInGlobalResults = includeInGlobalResults;
        QueryDelayMilliseconds = queryDelayMilliseconds;
        MinimumQueryLength = minimumQueryLength;
    }
}
