// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Stable command identifiers consumed by the native performance overview.
/// </summary>
public static class NativePerformanceOverviewCommandIds
{
    public const string PreviousGpu = "com.microsoft.cmdpal.gpu_widget.prev";
    public const string NextGpu = "com.microsoft.cmdpal.gpu_widget.next";
    public const string PreviousNetwork = "com.microsoft.cmdpal.network_widget.prev";
    public const string NextNetwork = "com.microsoft.cmdpal.network_widget.next";
}
