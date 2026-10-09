// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.WinGet.Models;

public enum WinGetPackageOperationState
{
    Queued = 0,
    Downloading = 1,
    Installing = 2,
    Uninstalling = 3,
    PostProcessing = 4,
    Succeeded = 5,
    Failed = 6,
    Canceled = 7,
}
