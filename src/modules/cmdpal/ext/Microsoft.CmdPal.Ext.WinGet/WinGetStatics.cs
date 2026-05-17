// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.WinGet;

internal static class WinGetStatics
{
    public static Func<string, ICommandItem?>? AppSearchByPackageFamilyNameCallback { get; set; }

    public static Func<string, ICommandItem?>? AppSearchByProductCodeCallback { get; set; }
}
