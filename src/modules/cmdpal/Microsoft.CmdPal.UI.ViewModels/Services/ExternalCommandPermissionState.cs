// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

internal sealed class ExternalCommandPermissionState
{
    public List<ExternalCommandPermission> Permissions { get; init; } = [];
}
