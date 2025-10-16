// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Core.Models;

namespace PowerDisplay.Core.Interfaces
{
    /// <summary>
    /// Monitor list changed event arguments
    /// </summary>
    public class MonitorListChangedEventArgs : EventArgs
    {
        public IReadOnlyList<Monitor> AddedMonitors { get; }

        public IReadOnlyList<Monitor> RemovedMonitors { get; }

        public IReadOnlyList<Monitor> AllMonitors { get; }

        public MonitorListChangedEventArgs(
            IReadOnlyList<Monitor> addedMonitors,
            IReadOnlyList<Monitor> removedMonitors,
            IReadOnlyList<Monitor> allMonitors)
        {
            AddedMonitors = addedMonitors;
            RemovedMonitors = removedMonitors;
            AllMonitors = allMonitors;
        }
    }
}