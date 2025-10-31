// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class FallbackSettings
{
    public bool IsEnabled { get; set; } = true;

    public bool IncludeInGlobalResults { get; set; }

    public int WeightBoost { get; set; }

    public FallbackSettings()
    {
    }

    [JsonConstructor]
    public FallbackSettings(bool isEnabled, int weightBoost, bool includeInGlobalResults)
    {
        IsEnabled = isEnabled;
        WeightBoost = weightBoost;
        IncludeInGlobalResults = includeInGlobalResults;
    }
}
