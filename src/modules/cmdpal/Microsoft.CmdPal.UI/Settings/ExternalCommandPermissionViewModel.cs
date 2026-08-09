// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.Settings;

public sealed class ExternalCommandPermissionViewModel
{
    public ExternalCommandPermissionViewModel(ExternalCommandPermission permission)
    {
        Permission = permission;
    }

    public ExternalCommandPermission Permission { get; }

    public string CommandName => Permission.CommandName;

    public string ProviderName => Permission.ProviderName;

    public string ProviderId => Permission.Key.ProviderId;

    public string CommandId => Permission.Key.CommandId;

    public bool HasCommandDetails => !string.IsNullOrEmpty(ProviderId) || !string.IsNullOrEmpty(CommandId);
}
