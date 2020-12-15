// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor.Utils
{
    public class FancyZonesEditorIO
    {
        // Non-localizable strings: JSON tags
        private const string BlankJsonTag = "blank";
        private const string FocusJsonTag = "focus";
        private const string ColumnsJsonTag = "columns";
        private const string RowsJsonTag = "rows";
        private const string GridJsonTag = "grid";
        private const string PriorityGridJsonTag = "priority-grid";
        private const string CustomJsonTag = "custom";

        // Non-localizable strings: Files
        private const string ZonesSettingsFile = "\\Microsoft\\PowerToys\\FancyZones\\zones-settings.json";

        // Non-localizable string: Multi-monitor id
        private const string MultiMonitorId = "FancyZones#MultiMonitorDevice";

        private readonly IFileSystem _fileSystem = new FileSystem();

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DashCaseNamingPolicy(),
        };

        public string FancyZonesSettingsFile { get; private set; }

        private enum CmdArgs
        {
            PowerToysPID = 0,
            SpanZones,
            TargetMonitorId,
            MonitorsCount,
            MonitorId,
            DPI,
            MonitorLeft,
            MonitorTop,
        }

        private struct NativeMonitorData
        {
            public string Id { get; set; }

            public int Dpi { get; set; }

            public int X { get; set; }

            public int Y { get; set; }

            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.Append("ID: ");
                sb.AppendLine(Id);
                sb.Append("DPI: ");
                sb.AppendLine(Dpi.ToString());

                sb.Append("X: ");
                sb.AppendLine(X.ToString());
                sb.Append("Y: ");
                sb.AppendLine(Y.ToString());

                return sb.ToString();
            }
        }

        private struct DeviceWrapper
        {
            public struct ActiveZoneSetWrapper
            {
                public string Uuid { get; set; }

                public string Type { get; set; }
            }

            public string DeviceId { get; set; }

            public ActiveZoneSetWrapper ActiveZoneset { get; set; }

            public bool EditorShowSpacing { get; set; }

            public int EditorSpacing { get; set; }

            public int EditorZoneCount { get; set; }

            public int EditorSensitivityRadius { get; set; }
        }

        private struct CanvasInfoWrapper
        {
            public struct CanvasZoneWrapper
            {
                public int X { get; set; }

                public int Y { get; set; }

            public int Width { get; set; }

                public int Height { get; set; }
            }

            public int RefWidth { get; set; }

            public int RefHeight { get; set; }

            public List<CanvasZoneWrapper> Zones { get; set; }
        }

        private struct GridInfoWrapper
        {
            public int Rows { get; set; }

            public int Columns { get; set; }

            public List<int> RowsPercentage { get; set; }

            public List<int> ColumnsPercentage { get; set; }

            public int[][] CellChildMap { get; set; }
        }

        private struct CustomLayoutWrapper
        {
            public string Uuid { get; set; }

            public string Name { get; set; }

            public string Type { get; set; }

            public JsonElement Info { get; set; } // CanvasInfoWrapper or GridInfoWrapper
        }

        private struct ZoneSettingsWrapper
        {
            public List<DeviceWrapper> Devices { get; set; }

            public List<CustomLayoutWrapper> CustomZoneSets { get; set; }
        }

        public FancyZonesEditorIO()
        {
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            FancyZonesSettingsFile = localAppDataDir + ZonesSettingsFile;
        }

        // All strings in this function shouldn't be localized.
        public static void ParseCommandLineArguments()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length < 2 && !App.DebugMode)
            {
                MessageBox.Show(Properties.Resources.Error_Not_Standalone_App, Properties.Resources.Error_Message_Box_Title);
                ((App)Application.Current).Shutdown();
            }

            try
            {
                /*
                * Divider: /
                * Parts:
                * (1) Process id
                * (2) Span zones across monitors
                * (3) Monitor id where the Editor should be opened
                * (4) Monitors count
                *
                * Data for each monitor:
                * (5) Monitor id
                * (6) DPI
                * (7) monitor left
                * (8) monitor top
                * ...
                */
                var argsParts = args[1].Split('/');

                // Process ID
                App.PowerToysPID = int.Parse(argsParts[(int)CmdArgs.PowerToysPID]);

                // Span zones across monitors
                App.Overlay.SpanZonesAcrossMonitors = int.Parse(argsParts[(int)CmdArgs.SpanZones]) == 1;

                if (!App.Overlay.SpanZonesAcrossMonitors)
                {
                    // Target monitor id
                    string targetMonitorName = argsParts[(int)CmdArgs.TargetMonitorId];

                    // Test launch with custom monitors configuration
                    bool isCustomMonitorConfigurationMode = targetMonitorName.StartsWith("Monitor#");
                    if (isCustomMonitorConfigurationMode)
                    {
                        App.Overlay.Monitors.Clear();
                    }

                    // Monitors count
                    int count = int.Parse(argsParts[(int)CmdArgs.MonitorsCount]);
                    if (count != App.Overlay.DesktopsCount && !isCustomMonitorConfigurationMode)
                    {
                        MessageBox.Show(Properties.Resources.Error_Invalid_Arguments, Properties.Resources.Error_Message_Box_Title);
                        ((App)Application.Current).Shutdown();
                    }

                    double primaryMonitorDPI = 96f;
                    double minimalUsedMonitorDPI = double.MaxValue;

                    // Parse the native monitor data
                    List<NativeMonitorData> nativeMonitorData = new List<NativeMonitorData>();
                    const int monitorArgsCount = 4;
                    for (int i = 0; i < count; i++)
                    {
                        var nativeData = default(NativeMonitorData);
                        nativeData.Id = argsParts[(int)CmdArgs.MonitorId + (i * monitorArgsCount)];
                        nativeData.Dpi = int.Parse(argsParts[(int)CmdArgs.DPI + (i * monitorArgsCount)]);
                        nativeData.X = int.Parse(argsParts[(int)CmdArgs.MonitorLeft + (i * monitorArgsCount)]);
                        nativeData.Y = int.Parse(argsParts[(int)CmdArgs.MonitorTop + (i * monitorArgsCount)]);

                        nativeMonitorData.Add(nativeData);

                        if (nativeData.X == 0 && nativeData.Y == 0)
                        {
                            primaryMonitorDPI = nativeData.Dpi;
                        }

                        if (minimalUsedMonitorDPI > nativeData.Dpi)
                        {
                            minimalUsedMonitorDPI = nativeData.Dpi;
                        }
                    }

                    var monitors = App.Overlay.Monitors;
                    double identifyScaleFactor = minimalUsedMonitorDPI / primaryMonitorDPI;
                    double scaleFactor = 96f / primaryMonitorDPI;

                    // Update monitors data
                    if (isCustomMonitorConfigurationMode)
                    {
                        foreach (NativeMonitorData nativeData in nativeMonitorData)
                        {
                            var splittedId = nativeData.Id.Split('_');
                            int width = int.Parse(splittedId[1]);
                            int height = int.Parse(splittedId[2]);

                            Rect bounds = new Rect(nativeData.X, nativeData.Y, width, height);
                            bool isPrimary = nativeData.X == 0 && nativeData.Y == 0;

                            Monitor monitor = new Monitor(bounds, bounds, isPrimary);
                            monitor.Device.Id = nativeData.Id;
                            monitor.Device.Dpi = nativeData.Dpi;

                            monitors.Add(monitor);
                        }
                    }
                    else
                    {
                        foreach (Monitor monitor in monitors)
                        {
                            bool matchFound = false;
                            monitor.Scale(scaleFactor);

                            double scaledBoundX = (int)(monitor.Device.UnscaledBounds.X * identifyScaleFactor);
                            double scaledBoundY = (int)(monitor.Device.UnscaledBounds.Y * identifyScaleFactor);

                            foreach (NativeMonitorData nativeData in nativeMonitorData)
                            {
                                // Can't do an exact match since the rounding algorithm used by the framework is different from ours
                                if (scaledBoundX >= (nativeData.X - 1) && scaledBoundX <= (nativeData.X + 1) &&
                                    scaledBoundY >= (nativeData.Y - 1) && scaledBoundY <= (nativeData.Y + 1))
                                {
                                    monitor.Device.Id = nativeData.Id;
                                    monitor.Device.Dpi = nativeData.Dpi;
                                    matchFound = true;
                                    break;
                                }
                            }

                            if (matchFound == false)
                            {
                                MessageBox.Show(string.Format(Properties.Resources.Error_Monitor_Match_Not_Found, monitor.Device.UnscaledBounds.ToString()));
                            }
                        }
                    }

                    // Set active desktop
                    for (int i = 0; i < monitors.Count; i++)
                    {
                        var monitor = monitors[i];
                        if (monitor.Device.Id == targetMonitorName)
                        {
                            App.Overlay.CurrentDesktop = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show(Properties.Resources.Error_Invalid_Arguments, Properties.Resources.Error_Message_Box_Title);
                ((App)Application.Current).Shutdown();
            }
        }

        public void ParseZoneSettings()
        {
            try
            {
                if (_fileSystem.File.Exists(FancyZonesSettingsFile))
                {
                    Stream inputStream = _fileSystem.File.Open(FancyZonesSettingsFile, FileMode.Open);
                    StreamReader reader = new StreamReader(inputStream);
                    string data = reader.ReadToEnd();
                    inputStream.Close();

                    var zoneSettings = JsonSerializer.Deserialize<ZoneSettingsWrapper>(data, _options);

                    // Set devices
                    var monitors = App.Overlay.Monitors;
                    foreach (var device in zoneSettings.Devices)
                    {
                        var settings = new LayoutSettings
                        {
                            ZonesetUuid = device.ActiveZoneset.Uuid,
                            ShowSpacing = device.EditorShowSpacing,
                            Spacing = device.EditorSpacing,
                            Type = JsonTagToLayoutType(device.ActiveZoneset.Type),
                            ZoneCount = device.EditorZoneCount,
                            SensitivityRadius = device.EditorSensitivityRadius,
                        };

                        if (!App.Overlay.SpanZonesAcrossMonitors)
                        {
                            foreach (Monitor monitor in monitors)
                            {
                                if (monitor.Device.Id == device.DeviceId)
                                {
                                    monitor.Settings = settings;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            bool isLayoutMultiMonitor = device.DeviceId.StartsWith(MultiMonitorId);
                            if (isLayoutMultiMonitor)
                            {
                                // one zoneset for all desktops
                                App.Overlay.Monitors[App.Overlay.CurrentDesktop].Settings = settings;
                                App.Overlay.Monitors[App.Overlay.CurrentDesktop].Device.Id = device.DeviceId;
                                break;
                            }
                        }
                    }

                    // Set layouts
                    MainWindowSettingsModel.CustomModels.Clear();
                    MainWindowSettingsModel.CustomModels.Add(MainWindowSettingsModel.BlankModel);
                    foreach (var zoneSet in zoneSettings.CustomZoneSets)
                    {
                        LayoutModel layout;
                        if (zoneSet.Type == CanvasLayoutModel.ModelTypeID)
                        {
                            var info = JsonSerializer.Deserialize<CanvasInfoWrapper>(zoneSet.Info.GetRawText(), _options);

                            var zones = new List<Int32Rect>();
                            foreach (var zone in info.Zones)
                            {
                                zones.Add(new Int32Rect { X = (int)zone.X, Y = (int)zone.Y, Width = (int)zone.Width, Height = (int)zone.Height });
                            }

                            layout = new CanvasLayoutModel(zoneSet.Uuid, zoneSet.Name, LayoutType.Custom, zones, info.RefWidth, info.RefHeight);
                        }
                        else if (zoneSet.Type == GridLayoutModel.ModelTypeID)
                        {
                            var info = JsonSerializer.Deserialize<GridInfoWrapper>(zoneSet.Info.GetRawText(), _options);

                            var cells = new int[info.Rows, info.Columns];
                            for (int row = 0; row < info.Rows; row++)
                            {
                                for (int column = 0; column < info.Columns; column++)
                                {
                                    cells[row, column] = info.CellChildMap[row][column];
                                }
                            }

                            layout = new GridLayoutModel(zoneSet.Uuid, zoneSet.Name, LayoutType.Custom, info.Rows, info.Columns, info.RowsPercentage, info.ColumnsPercentage, cells);
                        }
                        else
                        {
                            // Error
                            continue;
                        }

                        MainWindowSettingsModel.CustomModels.Add(layout);
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Parsing_Device_Info, ex);
            }
        }

        public void SerializeZoneSettings()
        {
            ZoneSettingsWrapper zoneSettings = new ZoneSettingsWrapper { };
            zoneSettings.Devices = new List<DeviceWrapper>();
            zoneSettings.CustomZoneSets = new List<CustomLayoutWrapper>();

            // Serialize devices
            foreach (var monitor in App.Overlay.Monitors)
            {
                LayoutSettings zoneset = monitor.Settings;
                if (zoneset.ZonesetUuid.Length == 0)
                {
                    continue;
                }

                zoneSettings.Devices.Add(new DeviceWrapper
                {
                    DeviceId = monitor.Device.Id,
                    ActiveZoneset = new ActiveZoneSetWrapper
                    {
                        Uuid = zoneset.ZonesetUuid,
                        Type = LayoutTypeToJsonTag(zoneset.Type),
                    },
                    EditorShowSpacing = zoneset.ShowSpacing,
                    EditorSpacing = zoneset.Spacing,
                    EditorZoneCount = zoneset.ZoneCount,
                    EditorSensitivityRadius = zoneset.SensitivityRadius,
                });
            }

            // Serialize custom zonesets
            foreach (LayoutModel layout in MainWindowSettingsModel.CustomModels)
            {
                if (layout.Type == LayoutType.Blank)
                {
                    continue;
                }

                JsonElement info;
                string type;

                if (layout is CanvasLayoutModel)
                {
                    type = CanvasLayoutModel.ModelTypeID;
                    var canvasLayout = layout as CanvasLayoutModel;

                    var canvasRect = canvasLayout.CanvasRect;
                    if (canvasRect.Width == 0 || canvasRect.Height == 0)
                    {
                        canvasRect = App.Overlay.WorkArea;
                    }

                    var wrapper = new CanvasInfoWrapper
                    {
                        RefWidth = (int)canvasRect.Width,
                        RefHeight = (int)canvasRect.Height,
                        Zones = new List<CanvasZoneWrapper>(),
                    };

                    foreach (var zone in canvasLayout.Zones)
                    {
                        wrapper.Zones.Add(new CanvasZoneWrapper
                        {
                            X = zone.X,
                            Y = zone.Y,
                            Width = zone.Width,
                            Height = zone.Height,
                        });
                    }

                    string json = JsonSerializer.Serialize(wrapper, _options);
                    info = JsonSerializer.Deserialize<JsonElement>(json);
                }
                else if (layout is GridLayoutModel)
                {
                    type = GridLayoutModel.ModelTypeID;
                    var gridLayout = layout as GridLayoutModel;

                    var cells = new int[gridLayout.Rows][];
                    for (int row = 0; row < gridLayout.Rows; row++)
                    {
                        cells[row] = new int[gridLayout.Columns];
                        for (int column = 0; column < gridLayout.Columns; column++)
                        {
                            cells[row][column] = gridLayout.CellChildMap[row, column];
                        }
                    }

                    var wrapper = new GridInfoWrapper
                    {
                        Rows = gridLayout.Rows,
                        Columns = gridLayout.Columns,
                        RowsPercentage = gridLayout.RowPercents,
                        ColumnsPercentage = gridLayout.ColumnPercents,
                        CellChildMap = cells,
                    };

                    string json = JsonSerializer.Serialize(wrapper, _options);
                    info = JsonSerializer.Deserialize<JsonElement>(json);
                }
                else
                {
                    // Error
                    continue;
                }

                CustomLayoutWrapper customLayout = new CustomLayoutWrapper
                {
                    Uuid = layout.Uuid,
                    Name = layout.Name,
                    Type = type,
                    Info = info,
                };

                zoneSettings.CustomZoneSets.Add(customLayout);
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(zoneSettings, _options);
                _fileSystem.File.WriteAllText(FancyZonesSettingsFile, jsonString);
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        private LayoutType JsonTagToLayoutType(string tag)
        {
            LayoutType type = LayoutType.Blank;
            switch (tag)
            {
                case FocusJsonTag:
                    type = LayoutType.Focus;
                    break;
                case ColumnsJsonTag:
                    type = LayoutType.Columns;
                    break;
                case RowsJsonTag:
                    type = LayoutType.Rows;
                    break;
                case GridJsonTag:
                    type = LayoutType.Grid;
                    break;
                case PriorityGridJsonTag:
                    type = LayoutType.PriorityGrid;
                    break;
                case CustomJsonTag:
                    type = LayoutType.Custom;
                    break;
            }

            return type;
        }

        private string LayoutTypeToJsonTag(LayoutType type)
        {
            return type switch
            {
                LayoutType.Blank => BlankJsonTag,
                LayoutType.Focus => FocusJsonTag,
                LayoutType.Rows => RowsJsonTag,
                LayoutType.Columns => ColumnsJsonTag,
                LayoutType.Grid => GridJsonTag,
                LayoutType.PriorityGrid => PriorityGridJsonTag,
                LayoutType.Custom => CustomJsonTag,
                _ => string.Empty,
            };
        }

        private static string ParsingCmdArgsErrorReport(string args, int count, string targetMonitorName, List<NativeMonitorData> monitorData, List<Monitor> monitors)
        {
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(" ## Command-line arguments:");
            sb.AppendLine();
            sb.AppendLine(args);

            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(" ## Parsed command-line arguments:");
            sb.AppendLine();

            sb.Append("Span zones across monitors: ");
            sb.AppendLine(App.Overlay.SpanZonesAcrossMonitors.ToString());
            sb.Append("Monitors count: ");
            sb.AppendLine(count.ToString());
            sb.Append("Target monitor: ");
            sb.AppendLine(targetMonitorName);

            sb.AppendLine();
            sb.AppendLine(" # Per monitor data:");
            sb.AppendLine();
            foreach (NativeMonitorData data in monitorData)
            {
                sb.AppendLine(data.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(" ## Monitors discovered:");
            sb.AppendLine();

            foreach (Monitor m in monitors)
            {
                sb.AppendLine(m.Device.ToString());
            }

            return sb.ToString();
        }
    }
}
