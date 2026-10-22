// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class PowerScriptLocalMxcSettings
{
    public bool? Enabled { get; set; }

    public List<string> EnabledPolicies { get; set; } = new();

    public List<string> DisabledPolicies { get; set; } = new();

    public bool RiskAccepted { get; set; }
}
