// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;

namespace FancyZonesEditor.Data
{
    public class EditorParameters : EditorData
    {
        protected struct NativeMonitorData
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

        protected struct EditorParams
        {
            public int ProcessId { get; set; }

            public bool SpanZonesAcrossMonitors { get; set; }

            public List<NativeMonitorData> Monitors { get; set; }
        }

        public EditorParameters()
            : base()
        {
            File = GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\editor-parameters.json";
        }

        public ParsingResult Parse()
        {
            IOHelper ioHelper = new IOHelper();
            string data = ioHelper.ReadFile(File);

            try
            {
                EditorParams editorParams = JsonSerializer.Deserialize<EditorParams>(data, JsonOptions);

                // Process ID
                App.PowerToysPID = editorParams.ProcessId;

                // Span zones across monitors
                App.Overlay.SpanZonesAcrossMonitors = editorParams.SpanZonesAcrossMonitors;

                if (!App.Overlay.SpanZonesAcrossMonitors)
                {
                    string targetMonitorId = string.Empty;
                    string targetMonitorSerialNumber = string.Empty;
                    string targetVirtualDesktop = string.Empty;
                    int targetMonitorNumber = 0;

                    foreach (NativeMonitorData nativeData in editorParams.Monitors)
                    {
                        Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.WorkAreaWidth, nativeData.WorkAreaHeight);
                        if (nativeData.IsSelected)
                        {
                            targetMonitorId = nativeData.Monitor;
                            targetMonitorSerialNumber = nativeData.MonitorSerialNumber;
                            targetMonitorNumber = nativeData.MonitorNumber;
                            targetVirtualDesktop = nativeData.VirtualDesktop;
                        }

                        Size monitorSize = new Size(nativeData.MonitorWidth, nativeData.MonitorHeight);

                        var monitor = new Monitor(workArea, monitorSize);
                        monitor.Device.MonitorName = nativeData.Monitor;
                        monitor.Device.MonitorInstanceId = nativeData.MonitorInstanceId;
                        monitor.Device.MonitorSerialNumber = nativeData.MonitorSerialNumber;
                        monitor.Device.MonitorNumber = nativeData.MonitorNumber;
                        monitor.Device.VirtualDesktopId = nativeData.VirtualDesktop;
                        monitor.Device.Dpi = nativeData.Dpi;

                        App.Overlay.AddMonitor(monitor);
                    }

                    // Set active desktop
                    var monitors = App.Overlay.Monitors;
                    for (int i = 0; i < monitors.Count; i++)
                    {
                        var monitor = monitors[i];
                        if (monitor.Device.MonitorName == targetMonitorId &&
                            monitor.Device.MonitorSerialNumber == targetMonitorSerialNumber &&
                            monitor.Device.MonitorNumber == targetMonitorNumber &&
                            monitor.Device.VirtualDesktopId == targetVirtualDesktop)
                        {
                            App.Overlay.CurrentDesktop = i;
                            break;
                        }
                    }
                }
                else
                {
                    if (editorParams.Monitors.Count != 1)
                    {
                        return new ParsingResult(false);
                    }

                    var nativeData = editorParams.Monitors[0];
                    Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.WorkAreaWidth, nativeData.WorkAreaHeight);
                    Size monitorSize = new Size(nativeData.MonitorWidth, nativeData.MonitorHeight);

                    var monitor = new Monitor(workArea, monitorSize);
                    monitor.Device.MonitorName = nativeData.Monitor;
                    monitor.Device.MonitorInstanceId = nativeData.MonitorInstanceId;
                    monitor.Device.MonitorSerialNumber = nativeData.MonitorSerialNumber;
                    monitor.Device.MonitorNumber = nativeData.MonitorNumber;
                    monitor.Device.VirtualDesktopId = nativeData.VirtualDesktop;

                    App.Overlay.AddMonitor(monitor);
                }
            }
            catch (Exception ex)
            {
                return new ParsingResult(false, ex.Message, data);
            }

            return new ParsingResult(true);
        }
    }
}
