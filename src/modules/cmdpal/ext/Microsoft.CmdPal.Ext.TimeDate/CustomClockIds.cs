// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal static class CustomClockIds
{
    private const string Prefix = "com.microsoft.cmdpal.timedate.customClock";

    internal const string LocalDetailPage = Prefix + ".local.page";

    internal static string GetDetailPage(Guid clockId) => $"{Prefix}.{clockId}.page";

    internal static string GetDockBand(Guid clockId) => $"{Prefix}.{clockId}.dock";
}
