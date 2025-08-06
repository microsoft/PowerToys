// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCsWin32;

public static partial class IID
{
    public static readonly Guid ISearchManager = new Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69");
    public static readonly Guid IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    public static readonly Guid IApplicationActivationManager = new Guid("2e941141-7f97-4756-ba1d-9decde894a3d");
    public static readonly Guid IVirtualDesktopManager = new("a5cd92ff-29be-454c-8d04-d82879fb3f1b");
}
