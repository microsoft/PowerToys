// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AllApps.Programs;

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

public sealed class AppCache
{
    private readonly IList<Win32Program> _win32s = Win32Program.All();
    private readonly IList<UWPApplication> _uwps = UWP.All();

    public IList<Win32Program> Win32s => _win32s;

    public IList<UWPApplication> UWPs => _uwps;

    public static readonly Lazy<AppCache> Instance = new(() => new());
}
