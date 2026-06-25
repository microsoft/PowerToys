// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class PowerModeGuids
{
    internal static readonly Guid BestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");

    internal static readonly Guid Balanced = Guid.Empty;

    internal static readonly Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");
}
