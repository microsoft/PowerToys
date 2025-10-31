// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace LanguageModelProvider;

public class ModelDetails
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public long Size { get; set; }

    public bool IsUserAdded { get; set; }

    public string Icon { get; set; } = string.Empty;

    public List<HardwareAccelerator> HardwareAccelerators { get; set; } = [];

    public bool SupportedOnQualcomm { get; set; }

    public string License { get; set; } = string.Empty;

    public object? ProviderModelDetails { get; set; }
}
