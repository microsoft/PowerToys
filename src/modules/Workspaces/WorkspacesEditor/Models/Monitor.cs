// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace WorkspacesEditor.Models
{
    public class Monitor
    {
        public string MonitorName { get; private set; }

        public string MonitorInstanceId { get; private set; }

        public int MonitorNumber { get; private set; }

        public int Dpi { get; private set; }

        public Rect MonitorDpiUnawareBounds { get; private set; }

        public Rect MonitorDpiAwareBounds { get; private set; }

        public Monitor(string monitorName, string monitorInstanceId, int number, int dpi, Rect dpiAwareBounds, Rect dpiUnawareBounds)
        {
            MonitorName = monitorName;
            MonitorInstanceId = monitorInstanceId;
            MonitorNumber = number;
            Dpi = dpi;
            MonitorDpiAwareBounds = dpiAwareBounds;
            MonitorDpiUnawareBounds = dpiUnawareBounds;
        }
    }
}
