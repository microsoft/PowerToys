// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerToys.FileLocksmithUI.Services
{
    internal sealed record FileLocksmithQueryResult(
        FileLocksmithQueryStatus Status,
        IReadOnlyList<FileLocksmithProcessInfo> Processes,
        int? ExitCode = null);
}
