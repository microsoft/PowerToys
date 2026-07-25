// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Identifies in-box form payloads that have a native Command Palette renderer.
/// Unrecognized values continue through the normal Adaptive Cards pipeline.
/// </summary>
public static class NativeFormContentTypes
{
    public const string PerformanceOverview = "cmdpal://native/performance-overview/v1";
}
