// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using AllApps.Programs;

namespace AllApps;

public sealed class AppCache
{
    internal IList<Win32Program> Win32s = AllApps.Programs.Win32Program.All();
    internal IList<UWPApplication> UWPs = Programs.UWP.All();
    public static readonly Lazy<AppCache> Instance = new(() => new());
}
