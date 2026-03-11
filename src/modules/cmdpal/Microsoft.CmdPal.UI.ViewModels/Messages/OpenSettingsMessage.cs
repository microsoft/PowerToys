// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.Messages;

public record OpenSettingsMessage(
    string SettingsPageTag = "",
    ExtensionSettingsNavigationRequest? ExtensionSettingsRequest = null);
