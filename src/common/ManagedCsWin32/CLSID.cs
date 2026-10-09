// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCsWin32;

public static partial class CLSID
{
    public static readonly Guid SearchManager = new Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39");
    public static readonly Guid CollatorDataSource = new Guid("9E175B8B-F52A-11D8-B9A5-505054503030");
    public static readonly Guid ApplicationActivationManager = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    public static readonly Guid VirtualDesktopManager = new("aa509086-5ca9-4c25-8f95-589d3c07b48a");
    public static readonly Guid DesktopWallpaper = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");
}
