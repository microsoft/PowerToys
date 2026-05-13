// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public sealed record ExtensionSettingsNavigationRequest(
    ProviderSettingsViewModel ProviderSettingsViewModel,
    string? CommandId = null,
    ExtensionSettingsFocusTarget FocusTarget = ExtensionSettingsFocusTarget.None);
