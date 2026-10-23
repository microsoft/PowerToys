// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed record PowerScriptMetadataRow(string Key, string Label, string Value)
{
    public string AutomationId => "PowerScriptsMetadata_" + Key;
}
