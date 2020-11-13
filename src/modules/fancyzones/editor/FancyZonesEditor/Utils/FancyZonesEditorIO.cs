// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor.Utils
{
    public class FancyZonesEditorIO
    {
        // JSON tags
        private const string AppliedZonesetsJsonTag = "applied-zonesets";
        private const string DeviceIdJsonTag = "device-id";
        private const string ActiveZoneSetJsonTag = "active-zoneset";
        private const string UuidJsonTag = "uuid";
        private const string TypeJsonTag = "type";
        private const string EditorShowSpacingJsonTag = "editor-show-spacing";
        private const string EditorSpacingJsonTag = "editor-spacing";
        private const string EditorZoneCountJsonTag = "editor-zone-count";
        private const string EditorSensitivityRadiusJsonTag = "editor-sensitivity-radius";

        private const string FocusJsonTag = "focus";
        private const string ColumnsJsonTag = "columns";
        private const string RowsJsonTag = "rows";
        private const string GridJsonTag = "grid";
        private const string PriorityGridJsonTag = "priority-grid";
        private const string CustomJsonTag = "custom";

        private const string NameStr = "name";
        private const string CustomZoneSetsJsonTag = "custom-zone-sets";
        private const string InfoJsonTag = "info";
        private const string RowsPercentageJsonTag = "rows-percentage";
        private const string ColumnsPercentageJsonTag = "columns-percentage";
        private const string CellChildMapJsonTag = "cell-child-map";
        private const string ZonesJsonTag = "zones";
        private const string CanvasJsonTag = "canvas";
        private const string RefWidthJsonTag = "ref-width";
        private const string RefHeightJsonTag = "ref-height";
        private const string XJsonTag = "X";
        private const string YJsonTag = "Y";
        private const string WidthJsonTag = "width";
        private const string HeightJsonTag = "height";

        // Files
        private const string ZonesSettingsFile = "\\Microsoft\\PowerToys\\FancyZones\\zones-settings.json";
        private const string ActiveZoneSetsTmpFileName = "FancyZonesActiveZoneSets.json";
        private const string AppliedZoneSetsTmpFileName = "FancyZonesAppliedZoneSets.json";
        private const string DeletedCustomZoneSetsTmpFileName = "FancyZonesDeletedCustomZoneSets.json";

        private readonly IFileSystem _fileSystem = new FileSystem();

        private JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DashCaseNamingPolicy(),
        };

        public string ActiveZoneSetTmpFile { get; private set; }

        public string AppliedZoneSetTmpFile { get; private set; }

        public string DeletedCustomZoneSetsTmpFile { get; private set; }

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

        private struct ActiveZoneSetWrapper
        {
            public string Uuid { get; set; }

            public string Type { get; set; }
        }

        private struct AppliedZoneSet
        {
            public string DeviceId { get; set; }

            public ActiveZoneSetWrapper ActiveZoneset { get; set; }

            public bool EditorShowSpacing { get; set; }

            public int EditorSpacing { get; set; }

            public int EditorZoneCount { get; set; }

            public int EditorSensitivityRadius { get; set; }
        }

        private struct AppliedZonesetsToDesktops
        {
            public List<AppliedZoneSet> AppliedZonesets { get; set; }
        }

        private struct DeletedCustomZoneSetsWrapper
        {
            public List<string> DeletedCustomZoneSets { get; set; }
        }

        private struct CreatedCustomZoneSetsWrapper
        {
            public List<JsonElement> CreatedCustomZoneSets { get; set; }
        }

        public FancyZonesEditorIO()
        {
            string tmpDirPath = _fileSystem.Path.GetTempPath();

            ActiveZoneSetTmpFile = tmpDirPath + ActiveZoneSetsTmpFileName;
            AppliedZoneSetTmpFile = tmpDirPath + AppliedZoneSetsTmpFileName;
            DeletedCustomZoneSetsTmpFile = tmpDirPath + DeletedCustomZoneSetsTmpFileName;

            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            FancyZonesSettingsFile = localAppDataDir + ZonesSettingsFile;
        }

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

                    // Monitors count
                    int count = int.Parse(argsParts[(int)CmdArgs.MonitorsCount]);
                    if (count != App.Overlay.DesktopsCount)
                    {
                        MessageBox.Show(Properties.Resources.Error_Invalid_Arguments, Properties.Resources.Error_Message_Box_Title);
                        ((App)Application.Current).Shutdown();
                    }

                    double primaryMonitorDPI = 96f;
                    double minimalUsedMonitorDPI = double.MaxValue;

                    // parse the native monitor data
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

                    // update monitors data
                    double scaleFactor = 96f / primaryMonitorDPI;

                    // update monitors data
                    foreach (Monitor monitor in monitors)
                    {
                        bool matchFound = false;
                        monitor.Scale(scaleFactor);

                        foreach (NativeMonitorData nativeData in nativeMonitorData)
                        {
                            // these coordinates will be scaled depending on the relation between
                            // minimal used monitor DPI and the DPI of the primary monitor
                            double x = monitor.Device.UnscaledBounds.X * identifyScaleFactor;
                            double y = monitor.Device.UnscaledBounds.Y * identifyScaleFactor;

                            // can't do an exact match since the rounding algorithm used by the framework is different from ours
                            if (x >= (nativeData.X - 1) && x <= (nativeData.X + 1) &&
                                y >= (nativeData.Y - 1) && y <= (nativeData.Y + 1))
                            {
                                monitor.Device.Id = nativeData.Id;
                                monitor.Device.Dpi = nativeData.Dpi;
                                matchFound = true;
                                break;
                            }
                        }

                        if (matchFound == false)
                        {
                            // TODO: move the string to the resx file
                            MessageBox.Show("Match not found (" + monitor.Device.UnscaledBounds.ToString() + ")");
                        }
                    }

                    // set active desktop
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

        public void ParseDeviceInfoData()
        {
            try
            {
                JsonElement jsonObject = default(JsonElement);

                if (_fileSystem.File.Exists(ActiveZoneSetTmpFile))
                {
                    Stream inputStream = _fileSystem.File.Open(ActiveZoneSetTmpFile, FileMode.Open);
                    jsonObject = JsonDocument.Parse(inputStream, options: default).RootElement;
                    inputStream.Close();

                    JsonElement json = jsonObject.GetProperty(AppliedZonesetsJsonTag);

                    int layoutId = 0;
                    for (int i = 0; i < json.GetArrayLength() && layoutId < App.Overlay.DesktopsCount; i++)
                    {
                        var zonesetData = json[i];

                        string deviceId = zonesetData.GetProperty(DeviceIdJsonTag).GetString();

                        string currentLayoutType = zonesetData.GetProperty(ActiveZoneSetJsonTag).GetProperty(TypeJsonTag).GetString();
                        LayoutType type = JsonTagToLayoutType(currentLayoutType);

                        if (!App.Overlay.SpanZonesAcrossMonitors)
                        {
                            var monitors = App.Overlay.Monitors;
                            for (int monitorIndex = 0; monitorIndex < monitors.Count; monitorIndex++)
                            {
                                if (monitors[monitorIndex].Device.Id == deviceId)
                                {
                                    monitors[monitorIndex].Settings = new LayoutSettings
                                    {
                                        DeviceId = deviceId,
                                        ZonesetUuid = zonesetData.GetProperty(ActiveZoneSetJsonTag).GetProperty(UuidJsonTag).GetString(),
                                        ShowSpacing = zonesetData.GetProperty(EditorShowSpacingJsonTag).GetBoolean(),
                                        Spacing = zonesetData.GetProperty(EditorSpacingJsonTag).GetInt32(),
                                        Type = type,
                                        ZoneCount = zonesetData.GetProperty(EditorZoneCountJsonTag).GetInt32(),
                                        SensitivityRadius = zonesetData.GetProperty(EditorSensitivityRadiusJsonTag).GetInt32(),
                                    };

                                    break;
                                }
                            }
                        }
                        else
                        {
                            bool isLayoutMultiMonitor = deviceId.StartsWith("FancyZones#MultiMonitorDevice");
                            if (isLayoutMultiMonitor)
                            {
                                // one zoneset for all desktops
                                App.Overlay.Monitors[App.Overlay.CurrentDesktop].Settings = new LayoutSettings
                                {
                                    DeviceId = deviceId,
                                    ZonesetUuid = zonesetData.GetProperty(ActiveZoneSetJsonTag).GetProperty(UuidJsonTag).GetString(),
                                    ShowSpacing = zonesetData.GetProperty(EditorShowSpacingJsonTag).GetBoolean(),
                                    Spacing = zonesetData.GetProperty(EditorSpacingJsonTag).GetInt32(),
                                    Type = type,
                                    ZoneCount = zonesetData.GetProperty(EditorZoneCountJsonTag).GetInt32(),
                                    SensitivityRadius = zonesetData.GetProperty(EditorSensitivityRadiusJsonTag).GetInt32(),
                                };

                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Parsing_Device_Info, ex);
            }
        }

        public void ParseLayouts(ref ObservableCollection<LayoutModel> custom, ref List<string> deleted)
        {
            try
            {
                Stream inputStream = _fileSystem.File.Open(FancyZonesSettingsFile, FileMode.Open);
                JsonDocument jsonObject = JsonDocument.Parse(inputStream, options: default);
                JsonElement.ArrayEnumerator customZoneSetsEnumerator = jsonObject.RootElement.GetProperty(CustomZoneSetsJsonTag).EnumerateArray();

                while (customZoneSetsEnumerator.MoveNext())
                {
                    var current = customZoneSetsEnumerator.Current;
                    string name = current.GetProperty(NameStr).GetString();
                    string type = current.GetProperty(TypeJsonTag).GetString();
                    string uuid = current.GetProperty(UuidJsonTag).GetString();
                    var info = current.GetProperty(InfoJsonTag);

                    if (type.Equals(GridJsonTag))
                    {
                        bool error = false;

                        int rows = info.GetProperty(RowsJsonTag).GetInt32();
                        int columns = info.GetProperty(ColumnsJsonTag).GetInt32();

                        List<int> rowsPercentage = new List<int>(rows);
                        JsonElement.ArrayEnumerator rowsPercentageEnumerator = info.GetProperty(RowsPercentageJsonTag).EnumerateArray();

                        List<int> columnsPercentage = new List<int>(columns);
                        JsonElement.ArrayEnumerator columnsPercentageEnumerator = info.GetProperty(ColumnsPercentageJsonTag).EnumerateArray();

                        if (rows <= 0 || columns <= 0 || rowsPercentageEnumerator.Count() != rows || columnsPercentageEnumerator.Count() != columns)
                        {
                            error = true;
                        }

                        while (!error && rowsPercentageEnumerator.MoveNext())
                        {
                            int percentage = rowsPercentageEnumerator.Current.GetInt32();
                            if (percentage <= 0)
                            {
                                error = true;
                                break;
                            }

                            rowsPercentage.Add(percentage);
                        }

                        while (!error && columnsPercentageEnumerator.MoveNext())
                        {
                            int percentage = columnsPercentageEnumerator.Current.GetInt32();
                            if (percentage <= 0)
                            {
                                error = true;
                                break;
                            }

                            columnsPercentage.Add(percentage);
                        }

                        int i = 0;
                        JsonElement.ArrayEnumerator cellChildMapRows = info.GetProperty(CellChildMapJsonTag).EnumerateArray();
                        int[,] cellChildMap = new int[rows, columns];

                        if (cellChildMapRows.Count() != rows)
                        {
                            error = true;
                        }

                        while (!error && cellChildMapRows.MoveNext())
                        {
                            int j = 0;
                            JsonElement.ArrayEnumerator cellChildMapRowElems = cellChildMapRows.Current.EnumerateArray();
                            if (cellChildMapRowElems.Count() != columns)
                            {
                                error = true;
                                break;
                            }

                            while (cellChildMapRowElems.MoveNext())
                            {
                                cellChildMap[i, j++] = cellChildMapRowElems.Current.GetInt32();
                            }

                            i++;
                        }

                        if (error)
                        {
                            App.ShowExceptionMessageBox(string.Format(Properties.Resources.Error_Layout_Malformed_Data, name));
                            deleted.Add(Guid.Parse(uuid).ToString().ToUpper());
                            continue;
                        }

                        custom.Add(new GridLayoutModel(uuid, name, LayoutType.Custom, rows, columns, rowsPercentage, columnsPercentage, cellChildMap));
                    }
                    else if (type.Equals(CanvasJsonTag))
                    {
                        int workAreaWidth = info.GetProperty(RefWidthJsonTag).GetInt32();
                        int workAreaHeight = info.GetProperty(RefHeightJsonTag).GetInt32();

                        JsonElement.ArrayEnumerator zonesEnumerator = info.GetProperty(ZonesJsonTag).EnumerateArray();
                        IList<Int32Rect> zones = new List<Int32Rect>();

                        bool error = false;

                        if (workAreaWidth <= 0 || workAreaHeight <= 0)
                        {
                            error = true;
                        }

                        while (!error && zonesEnumerator.MoveNext())
                        {
                            int x = zonesEnumerator.Current.GetProperty(XJsonTag).GetInt32();
                            int y = zonesEnumerator.Current.GetProperty(YJsonTag).GetInt32();
                            int width = zonesEnumerator.Current.GetProperty(WidthJsonTag).GetInt32();
                            int height = zonesEnumerator.Current.GetProperty(HeightJsonTag).GetInt32();

                            if (width <= 0 || height <= 0)
                            {
                                error = true;
                                break;
                            }

                            zones.Add(new Int32Rect(x, y, width, height));
                        }

                        if (error)
                        {
                            App.ShowExceptionMessageBox(string.Format(Properties.Resources.Error_Layout_Malformed_Data, name));
                            deleted.Add(Guid.Parse(uuid).ToString().ToUpper());
                            continue;
                        }

                        custom.Add(new CanvasLayoutModel(uuid, name, LayoutType.Custom, zones, workAreaWidth, workAreaHeight));
                    }
                }

                inputStream.Close();
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Loading_Custom_Layouts, ex);
            }
        }

        public void SerializeAppliedLayouts()
        {
            AppliedZonesetsToDesktops applied = new AppliedZonesetsToDesktops { };
            applied.AppliedZonesets = new List<AppliedZoneSet>();

            foreach (var monitor in App.Overlay.Monitors)
            {
                LayoutSettings zoneset = monitor.Settings;
                if (zoneset.ZonesetUuid.Length == 0)
                {
                    continue;
                }

                ActiveZoneSetWrapper activeZoneSet = new ActiveZoneSetWrapper
                {
                    Uuid = zoneset.ZonesetUuid,
                };

                activeZoneSet.Type = LayoutTypeToJsonTag(zoneset.Type);

                applied.AppliedZonesets.Add(new AppliedZoneSet
                {
                    DeviceId = zoneset.DeviceId,
                    ActiveZoneset = activeZoneSet,
                    EditorShowSpacing = zoneset.ShowSpacing,
                    EditorSpacing = zoneset.Spacing,
                    EditorZoneCount = zoneset.ZoneCount,
                    EditorSensitivityRadius = zoneset.SensitivityRadius,
                });
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(applied, _options);
                _fileSystem.File.WriteAllText(ActiveZoneSetTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeDeletedCustomZoneSets(List<string> models)
        {
            DeletedCustomZoneSetsWrapper deletedLayouts = new DeletedCustomZoneSetsWrapper
            {
                DeletedCustomZoneSets = models,
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(deletedLayouts, _options);
                _fileSystem.File.WriteAllText(DeletedCustomZoneSetsTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Serializing_Deleted_Layouts, ex);
            }
        }

        public void SerializeCreatedCustomZonesets(List<JsonElement> models)
        {
            CreatedCustomZoneSetsWrapper layouts = new CreatedCustomZoneSetsWrapper
            {
                CreatedCustomZoneSets = models,
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(layouts, _options);
                _fileSystem.File.WriteAllText(AppliedZoneSetTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessageBox(Properties.Resources.Error_Persisting_Custom_Layout, ex);
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
            switch (type)
            {
                case LayoutType.Focus:
                    return FocusJsonTag;
                case LayoutType.Rows:
                    return RowsJsonTag;
                case LayoutType.Columns:
                    return ColumnsJsonTag;
                case LayoutType.Grid:
                    return GridJsonTag;
                case LayoutType.PriorityGrid:
                    return PriorityGridJsonTag;
                case LayoutType.Custom:
                    return CustomJsonTag;
            }

            return string.Empty;
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
