// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace WorkspacesEditor.Models
{
    public partial class MonitorSetup : Monitor
    {
        public string MonitorInfo => MonitorName;

        public string MonitorInfoWithResolution => $"{MonitorName}    {MonitorDpiAwareBounds.Width}x{MonitorDpiAwareBounds.Height}";

        public MonitorSetup(string monitorName, string monitorInstanceId, int number, int dpi, Rect dpiAwareBounds, Rect dpiUnawareBounds)
            : base(monitorName, monitorInstanceId, number, dpi, dpiAwareBounds, dpiUnawareBounds)
        {
        }

        public MonitorSetup(MonitorSetup other)
            : base(other.MonitorName, other.MonitorInstanceId, other.MonitorNumber, other.Dpi, other.MonitorDpiAwareBounds, other.MonitorDpiUnawareBounds)
        {
        }
    }
}
