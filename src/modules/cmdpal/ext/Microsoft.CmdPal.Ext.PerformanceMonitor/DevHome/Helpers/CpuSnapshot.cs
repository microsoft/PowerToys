// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace CoreWidgetProvider.Helpers;

internal sealed record CpuSnapshot(float CpuUsage, float KernelUsage, float CpuSpeed, GraphSample[] History)
{
    public static readonly CpuSnapshot Empty = new(0, 0, 0, []);
}
