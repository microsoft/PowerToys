// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace MonitorPower;

internal sealed class MonitorPowerProfile
{
    public string Name { get; set; } = string.Empty;

    public List<DisplayHelpers.DisplayTargetId> Targets { get; set; } = [];

    public List<DisplayHelpers.SnapshotTarget>? Layout { get; set; }
}
