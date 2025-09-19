// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderLayoutConfig
{
    public ToolbarGroupLayoutStyle? Style { get; set; }

    public ToolbarGroupOverflowMode? Overflow { get; set; }

    public int? MaxInline { get; set; }

    public bool? ShowLabels { get; set; }
}
