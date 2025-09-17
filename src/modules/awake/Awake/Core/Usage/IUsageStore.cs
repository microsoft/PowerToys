// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1516, SA1636
using System;
using System.Collections.Generic;
using Awake.Core.Usage.Models;

namespace Awake.Core.Usage
{
    internal interface IUsageStore : IDisposable
    {
        void AddSpan(string processName, double seconds, DateTime firstSeenUtc, DateTime lastUpdatedUtc, int retentionDays);

        IReadOnlyList<AppUsageRecord> Query(int top, int days);

        void Prune(int retentionDays);
    }
}
#pragma warning restore SA1516, SA1636
