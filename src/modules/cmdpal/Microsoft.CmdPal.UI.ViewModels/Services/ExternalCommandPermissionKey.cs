// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed record ExternalCommandPermissionKey(
    ExternalCommandKind Kind,
    string PackageFamilyName,
    string ProviderId,
    string CommandId)
{
    public static ExternalCommandPermissionKey Reload { get; } = new(
        ExternalCommandKind.Reload,
        string.Empty,
        string.Empty,
        string.Empty);
}
