// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AllApps.Programs;

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

public sealed class AppCache
{
    internal IList<Win32Program> Win32s = Win32Program.All();
    internal IList<UWPApplication> UWPs = UWP.All();
    public static readonly Lazy<AppCache> Instance = new(() => new());
}
