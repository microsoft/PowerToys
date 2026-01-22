// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class FallbackSettings
{
    public bool IsEnabled { get; set; } = true;

    public bool IncludeInGlobalResults { get; set; }

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
