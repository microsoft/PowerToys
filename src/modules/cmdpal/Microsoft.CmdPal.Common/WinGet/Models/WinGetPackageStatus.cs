// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.WinGet.Models;

public sealed record WinGetPackageStatus(
    bool IsInstalled,
    bool IsInstalledStateKnown,
    bool IsUpdateAvailable,
    bool IsUpdateStateKnown);
