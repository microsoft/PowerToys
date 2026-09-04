// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.FileLocksmithUI.Services
{
    internal sealed record FileLocksmithProcessInfo(string Name, uint Pid, string User, string[] Files);
}
