// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record FallbackSettings
{
    public bool IsEnabled { get; init; } = true;

    public bool IncludeInGlobalResults { get; init; }

    public FallbackSettings()
    {
    }

    public FallbackSettings(bool isBuiltIn)
    {
        IncludeInGlobalResults = isBuiltIn;
    }

    [JsonConstructor]
    public FallbackSettings(bool isEnabled, bool includeInGlobalResults)
    {
        IsEnabled = isEnabled;
        IncludeInGlobalResults = includeInGlobalResults;
    }
}
