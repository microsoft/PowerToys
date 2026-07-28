// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public sealed class WinGetPackageOperationEventArgs(WinGetPackageOperation operation) : EventArgs
{
    public WinGetPackageOperation Operation { get; } = operation;
}
