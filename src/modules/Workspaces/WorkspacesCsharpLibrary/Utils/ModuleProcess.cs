// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

namespace WorkspacesCsharpLibrary.Utils;

public static class ModuleProcess
{
    public static ProcessStartInfo CreateStartInfo(string executableName, string arguments)
    {
        return new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, executableName), arguments)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            CreateNoWindow = true,
        };
    }
}
