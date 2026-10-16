// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public interface IExternalCommandPermissionStore
{
    event EventHandler? PermissionsChanged;

    Task<bool> IsAllowedAsync(ExternalCommandPermissionKey key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalCommandPermission>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> RememberAsync(ExternalCommandPermission permission, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(ExternalCommandPermissionKey key, CancellationToken cancellationToken = default);

    Task<bool> ClearAsync(CancellationToken cancellationToken = default);
}
