// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace FancyZonesEditor.Utils
{
    public class MonitorChangedEventArgs : EventArgs
    {
        public int LastMonitor { get; }

        public MonitorChangedEventArgs(int lastMonitor)
        {
            LastMonitor = lastMonitor;
        }
    }
}
