// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class EnergySaverGuids
{
    // GUID_ENERGY_SAVER_STATUS
    internal static readonly Guid EnergySaverStatus = new("0d7a3649-ef95-4153-99fc-9d88546a5a4b");

    internal const uint StatusOff = 0;

    internal const uint StatusStandard = 1;

    internal const uint StatusHighSavings = 2;
}
