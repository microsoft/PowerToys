// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FancyZonesEditorCommon.Data
{
    public class EditorParameters : EditorData<EditorParameters.ParamsWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\editor-parameters.json";
            }
        }

        public struct NativeMonitorDataWrapper
        {
            public string Monitor { get; set; }

            public string MonitorInstanceId { get; set; }

            public string MonitorSerialNumber { get; set; }

            public int MonitorNumber { get; set; }

            public string VirtualDesktop { get; set; }

            public int Dpi { get; set; }

            public int LeftCoordinate { get; set; }

            public int TopCoordinate { get; set; }

            public int WorkAreaWidth { get; set; }

            public int WorkAreaHeight { get; set; }

            public int MonitorWidth { get; set; }

            public int MonitorHeight { get; set; }

            public bool IsSelected { get; set; }

            public override string ToString()
            {
                var sb = new StringBuilder();

                // using CultureInfo.InvariantCulture since this is internal data
                sb.Append("Monitor: ");
                sb.AppendLine(Monitor);
                sb.Append("Virtual desktop: ");
                sb.AppendLine(VirtualDesktop);
                sb.Append("DPI: ");
                sb.AppendLine(Dpi.ToString(CultureInfo.InvariantCulture));

                sb.Append("X: ");
                sb.AppendLine(LeftCoordinate.ToString(CultureInfo.InvariantCulture));
                sb.Append("Y: ");
                sb.AppendLine(TopCoordinate.ToString(CultureInfo.InvariantCulture));

                sb.Append("Width: ");
                sb.AppendLine(MonitorWidth.ToString(CultureInfo.InvariantCulture));
                sb.Append("Height: ");
                sb.AppendLine(MonitorHeight.ToString(CultureInfo.InvariantCulture));

                return sb.ToString();
            }
        }

        public struct ParamsWrapper
        {
            public int ProcessId { get; set; }

            public bool SpanZonesAcrossMonitors { get; set; }

            public List<NativeMonitorDataWrapper> Monitors { get; set; }
        }

        public EditorParameters()
            : base()
        {
        }
    }
}
