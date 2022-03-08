// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This rule does not make sense for structs.")]
    public struct BatteryReportingScale
    {
        public uint Granularity;
        public uint Capacity;
    }
}
