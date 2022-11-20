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
using System.Threading.Tasks;
using System.Windows;
using FancyZonesEditor.Logs;
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
        private const string HorizontalJsonTag = "horizontal";
        private const string VerticalJsonTag = "vertical";

        // Non-localizable strings: Files
        private const string AppliedLayoutsFile = "\\Microsoft\\PowerToys\\FancyZones\\applied-layouts.json";
        private const string LayoutHotkeysFile = "\\Microsoft\\PowerToys\\FancyZones\\layout-hotkeys.json";
        private const string LayoutTemplatesFile = "\\Microsoft\\PowerToys\\FancyZones\\layout-templates.json";
        private const string CustomLayoutsFile = "\\Microsoft\\PowerToys\\FancyZones\\custom-layouts.json";
        private const string DefaultLayoutsFile = "\\Microsoft\\PowerToys\\FancyZones\\default-layouts.json";
        private const string ParamsFile = "\\Microsoft\\PowerToys\\FancyZones\\editor-parameters.json";

        // Non-localizable string: default virtual desktop id
        private const string DefaultVirtualDesktopGuid = "{00000000-0000-0000-0000-000000000000}";

        private readonly IFileSystem _fileSystem = new FileSystem();

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new DashCaseNamingPolicy(),
            WriteIndented = true,
        };

        private List<AppliedLayoutWrapper> _unusedLayouts = new List<AppliedLayoutWrapper>();

        public string FancyZonesAppliedLayoutsFile { get; private set; }

        public string FancyZonesLayoutHotkeysFile { get; private set; }

        public string FancyZonesLayoutTemplatesFile { get; private set; }

        public string FancyZonesCustomLayoutsFile { get; private set; }

        public string FancyZonesDefaultLayoutsFile { get; private set; }

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

        // applied-layouts.json
        private struct AppliedLayoutWrapper
        {
            public struct DeviceIdWrapper
            {
                public string Monitor { get; set; }

                public string MonitorInstance { get; set; }

                public int MonitorNumber { get; set; }

                public string SerialNumber { get; set; }

                public string VirtualDesktop { get; set; }
            }

            public struct LayoutWrapper
            {
                public string Uuid { get; set; }

                public string Type { get; set; }

                public bool ShowSpacing { get; set; }

                public int Spacing { get; set; }

                public int ZoneCount { get; set; }

                public int SensitivityRadius { get; set; }
            }

            public DeviceIdWrapper Device { get; set; }

            public LayoutWrapper AppliedLayout { get; set; }
        }

        // applied-layouts.json
        private struct AppliedLayoutsListWrapper
        {
            public List<AppliedLayoutWrapper> AppliedLayouts { get; set; }
        }

        // custom-layouts.json
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

        // custom-layouts.json
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

        // custom-layouts.json
        private struct CustomLayoutWrapper
        {
            public string Uuid { get; set; }

            public string Name { get; set; }

            public string Type { get; set; }

            public JsonElement Info { get; set; } // CanvasInfoWrapper or GridInfoWrapper
        }

        // custom-layouts.json
        private struct CustomLayoutListWrapper
        {
            public List<CustomLayoutWrapper> CustomLayouts { get; set; }
        }

        // layout-templates.json
        private struct TemplateLayoutWrapper
        {
            public string Type { get; set; }

            public bool ShowSpacing { get; set; }

            public int Spacing { get; set; }

            public int ZoneCount { get; set; }

            public int SensitivityRadius { get; set; }
        }

        // layout-templates.json
        private struct TemplateLayoutsListWrapper
        {
            public List<TemplateLayoutWrapper> LayoutTemplates { get; set; }
        }

        // layout-hotkeys.json
        private struct LayoutHotkeyWrapper
        {
            public int Key { get; set; }

            public string LayoutId { get; set; }
        }

        // layout-hotkeys.json
        private struct LayoutHotkeysWrapper
        {
            public List<LayoutHotkeyWrapper> LayoutHotkeys { get; set; }
        }

        // default-layouts.json
        private struct DefaultLayoutWrapper
        {
            public struct LayoutWrapper
            {
                public string Uuid { get; set; }

                public string Type { get; set; }

                public bool ShowSpacing { get; set; }

                public int Spacing { get; set; }

                public int ZoneCount { get; set; }

                public int SensitivityRadius { get; set; }
            }

            public string MonitorConfiguration { get; set; }

            public LayoutWrapper Layout { get; set; }
        }

        // default-layouts.json
        private struct DefaultLayoutsListWrapper
        {
            public List<DefaultLayoutWrapper> DefaultLayouts { get; set; }
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
            FancyZonesAppliedLayoutsFile = localAppDataDir + AppliedLayoutsFile;
            FancyZonesLayoutHotkeysFile = localAppDataDir + LayoutHotkeysFile;
            FancyZonesLayoutTemplatesFile = localAppDataDir + LayoutTemplatesFile;
            FancyZonesCustomLayoutsFile = localAppDataDir + CustomLayoutsFile;
            FancyZonesDefaultLayoutsFile = localAppDataDir + DefaultLayoutsFile;
            FancyZonesEditorParamsFile = localAppDataDir + ParamsFile;
        }

        public ParsingResult ParseParams()
        {
            Logger.LogTrace();

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
                    Logger.LogError("Editor params parsing error", ex);
                    return new ParsingResult(false, ex.Message, data);
                }

                return new ParsingResult(true);
            }
            else
            {
                return new ParsingResult(false);
            }
        }

        public ParsingResult ParseAppliedLayouts()
        {
            Logger.LogTrace();

            _unusedLayouts.Clear();

            if (_fileSystem.File.Exists(FancyZonesAppliedLayoutsFile))
            {
                AppliedLayoutsListWrapper appliedLayouts;
                string settingsString = string.Empty;

                try
                {
                    settingsString = ReadFile(FancyZonesAppliedLayoutsFile);
                    appliedLayouts = JsonSerializer.Deserialize<AppliedLayoutsListWrapper>(settingsString, _options);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Applied layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, settingsString);
                }

                try
                {
                    bool parsingResult = SetAppliedLayouts(appliedLayouts.AppliedLayouts);
                    if (!parsingResult)
                    {
                        return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Applied_Layouts_Message, settingsString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Applied layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, settingsString);
                }
            }

            return new ParsingResult(true);
        }

        public ParsingResult ParseLayoutHotkeys()
        {
            Logger.LogTrace();

            if (_fileSystem.File.Exists(FancyZonesLayoutHotkeysFile))
            {
                LayoutHotkeysWrapper layoutHotkeys;
                string dataString = string.Empty;

                try
                {
                    dataString = ReadFile(FancyZonesLayoutHotkeysFile);
                    layoutHotkeys = JsonSerializer.Deserialize<LayoutHotkeysWrapper>(dataString, _options);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Layout hotkeys parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }

                try
                {
                    bool layoutHotkeysParsingResult = SetLayoutHotkeys(layoutHotkeys);

                    if (!layoutHotkeysParsingResult)
                    {
                        return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Layout_Hotkeys_Message, dataString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Layout hotkeys parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }
            }

            return new ParsingResult(true);
        }

        public ParsingResult ParseLayoutTemplates()
        {
            Logger.LogTrace();

            if (_fileSystem.File.Exists(FancyZonesLayoutTemplatesFile))
            {
                TemplateLayoutsListWrapper templates;
                string dataString = string.Empty;

                try
                {
                    dataString = ReadFile(FancyZonesLayoutTemplatesFile);
                    templates = JsonSerializer.Deserialize<TemplateLayoutsListWrapper>(dataString, _options);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Layout templates parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }

                try
                {
                    bool parsingResult = SetTemplateLayouts(templates.LayoutTemplates);
                    if (parsingResult)
                    {
                        return new ParsingResult(true);
                    }

                    return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Layout_Templates_Message, dataString);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Layout templates parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }
            }

            return new ParsingResult(true);
        }

        public ParsingResult ParseCustomLayouts()
        {
            Logger.LogTrace();

            if (_fileSystem.File.Exists(FancyZonesCustomLayoutsFile))
            {
                CustomLayoutListWrapper wrapper;
                string dataString = string.Empty;

                try
                {
                    dataString = ReadFile(FancyZonesCustomLayoutsFile);
                    wrapper = JsonSerializer.Deserialize<CustomLayoutListWrapper>(dataString, _options);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Custom layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }

                try
                {
                    bool parsingResult = SetCustomLayouts(wrapper.CustomLayouts);
                    if (parsingResult)
                    {
                        return new ParsingResult(true);
                    }

                    return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Custom_Layouts_Message, dataString);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Custom layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }
            }

            return new ParsingResult(true);
        }

        public ParsingResult ParseDefaultLayouts()
        {
            Logger.LogTrace();

            if (_fileSystem.File.Exists(FancyZonesDefaultLayoutsFile))
            {
                DefaultLayoutsListWrapper wrapper;
                string dataString = string.Empty;

                try
                {
                    dataString = ReadFile(FancyZonesDefaultLayoutsFile);
                    wrapper = JsonSerializer.Deserialize<DefaultLayoutsListWrapper>(dataString, _options);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Default layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }

                try
                {
                    bool parsingResult = SetDefaultLayouts(wrapper.DefaultLayouts);
                    if (parsingResult)
                    {
                        return new ParsingResult(true);
                    }

                    return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Default_Layouts_Message, dataString);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Default layouts parsing error", ex);
                    return new ParsingResult(false, ex.Message, dataString);
                }
            }

            return new ParsingResult(true);
        }

        public void SerializeAppliedLayouts()
        {
            Logger.LogTrace();

            AppliedLayoutsListWrapper layouts = new AppliedLayoutsListWrapper { };
            layouts.AppliedLayouts = new List<AppliedLayoutWrapper>();

            // Serialize used layouts
            foreach (var monitor in App.Overlay.Monitors)
            {
                LayoutSettings zoneset = monitor.Settings;
                if (zoneset.ZonesetUuid.Length == 0)
                {
                    continue;
                }

                layouts.AppliedLayouts.Add(new AppliedLayoutWrapper
                {
                    Device = new AppliedLayoutWrapper.DeviceIdWrapper
                    {
                        Monitor = monitor.Device.MonitorName,
                        MonitorInstance = monitor.Device.MonitorInstanceId,
                        MonitorNumber = monitor.Device.MonitorNumber,
                        SerialNumber = monitor.Device.MonitorSerialNumber,
                        VirtualDesktop = monitor.Device.VirtualDesktopId,
                    },

                    AppliedLayout = new AppliedLayoutWrapper.LayoutWrapper
                    {
                        Uuid = zoneset.ZonesetUuid,
                        Type = LayoutTypeToJsonTag(zoneset.Type),
                        ShowSpacing = zoneset.ShowSpacing,
                        Spacing = zoneset.Spacing,
                        ZoneCount = zoneset.ZoneCount,
                        SensitivityRadius = zoneset.SensitivityRadius,
                    },
                });
            }

            // Serialize unused layouts
            foreach (var device in _unusedLayouts)
            {
                layouts.AppliedLayouts.Add(device);
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(layouts, _options);
                _fileSystem.File.WriteAllText(FancyZonesAppliedLayoutsFile, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize applied layouts error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeLayoutHotkeys()
        {
            LayoutHotkeysWrapper hotkeys = new LayoutHotkeysWrapper { };
            hotkeys.LayoutHotkeys = new List<LayoutHotkeyWrapper>();

            foreach (var pair in MainWindowSettingsModel.LayoutHotkeys.SelectedKeys)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    try
                    {
                        LayoutHotkeyWrapper wrapper = new LayoutHotkeyWrapper
                        {
                            Key = int.Parse(pair.Key, CultureInfo.CurrentCulture),
                            LayoutId = pair.Value,
                        };

                        hotkeys.LayoutHotkeys.Add(wrapper);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Serialize quick layout keys error", ex);
                    }
                }
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(hotkeys, _options);
                _fileSystem.File.WriteAllText(FancyZonesLayoutHotkeysFile, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize layout hotkeys error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeLayoutTemplates()
        {
            TemplateLayoutsListWrapper templates = new TemplateLayoutsListWrapper { };
            templates.LayoutTemplates = new List<TemplateLayoutWrapper>();

            foreach (LayoutModel layout in MainWindowSettingsModel.TemplateModels)
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

                templates.LayoutTemplates.Add(wrapper);
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(templates, _options);
                _fileSystem.File.WriteAllText(FancyZonesLayoutTemplatesFile, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize layout templates error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeCustomLayouts()
        {
            CustomLayoutListWrapper layouts = new CustomLayoutListWrapper { };
            layouts.CustomLayouts = new List<CustomLayoutWrapper>();

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

                layouts.CustomLayouts.Add(customLayout);
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(layouts, _options);
                _fileSystem.File.WriteAllText(FancyZonesCustomLayoutsFile, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize custom layouts error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeDefaultLayouts()
        {
            DefaultLayoutsListWrapper layouts = new DefaultLayoutsListWrapper { };
            layouts.DefaultLayouts = new List<DefaultLayoutWrapper>();

            foreach (LayoutModel layout in MainWindowSettingsModel.TemplateModels)
            {
                if (layout.IsHorizontalDefault || layout.IsVerticalDefault)
                {
                    DefaultLayoutWrapper.LayoutWrapper layoutWrapper = new DefaultLayoutWrapper.LayoutWrapper
                    {
                        Uuid = string.Empty,
                        Type = LayoutTypeToJsonTag(layout.Type),
                        SensitivityRadius = layout.SensitivityRadius,
                        ZoneCount = layout.TemplateZoneCount,
                    };

                    if (layout is GridLayoutModel grid)
                    {
                        layoutWrapper.ShowSpacing = grid.ShowSpacing;
                        layoutWrapper.Spacing = grid.Spacing;
                    }

                    // can be both horizontal and vertical, so check separately
                    if (layout.IsHorizontalDefault)
                    {
                        DefaultLayoutWrapper wrapper = new DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Horizontal),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }

                    if (layout.IsVerticalDefault)
                    {
                        DefaultLayoutWrapper wrapper = new DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Vertical),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }
                }
            }

            foreach (LayoutModel layout in MainWindowSettingsModel.CustomModels)
            {
                if (layout.IsHorizontalDefault || layout.IsVerticalDefault)
                {
                    DefaultLayoutWrapper.LayoutWrapper layoutWrapper = new DefaultLayoutWrapper.LayoutWrapper
                    {
                        Uuid = layout.Uuid,
                        Type = LayoutTypeToJsonTag(LayoutType.Custom),
                    };

                    if (layout is GridLayoutModel grid)
                    {
                        layoutWrapper.ShowSpacing = grid.ShowSpacing;
                        layoutWrapper.Spacing = grid.Spacing;
                    }

                    // can be both horizontal and vertical, so check separately
                    if (layout.IsHorizontalDefault)
                    {
                        DefaultLayoutWrapper wrapper = new DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Horizontal),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }

                    if (layout.IsVerticalDefault)
                    {
                        DefaultLayoutWrapper wrapper = new DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Vertical),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }
                }
            }

            try
            {
                string jsonString = JsonSerializer.Serialize(layouts, _options);
                _fileSystem.File.WriteAllText(FancyZonesDefaultLayoutsFile, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize default layout error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        private string ReadFile(string fileName)
        {
            Logger.LogTrace();

            var attempts = 0;
            while (attempts < 10)
            {
                try
                {
                    using (Stream inputStream = _fileSystem.File.Open(fileName, FileMode.Open))
                    using (StreamReader reader = new StreamReader(inputStream))
                    {
                        string data = reader.ReadToEnd();
                        inputStream.Close();
                        return data;
                    }
                }
                catch (Exception)
                {
                    Logger.LogError("File reading error, retry");
                    Task.Delay(10).Wait();
                }

                attempts++;
            }

            return string.Empty;
        }

        private bool SetAppliedLayouts(List<AppliedLayoutWrapper> layouts)
        {
            Logger.LogTrace();

            if (layouts == null)
            {
                return false;
            }

            bool result = true;
            var monitors = App.Overlay.Monitors;
            foreach (var layout in layouts)
            {
                if (layout.Device.Monitor == null ||
                    layout.Device.Monitor.Length == 0 ||
                    layout.Device.VirtualDesktop == null ||
                    layout.Device.VirtualDesktop.Length == 0 ||
                    layout.AppliedLayout.Uuid == null ||
                    layout.AppliedLayout.Uuid.Length == 0)
                {
                    result = false;
                    continue;
                }

                bool unused = true;
                foreach (Monitor monitor in monitors)
                {
                    if (monitor.Device.MonitorName == layout.Device.Monitor &&
                        monitor.Device.MonitorSerialNumber == layout.Device.SerialNumber &&
                        monitor.Device.MonitorNumber == layout.Device.MonitorNumber &&
                        (monitor.Device.VirtualDesktopId == layout.Device.VirtualDesktop ||
                        layout.Device.VirtualDesktop == DefaultVirtualDesktopGuid))
                    {
                        var settings = new LayoutSettings
                        {
                            ZonesetUuid = layout.AppliedLayout.Uuid,
                            ShowSpacing = layout.AppliedLayout.ShowSpacing,
                            Spacing = layout.AppliedLayout.Spacing,
                            Type = JsonTagToLayoutType(layout.AppliedLayout.Type),
                            ZoneCount = layout.AppliedLayout.ZoneCount,
                            SensitivityRadius = layout.AppliedLayout.SensitivityRadius,
                        };

                        monitor.Settings = settings;
                        unused = false;
                        break;
                    }
                }

                if (unused)
                {
                    _unusedLayouts.Add(layout);
                }
            }

            return result;
        }

        private bool SetCustomLayouts(List<CustomLayoutWrapper> customLayouts)
        {
            Logger.LogTrace();

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
                catch (Exception ex)
                {
                    result = false;
                    Logger.LogError("Parse custom layout error", ex);
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
            Logger.LogTrace();

            if (templateLayouts == null)
            {
                return false;
            }

            foreach (var wrapper in templateLayouts)
            {
                LayoutType type = JsonTagToLayoutType(wrapper.Type);
                LayoutModel layout = MainWindowSettingsModel.TemplateModels[(int)type];

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

        private bool SetLayoutHotkeys(LayoutHotkeysWrapper layoutHotkeys)
        {
            Logger.LogTrace();

            MainWindowSettingsModel.LayoutHotkeys.CleanUp();
            foreach (var wrapper in layoutHotkeys.LayoutHotkeys)
            {
                MainWindowSettingsModel.LayoutHotkeys.SelectKey(wrapper.Key.ToString(CultureInfo.CurrentCulture), wrapper.LayoutId);
            }

            return true;
        }

        private bool SetDefaultLayouts(List<DefaultLayoutWrapper> layouts)
        {
            Logger.LogTrace();

            if (layouts == null)
            {
                return false;
            }

            foreach (var layout in layouts)
            {
                if (layout.Layout.Uuid != null && layout.Layout.Uuid != string.Empty)
                {
                    MonitorConfigurationType type = JsonTagToMonitorConfigurationType(layout.MonitorConfiguration);

                    foreach (var customLayout in MainWindowSettingsModel.CustomModels)
                    {
                        if (customLayout.Uuid == layout.Layout.Uuid)
                        {
                            MainWindowSettingsModel.DefaultLayouts.Set(customLayout, type);
                            break;
                        }
                    }
                }
                else
                {
                    LayoutType layoutType = JsonTagToLayoutType(layout.Layout.Type);
                    MonitorConfigurationType type = JsonTagToMonitorConfigurationType(layout.MonitorConfiguration);
                    MainWindowSettingsModel.DefaultLayouts.Set(MainWindowSettingsModel.TemplateModels[(int)layoutType], type);
                }
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

        private MonitorConfigurationType JsonTagToMonitorConfigurationType(string tag)
        {
            switch (tag)
            {
                case HorizontalJsonTag:
                    return MonitorConfigurationType.Horizontal;
                case VerticalJsonTag:
                    return MonitorConfigurationType.Vertical;
            }

            return MonitorConfigurationType.Horizontal;
        }

        private string MonitorConfigurationTypeToJsonTag(MonitorConfigurationType type)
        {
            switch (type)
            {
                case MonitorConfigurationType.Horizontal:
                    return HorizontalJsonTag;
                case MonitorConfigurationType.Vertical:
                    return VerticalJsonTag;
            }

            return HorizontalJsonTag;
        }
    }
}
