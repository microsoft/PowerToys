// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace WorkspacesEditor.Models
{
    public class Monitor(string monitorName, string monitorInstanceId, int number, int dpi, Rect dpiAwareBounds, Rect dpiUnawareBounds)
    {
        public string MonitorName { get; private set; } = monitorName;

        public string MonitorInstanceId { get; private set; } = monitorInstanceId;

        public int MonitorNumber { get; private set; } = number;

        public int Dpi { get; private set; } = dpi;

        public Rect MonitorDpiUnawareBounds { get; private set; } = dpiUnawareBounds;

        public Rect MonitorDpiAwareBounds { get; private set; } = dpiAwareBounds;
    }
}
