// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        private const string ParamsFile = "\\Microsoft\\PowerToys\\FancyZones\\editor-parameters.json";

        // Non-localizable string: Multi-monitor id
        private const string MultiMonitorId = "FancyZones#MultiMonitorDevice";

        private readonly IFileSystem _fileSystem = new FileSystem();

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DashCaseNamingPolicy(),
            WriteIndented = true,
        };

        private List<DeviceWrapper> _unusedDevices = new List<DeviceWrapper>();

        public string FancyZonesSettingsFile { get; private set; }

        public string FancyZonesEditorParamsFile { get; private set; }

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
            MonitorWidth,
            MonitorHeight,
        }

        // parsing cmd args
        private struct NativeMonitorData
        {
            public string MonitorId { get; set; }

            public int Dpi { get; set; }

            public int LeftCoordinate { get; set; }

            public int TopCoordinate { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public bool IsSelected { get; set; }

            public override string ToString()
            {
                var sb = new StringBuilder();

                // using CultureInfo.InvariantCulture since this is internal data
                sb.Append("ID: ");
                sb.AppendLine(MonitorId);
                sb.Append("DPI: ");
                sb.AppendLine(Dpi.ToString(CultureInfo.InvariantCulture));

                sb.Append("X: ");
                sb.AppendLine(LeftCoordinate.ToString(CultureInfo.InvariantCulture));
                sb.Append("Y: ");
                sb.AppendLine(TopCoordinate.ToString(CultureInfo.InvariantCulture));

                return sb.ToString();
            }
        }

        // zones-settings: devices
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

        // zones-settings: custom-zone-sets
        private class CanvasInfoWrapper
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

            public int SensitivityRadius { get; set; } = LayoutSettings.DefaultSensitivityRadius;
        }

        // zones-settings: custom-zone-sets
        private class GridInfoWrapper
        {
            public int Rows { get; set; }

            public int Columns { get; set; }

            public List<int> RowsPercentage { get; set; }

            public List<int> ColumnsPercentage { get; set; }

            public int[][] CellChildMap { get; set; }

            public bool ShowSpacing { get; set; } = LayoutSettings.DefaultShowSpacing;

            public int Spacing { get; set; } = LayoutSettings.DefaultSpacing;

            public int SensitivityRadius { get; set; } = LayoutSettings.DefaultSensitivityRadius;
        }

        // zones-settings: custom-zone-sets
        private struct CustomLayoutWrapper
        {
            public string Uuid { get; set; }

            public string Name { get; set; }

            public string Type { get; set; }

            public JsonElement Info { get; set; } // CanvasInfoWrapper or GridInfoWrapper
        }

        // zones-settings: templates
        private struct TemplateLayoutWrapper
        {
            public string Type { get; set; }

            public bool ShowSpacing { get; set; }

            public int Spacing { get; set; }

            public int ZoneCount { get; set; }

            public int SensitivityRadius { get; set; }
        }

        // zones-settings: quick-layout-keys-wrapper
        private struct QuickLayoutKeysWrapper
        {
            public int Key { get; set; }

            public string Uuid { get; set; }
        }

        // zones-settings
        private struct ZoneSettingsWrapper
        {
            public List<DeviceWrapper> Devices { get; set; }

            public List<CustomLayoutWrapper> CustomZoneSets { get; set; }

            public List<TemplateLayoutWrapper> Templates { get; set; }

            public List<QuickLayoutKeysWrapper> QuickLayoutKeys { get; set; }
        }

        private struct EditorParams
        {
            public int ProcessId { get; set; }

            public bool SpanZonesAcrossMonitors { get; set; }

            public List<NativeMonitorData> Monitors { get; set; }
        }

        public struct ParsingResult
        {
            public bool Result { get; }

            public string Message { get; }

            public string MalformedData { get; }

            public ParsingResult(bool result, string message = "", string data = "")
            {
                Result = result;
                Message = message;
                MalformedData = data;
            }
        }

        public FancyZonesEditorIO()
        {
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            FancyZonesSettingsFile = localAppDataDir + ZonesSettingsFile;
            FancyZonesEditorParamsFile = localAppDataDir + ParamsFile;
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
                * (7) work area left
                * (8) work area top
                * (9) work area width
                * (10) work area height
                * ...
                * Using CultureInfo.InvariantCulture since this is us parsing our own data
                */
                var argsParts = args[1].Split('/');

                // Process ID
                App.PowerToysPID = int.Parse(argsParts[(int)CmdArgs.PowerToysPID], CultureInfo.InvariantCulture);

                // Span zones across monitors
                App.Overlay.SpanZonesAcrossMonitors = int.Parse(argsParts[(int)CmdArgs.SpanZones], CultureInfo.InvariantCulture) == 1;

                // Target monitor id
                string targetMonitorName = argsParts[(int)CmdArgs.TargetMonitorId];

                if (!App.Overlay.SpanZonesAcrossMonitors)
                {
                    // Test launch with custom monitors configuration
                    bool isCustomMonitorConfigurationMode = targetMonitorName.StartsWith("Monitor#");
                    if (isCustomMonitorConfigurationMode)
                    {
                        App.Overlay.Monitors.Clear();
                    }

                    // Monitors count
                    int count = int.Parse(argsParts[(int)CmdArgs.MonitorsCount], CultureInfo.InvariantCulture);

                    // Parse the native monitor data
                    List<NativeMonitorData> nativeMonitorData = new List<NativeMonitorData>();
                    const int monitorArgsCount = 6;
                    for (int i = 0; i < count; i++)
                    {
                        var nativeData = default(NativeMonitorData);
                        nativeData.MonitorId = argsParts[(int)CmdArgs.MonitorId + (i * monitorArgsCount)];
                        nativeData.Dpi = int.Parse(argsParts[(int)CmdArgs.DPI + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.LeftCoordinate = int.Parse(argsParts[(int)CmdArgs.MonitorLeft + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.TopCoordinate = int.Parse(argsParts[(int)CmdArgs.MonitorTop + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.Width = int.Parse(argsParts[(int)CmdArgs.MonitorWidth + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.Height = int.Parse(argsParts[(int)CmdArgs.MonitorHeight + (i * monitorArgsCount)], CultureInfo.InvariantCulture);

                        nativeMonitorData.Add(nativeData);
                    }

                    var monitors = App.Overlay.Monitors;

                    // Update monitors data
                    if (isCustomMonitorConfigurationMode)
                    {
                        foreach (NativeMonitorData nativeData in nativeMonitorData)
                        {
                            var splittedId = nativeData.MonitorId.Split('_');
                            int width = int.Parse(splittedId[1], CultureInfo.InvariantCulture);
                            int height = int.Parse(splittedId[2], CultureInfo.InvariantCulture);

                            Rect bounds = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.Width, nativeData.Height);

                            Monitor monitor = new Monitor(bounds, bounds);
                            monitor.Device.Id = nativeData.MonitorId;
                            monitor.Device.Dpi = nativeData.Dpi;

                            monitors.Add(monitor);
                        }
                    }
                    else
                    {
                        foreach (NativeMonitorData nativeData in nativeMonitorData)
                        {
                            Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.Width, nativeData.Height);
                            if (nativeData.IsSelected)
                            {
                                targetMonitorName = nativeData.MonitorId;
                            }

                            var monitor = new Monitor(workArea, workArea);
                            monitor.Device.Id = nativeData.MonitorId;
                            monitor.Device.Dpi = nativeData.Dpi;

                            App.Overlay.AddMonitor(monitor);
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
                else
                {
                    // Monitors count
                    int count = int.Parse(argsParts[(int)CmdArgs.MonitorsCount], CultureInfo.InvariantCulture);

                    // Parse the native monitor data
                    List<NativeMonitorData> nativeMonitorData = new List<NativeMonitorData>();
                    const int monitorArgsCount = 6;
                    for (int i = 0; i < count; i++)
                    {
                        var nativeData = default(NativeMonitorData);
                        nativeData.MonitorId = argsParts[(int)CmdArgs.MonitorId + (i * monitorArgsCount)];
                        nativeData.Dpi = int.Parse(argsParts[(int)CmdArgs.DPI + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.LeftCoordinate = int.Parse(argsParts[(int)CmdArgs.MonitorLeft + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.TopCoordinate = int.Parse(argsParts[(int)CmdArgs.MonitorTop + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.Width = int.Parse(argsParts[(int)CmdArgs.MonitorWidth + (i * monitorArgsCount)], CultureInfo.InvariantCulture);
                        nativeData.Height = int.Parse(argsParts[(int)CmdArgs.MonitorHeight + (i * monitorArgsCount)], CultureInfo.InvariantCulture);

                        nativeMonitorData.Add(nativeData);
                    }

                    Rect workAreaUnion = default;

                    // Update monitors data
                    foreach (NativeMonitorData nativeData in nativeMonitorData)
                    {
                        Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.Width, nativeData.Height);
                        workAreaUnion = Rect.Union(workAreaUnion, workArea);
                    }

                    var monitor = new Monitor(workAreaUnion, workAreaUnion);
                    monitor.Device.Id = targetMonitorName;

                    App.Overlay.Monitors.Add(monitor);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(Properties.Resources.Error_Invalid_Arguments, Properties.Resources.Error_Message_Box_Title);
                ((App)Application.Current).Shutdown();
            }
        }

        public ParsingResult ParseParams()
        {
            if (_fileSystem.File.Exists(FancyZonesEditorParamsFile))
            {
                string data = string.Empty;

                try
                {
                    data = ReadFile(FancyZonesEditorParamsFile);
                    EditorParams editorParams = JsonSerializer.Deserialize<EditorParams>(data, _options);

                    // Process ID
                    App.PowerToysPID = editorParams.ProcessId;

                    // Span zones across monitors
                    App.Overlay.SpanZonesAcrossMonitors = editorParams.SpanZonesAcrossMonitors;

                    if (!App.Overlay.SpanZonesAcrossMonitors)
                    {
                        string targetMonitorName = string.Empty;

                        foreach (NativeMonitorData nativeData in editorParams.Monitors)
                        {
                            Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.Width, nativeData.Height);
                            if (nativeData.IsSelected)
                            {
                                targetMonitorName = nativeData.MonitorId;
                            }

                            var monitor = new Monitor(workArea, workArea);
                            monitor.Device.Id = nativeData.MonitorId;
                            monitor.Device.Dpi = nativeData.Dpi;

                            App.Overlay.AddMonitor(monitor);
                        }

                        // Set active desktop
                        var monitors = App.Overlay.Monitors;
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
                    else
                    {
                        if (editorParams.Monitors.Count != 1)
                        {
                            return new ParsingResult(false);
                        }

                        var nativeData = editorParams.Monitors[0];
                        Rect workArea = new Rect(nativeData.LeftCoordinate, nativeData.TopCoordinate, nativeData.Width, nativeData.Height);

                        var monitor = new Monitor(workArea, workArea);
                        monitor.Device.Id = nativeData.MonitorId;
                        App.Overlay.AddMonitor(monitor);
                    }
                }
                catch (Exception ex)
                {
                    return new ParsingResult(false, ex.Message, data);
                }

                return new ParsingResult(true);
            }
            else
            {
                return new ParsingResult(false);
            }
        }

        public ParsingResult ParseZoneSettings()
        {
            _unusedDevices.Clear();

            if (_fileSystem.File.Exists(FancyZonesSettingsFile))
            {
                ZoneSettingsWrapper zoneSettings;
                string settingsString = string.Empty;

                try
                {
                    settingsString = ReadFile(FancyZonesSettingsFile);
                    zoneSettings = JsonSerializer.Deserialize<ZoneSettingsWrapper>(settingsString, _options);
                }
                catch (Exception ex)
                {
                    return new ParsingResult(false, ex.Message, settingsString);
                }

                try
                {
                    bool devicesParsingResult = SetDevices(zoneSettings.Devices);
                    bool customZonesParsingResult = SetCustomLayouts(zoneSettings.CustomZoneSets);
                    bool templatesParsingResult = SetTemplateLayouts(zoneSettings.Templates);
                    bool quickLayoutSwitchKeysParsingResult = SetQuickLayoutSwitchKeys(zoneSettings.QuickLayoutKeys);

                    if (!devicesParsingResult || !customZonesParsingResult)
                    {
                        return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Zones_Settings_Message, settingsString);
                    }
                }
                catch (Exception ex)
                {
                    return new ParsingResult(false, ex.Message, settingsString);
                }
            }

            return new ParsingResult(true);
        }

        public void SerializeZoneSettings()
        {
            ZoneSettingsWrapper zoneSettings = new ZoneSettingsWrapper { };
            zoneSettings.Devices = new List<DeviceWrapper>();
            zoneSettings.CustomZoneSets = new List<CustomLayoutWrapper>();
            zoneSettings.Templates = new List<TemplateLayoutWrapper>();
            zoneSettings.QuickLayoutKeys = new List<QuickLayoutKeysWrapper>();

            // Serialize used devices
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
                    ActiveZoneset = new DeviceWrapper.ActiveZoneSetWrapper
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

            // Serialize unused devices
            foreach (var device in _unusedDevices)
            {
                zoneSettings.Devices.Add(device);
            }

            // Serialize custom zonesets
            foreach (LayoutModel layout in MainWindowSettingsModel.CustomModels)
            {
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
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper>(),
                        SensitivityRadius = canvasLayout.SensitivityRadius,
                    };

                    foreach (var zone in canvasLayout.Zones)
                    {
                        wrapper.Zones.Add(new CanvasInfoWrapper.CanvasZoneWrapper
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
                        ShowSpacing = gridLayout.ShowSpacing,
                        Spacing = gridLayout.Spacing,
                        SensitivityRadius = gridLayout.SensitivityRadius,
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

            // Serialize template layouts
            foreach (LayoutModel layout in MainWindowSettingsModel.DefaultModels)
            {
                TemplateLayoutWrapper wrapper = new TemplateLayoutWrapper
                {
                    Type = LayoutTypeToJsonTag(layout.Type),
                    SensitivityRadius = layout.SensitivityRadius,
                    ZoneCount = layout.TemplateZoneCount,
                };

                if (layout is GridLayoutModel grid)
                {
                    wrapper.ShowSpacing = grid.ShowSpacing;
                    wrapper.Spacing = grid.Spacing;
                }

                zoneSettings.Templates.Add(wrapper);
            }

            // Serialize quick layout switch keys
            foreach (var pair in MainWindowSettingsModel.QuickKeys.SelectedKeys)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    try
                    {
                        QuickLayoutKeysWrapper wrapper = new QuickLayoutKeysWrapper
                        {
                            Key = int.Parse(pair.Key),
                            Uuid = pair.Value,
                        };

                        zoneSettings.QuickLayoutKeys.Add(wrapper);
                    }
                    catch (Exception)
                    {
                    }
                }
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

        private string ReadFile(string fileName)
        {
            Stream inputStream = _fileSystem.File.Open(fileName, FileMode.Open);
            using (StreamReader reader = new StreamReader(inputStream))
            {
                string data = reader.ReadToEnd();
                inputStream.Close();
                return data;
            }
        }

        private bool SetDevices(List<DeviceWrapper> devices)
        {
            if (devices == null)
            {
                return false;
            }

            bool result = true;
            var monitors = App.Overlay.Monitors;
            foreach (var device in devices)
            {
                if (device.DeviceId == null || device.DeviceId.Length == 0 || device.ActiveZoneset.Uuid == null || device.ActiveZoneset.Uuid.Length == 0)
                {
                    result = false;
                    continue;
                }

                bool unused = true;
                foreach (Monitor monitor in monitors)
                {
                    if (monitor.Device.Id == device.DeviceId)
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

                        monitor.Settings = settings;
                        unused = false;
                        break;
                    }
                }

                if (unused)
                {
                    _unusedDevices.Add(device);
                }
            }

            return result;
        }

        private bool SetCustomLayouts(List<CustomLayoutWrapper> customLayouts)
        {
            if (customLayouts == null)
            {
                return false;
            }

            ObservableCollection<LayoutModel> models = new ObservableCollection<LayoutModel>();
            bool result = true;

            foreach (var zoneSet in customLayouts)
            {
                if (zoneSet.Uuid == null || zoneSet.Uuid.Length == 0)
                {
                    result = false;
                    continue;
                }

                LayoutModel layout = null;
                try
                {
                    if (zoneSet.Type == CanvasLayoutModel.ModelTypeID)
                    {
                        layout = ParseCanvasInfo(zoneSet);
                    }
                    else if (zoneSet.Type == GridLayoutModel.ModelTypeID)
                    {
                        layout = ParseGridInfo(zoneSet);
                    }
                }
                catch (Exception)
                {
                    result = false;
                    continue;
                }

                if (layout == null)
                {
                    result = false;
                    continue;
                }

                models.Add(layout);
            }

            MainWindowSettingsModel.CustomModels = models;

            return result;
        }

        private bool SetTemplateLayouts(List<TemplateLayoutWrapper> templateLayouts)
        {
            if (templateLayouts == null)
            {
                return false;
            }

            foreach (var wrapper in templateLayouts)
            {
                LayoutType type = JsonTagToLayoutType(wrapper.Type);
                LayoutModel layout = MainWindowSettingsModel.DefaultModels[(int)type];

                layout.SensitivityRadius = wrapper.SensitivityRadius;
                layout.TemplateZoneCount = wrapper.ZoneCount;

                if (layout is GridLayoutModel grid)
                {
                    grid.ShowSpacing = wrapper.ShowSpacing;
                    grid.Spacing = wrapper.Spacing;
                }

                layout.InitTemplateZones();
            }

            return true;
        }

        private bool SetQuickLayoutSwitchKeys(List<QuickLayoutKeysWrapper> quickSwitchKeys)
        {
            if (quickSwitchKeys == null)
            {
                return false;
            }

            MainWindowSettingsModel.QuickKeys.CleanUp();
            foreach (var wrapper in quickSwitchKeys)
            {
                MainWindowSettingsModel.QuickKeys.SelectKey(wrapper.Key.ToString(), wrapper.Uuid);
            }

            return true;
        }

        private CanvasLayoutModel ParseCanvasInfo(CustomLayoutWrapper wrapper)
        {
            var info = JsonSerializer.Deserialize<CanvasInfoWrapper>(wrapper.Info.GetRawText(), _options);

            var zones = new List<Int32Rect>();
            foreach (var zone in info.Zones)
            {
                if (zone.Width < 0 || zone.Height < 0)
                {
                    // Malformed data
                    return null;
                }

                zones.Add(new Int32Rect { X = zone.X, Y = zone.Y, Width = zone.Width, Height = zone.Height });
            }

            var layout = new CanvasLayoutModel(wrapper.Uuid, wrapper.Name, LayoutType.Custom, zones, Math.Max(info.RefWidth, 0), Math.Max(info.RefHeight, 0));
            layout.SensitivityRadius = info.SensitivityRadius;

            return layout;
        }

        private GridLayoutModel ParseGridInfo(CustomLayoutWrapper wrapper)
        {
            var info = JsonSerializer.Deserialize<GridInfoWrapper>(wrapper.Info.GetRawText(), _options);

            // Check if rows and columns are valid
            if (info.Rows <= 0 || info.Columns <= 0)
            {
                return null;
            }

            // Check if percentage is valid. Otherwise, Editor could crash on layout rendering.
            if (info.RowsPercentage.Exists((x) => (x < 1)) || info.ColumnsPercentage.Exists((x) => (x < 1)))
            {
                return null;
            }

            if (info.CellChildMap.Length != info.Rows)
            {
                return null;
            }

            foreach (var col in info.CellChildMap)
            {
                if (col.Length != info.Columns)
                {
                    return null;
                }
            }

            var cells = new int[info.Rows, info.Columns];
            for (int row = 0; row < info.Rows; row++)
            {
                for (int column = 0; column < info.Columns; column++)
                {
                    cells[row, column] = info.CellChildMap[row][column];
                }
            }

            var layout = new GridLayoutModel(wrapper.Uuid, wrapper.Name, LayoutType.Custom, info.Rows, info.Columns, info.RowsPercentage, info.ColumnsPercentage, cells);
            if (!layout.IsModelValid())
            {
                return null;
            }

            layout.SensitivityRadius = info.SensitivityRadius;
            layout.ShowSpacing = info.ShowSpacing;
            layout.Spacing = info.Spacing;
            return layout;
        }

        private LayoutType JsonTagToLayoutType(string tag)
        {
            switch (tag)
            {
                case BlankJsonTag:
                    return LayoutType.Blank;
                case FocusJsonTag:
                    return LayoutType.Focus;
                case ColumnsJsonTag:
                    return LayoutType.Columns;
                case RowsJsonTag:
                    return LayoutType.Rows;
                case GridJsonTag:
                    return LayoutType.Grid;
                case PriorityGridJsonTag:
                    return LayoutType.PriorityGrid;
                case CustomJsonTag:
                    return LayoutType.Custom;
            }

            return LayoutType.Blank;
        }

        private string LayoutTypeToJsonTag(LayoutType type)
        {
            switch (type)
            {
                case LayoutType.Blank:
                    return BlankJsonTag;
                case LayoutType.Focus:
                    return FocusJsonTag;
                case LayoutType.Columns:
                    return ColumnsJsonTag;
                case LayoutType.Rows:
                    return RowsJsonTag;
                case LayoutType.Grid:
                    return GridJsonTag;
                case LayoutType.PriorityGrid:
                    return PriorityGridJsonTag;
                case LayoutType.Custom:
                    return CustomJsonTag;
                default:
                    return string.Empty;
            }
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

            // using CultureInfo.InvariantCulture since this is for PowerToys team
            sb.Append("Monitors count: ");
            sb.AppendLine(count.ToString(CultureInfo.InvariantCulture));
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
