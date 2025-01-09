// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ProviderSettingsViewModel(CommandProviderWrapper _provider, ProviderSettings _providerSettings) : ObservableObject
{
    public string DisplayName => _provider.DisplayName;

    public string ExtensionName => _provider.Extension?.ExtensionDisplayName ?? "Built-in";

    public IconInfo Icon => _provider.Icon;

    public bool IsEnabled
    {
        get => _providerSettings.IsEnabled;
        set => _providerSettings.IsEnabled = value;
    }
}
