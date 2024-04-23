// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;
using ManagedCommon;

namespace FancyZonesEditor.Utils
{
    public class FancyZonesEditorIO
    {
        // Non-localizable strings: JSON tags
        private const string HorizontalJsonTag = "horizontal";
        private const string VerticalJsonTag = "vertical";

        // Non-localizable string: default virtual desktop id
        private const string DefaultVirtualDesktopGuid = "{00000000-0000-0000-0000-000000000000}";

        private List<AppliedLayouts.AppliedLayoutWrapper> _unusedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>();

        public FancyZonesEditorIO()
        {
        }

        public ParsingResult ParseParams()
        {
            Logger.LogTrace();

            try
            {
                EditorParameters parser = new EditorParameters();
                var editorParams = parser.Read(parser.File);

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

                    foreach (EditorParameters.NativeMonitorDataWrapper nativeData in editorParams.Monitors)
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
                        return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Editor_Parameters_Message);
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

                return new ParsingResult(true);
            }
            catch (Exception e)
            {
                return new ParsingResult(false, e.Message);
            }
        }

        public ParsingResult ParseAppliedLayouts()
        {
            Logger.LogTrace();

            _unusedLayouts.Clear();

            try
            {
                AppliedLayouts parser = new AppliedLayouts();
                var appliedLayouts = parser.Read(parser.File);

                bool parsingResult = SetAppliedLayouts(appliedLayouts.AppliedLayouts);
                if (!parsingResult)
                {
                    return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Applied_Layouts_Message);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Applied layouts parsing error", ex);
                return new ParsingResult(false, ex.Message);
            }

            return new ParsingResult(true);
        }

        public ParsingResult ParseLayoutHotkeys()
        {
            Logger.LogTrace();

            try
            {
                LayoutHotkeys parser = new LayoutHotkeys();
                if (!File.Exists(parser.File))
                {
                    return new ParsingResult(true);
                }

                var layoutHotkeys = parser.Read(parser.File);
                bool layoutHotkeysParsingResult = SetLayoutHotkeys(layoutHotkeys);
                if (!layoutHotkeysParsingResult)
                {
                    return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Layout_Hotkeys_Message);
                }

                return new ParsingResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Layout hotkeys parsing error", ex);
                return new ParsingResult(false, ex.Message);
            }
        }

        public ParsingResult ParseLayoutTemplates()
        {
            Logger.LogTrace();

            try
            {
                LayoutTemplates parser = new LayoutTemplates();
                if (!File.Exists(parser.File))
                {
                    return new ParsingResult(true);
                }

                var templates = parser.Read(parser.File);
                bool parsingResult = SetTemplateLayouts(templates.LayoutTemplates);
                if (parsingResult)
                {
                    return new ParsingResult(true);
                }

                return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Layout_Templates_Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Layout templates parsing error", ex);
                return new ParsingResult(false, ex.Message);
            }
        }

        public ParsingResult ParseCustomLayouts()
        {
            Logger.LogTrace();

            try
            {
                CustomLayouts parser = new CustomLayouts();
                if (!File.Exists(parser.File))
                {
                    return new ParsingResult(true);
                }

                var wrapper = parser.Read(parser.File);
                bool parsingResult = SetCustomLayouts(wrapper.CustomLayouts);
                if (parsingResult)
                {
                    return new ParsingResult(true);
                }

                return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Custom_Layouts_Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Custom layouts parsing error", ex);
                return new ParsingResult(false, ex.Message);
            }
        }

        public ParsingResult ParseDefaultLayouts()
        {
            Logger.LogTrace();

            try
            {
                DefaultLayouts parser = new DefaultLayouts();
                if (!File.Exists(parser.File))
                {
                    return new ParsingResult(true);
                }

                var wrapper = parser.Read(parser.File);
                bool parsingResult = SetDefaultLayouts(wrapper.DefaultLayouts);
                if (parsingResult)
                {
                    return new ParsingResult(true);
                }

                return new ParsingResult(false, FancyZonesEditor.Properties.Resources.Error_Parsing_Default_Layouts_Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Default layouts parsing error", ex);
                return new ParsingResult(false, ex.Message);
            }
        }

        public void SerializeAppliedLayouts()
        {
            Logger.LogTrace();

            AppliedLayouts.AppliedLayoutsListWrapper layouts = new AppliedLayouts.AppliedLayoutsListWrapper { };
            layouts.AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>();

            // Serialize used layouts
            foreach (var monitor in App.Overlay.Monitors)
            {
                LayoutSettings zoneset = monitor.Settings;
                if (zoneset.ZonesetUuid.Length == 0)
                {
                    continue;
                }

                layouts.AppliedLayouts.Add(new AppliedLayouts.AppliedLayoutWrapper
                {
                    Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                    {
                        Monitor = monitor.Device.MonitorName,
                        MonitorInstance = monitor.Device.MonitorInstanceId,
                        MonitorNumber = monitor.Device.MonitorNumber,
                        SerialNumber = monitor.Device.MonitorSerialNumber,
                        VirtualDesktop = monitor.Device.VirtualDesktopId,
                    },

                    AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
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
                AppliedLayouts serializer = new AppliedLayouts();
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(serializer.File, serializer.Serialize(layouts));
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize applied layouts error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeLayoutHotkeys()
        {
            LayoutHotkeys.LayoutHotkeysWrapper hotkeys = new LayoutHotkeys.LayoutHotkeysWrapper { };
            hotkeys.LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper>();

            foreach (var pair in MainWindowSettingsModel.LayoutHotkeys.SelectedKeys)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    try
                    {
                        LayoutHotkeys.LayoutHotkeyWrapper wrapper = new LayoutHotkeys.LayoutHotkeyWrapper
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
                LayoutHotkeys serializer = new LayoutHotkeys();
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(serializer.File, serializer.Serialize(hotkeys));
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize layout hotkeys error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeLayoutTemplates()
        {
            LayoutTemplates.TemplateLayoutsListWrapper templates = new LayoutTemplates.TemplateLayoutsListWrapper { };
            templates.LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>();

            foreach (LayoutModel layout in MainWindowSettingsModel.TemplateModels)
            {
                LayoutTemplates.TemplateLayoutWrapper wrapper = new LayoutTemplates.TemplateLayoutWrapper
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
                LayoutTemplates serializer = new LayoutTemplates();
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(serializer.File, serializer.Serialize(templates));
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize layout templates error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeCustomLayouts()
        {
            CustomLayouts serializer = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper layouts = new CustomLayouts.CustomLayoutListWrapper { };
            layouts.CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>();

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

                    var wrapper = new CustomLayouts.CanvasInfoWrapper
                    {
                        RefWidth = (int)canvasRect.Width,
                        RefHeight = (int)canvasRect.Height,
                        Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper>(),
                        SensitivityRadius = canvasLayout.SensitivityRadius,
                    };

                    foreach (var zone in canvasLayout.Zones)
                    {
                        wrapper.Zones.Add(new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                        {
                            X = zone.X,
                            Y = zone.Y,
                            Width = zone.Width,
                            Height = zone.Height,
                        });
                    }

                    info = serializer.ToJsonElement(wrapper);
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

                    var wrapper = new CustomLayouts.GridInfoWrapper
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

                    info = serializer.ToJsonElement(wrapper);
                }
                else
                {
                    // Error
                    continue;
                }

                CustomLayouts.CustomLayoutWrapper customLayout = new CustomLayouts.CustomLayoutWrapper
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
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(serializer.File, serializer.Serialize(layouts));
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize custom layouts error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        public void SerializeDefaultLayouts()
        {
            DefaultLayouts.DefaultLayoutsListWrapper layouts = new DefaultLayouts.DefaultLayoutsListWrapper { };
            layouts.DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>();

            foreach (LayoutModel layout in MainWindowSettingsModel.TemplateModels)
            {
                if (layout.IsHorizontalDefault || layout.IsVerticalDefault)
                {
                    DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper layoutWrapper = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
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
                        DefaultLayouts.DefaultLayoutWrapper wrapper = new DefaultLayouts.DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Horizontal),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }

                    if (layout.IsVerticalDefault)
                    {
                        DefaultLayouts.DefaultLayoutWrapper wrapper = new DefaultLayouts.DefaultLayoutWrapper
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
                    DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper layoutWrapper = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
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
                        DefaultLayouts.DefaultLayoutWrapper wrapper = new DefaultLayouts.DefaultLayoutWrapper
                        {
                            MonitorConfiguration = MonitorConfigurationTypeToJsonTag(MonitorConfigurationType.Horizontal),
                            Layout = layoutWrapper,
                        };

                        layouts.DefaultLayouts.Add(wrapper);
                    }

                    if (layout.IsVerticalDefault)
                    {
                        DefaultLayouts.DefaultLayoutWrapper wrapper = new DefaultLayouts.DefaultLayoutWrapper
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
                DefaultLayouts serializer = new DefaultLayouts();
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(serializer.File, serializer.Serialize(layouts));
            }
            catch (Exception ex)
            {
                Logger.LogError("Serialize default layout error", ex);
                App.ShowExceptionMessageBox(Properties.Resources.Error_Applying_Layout, ex);
            }
        }

        private bool SetAppliedLayouts(List<AppliedLayouts.AppliedLayoutWrapper> layouts)
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

                LayoutType layoutType = JsonTagToLayoutType(layout.AppliedLayout.Type);
                LayoutSettings settings = new LayoutSettings
                {
                    ZonesetUuid = layout.AppliedLayout.Uuid,
                    ShowSpacing = layout.AppliedLayout.ShowSpacing,
                    Spacing = layout.AppliedLayout.Spacing,
                    Type = layoutType,
                    ZoneCount = layout.AppliedLayout.ZoneCount,
                    SensitivityRadius = layout.AppliedLayout.SensitivityRadius,
                };

                // check if the custom layout exists
                bool existingLayout = layoutType != LayoutType.Custom;
                if (layoutType == LayoutType.Custom)
                {
                    foreach (LayoutModel custom in MainWindowSettingsModel.CustomModels)
                    {
                        if (custom.Uuid == layout.AppliedLayout.Uuid)
                        {
                            existingLayout = true;
                            break;
                        }
                    }
                }

                // replace deleted layout with the Blank layout
                if (!existingLayout)
                {
                    LayoutModel blankLayout = MainWindowSettingsModel.TemplateModels[(int)LayoutType.Blank];
                    settings.ZonesetUuid = blankLayout.Uuid;
                    settings.Type = blankLayout.Type;
                    settings.ZoneCount = blankLayout.TemplateZoneCount;
                    settings.SensitivityRadius = blankLayout.SensitivityRadius;

                    // grid layout settings, just resetting them
                    settings.ShowSpacing = false;
                    settings.Spacing = 0;
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

        private bool SetCustomLayouts(List<CustomLayouts.CustomLayoutWrapper> customLayouts)
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

        private bool SetTemplateLayouts(List<LayoutTemplates.TemplateLayoutWrapper> templateLayouts)
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

        private bool SetLayoutHotkeys(LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeys)
        {
            Logger.LogTrace();

            MainWindowSettingsModel.LayoutHotkeys.CleanUp();
            foreach (var wrapper in layoutHotkeys.LayoutHotkeys)
            {
                MainWindowSettingsModel.LayoutHotkeys.SelectKey(wrapper.Key.ToString(CultureInfo.CurrentCulture), wrapper.LayoutId);
            }

            return true;
        }

        private bool SetDefaultLayouts(List<DefaultLayouts.DefaultLayoutWrapper> layouts)
        {
            Logger.LogTrace();

            if (layouts == null)
            {
                return false;
            }

            foreach (var layout in layouts)
            {
                LayoutModel defaultLayoutModel = null;
                MonitorConfigurationType type = JsonTagToMonitorConfigurationType(layout.MonitorConfiguration);

                if (layout.Layout.Uuid != null && layout.Layout.Uuid != string.Empty)
                {
                    foreach (var customLayout in MainWindowSettingsModel.CustomModels)
                    {
                        if (customLayout.Uuid == layout.Layout.Uuid)
                        {
                            MainWindowSettingsModel.DefaultLayouts.Set(customLayout, type);
                            defaultLayoutModel = customLayout;
                            break;
                        }
                    }
                }
                else
                {
                    LayoutType layoutType = JsonTagToLayoutType(layout.Layout.Type);
                    defaultLayoutModel = MainWindowSettingsModel.TemplateModels[(int)layoutType];
                    defaultLayoutModel.TemplateZoneCount = layout.Layout.ZoneCount;
                    defaultLayoutModel.SensitivityRadius = layout.Layout.SensitivityRadius;

                    if (defaultLayoutModel is GridLayoutModel gridDefaultLayoutModel)
                    {
                        gridDefaultLayoutModel.ShowSpacing = layout.Layout.ShowSpacing;
                        gridDefaultLayoutModel.Spacing = layout.Layout.Spacing;
                    }

                    MainWindowSettingsModel.DefaultLayouts.Set(defaultLayoutModel, type);
                }

                if (defaultLayoutModel != null)
                {
                    foreach (Monitor monitor in App.Overlay.Monitors)
                    {
                        if (!monitor.IsInitialized && monitor.MonitorConfigurationType == type)
                        {
                            monitor.SetLayoutSettings(defaultLayoutModel);
                        }
                    }
                }
            }

            return true;
        }

        private CanvasLayoutModel ParseCanvasInfo(CustomLayouts.CustomLayoutWrapper wrapper)
        {
            CustomLayouts deserializer = new CustomLayouts();
            var info = deserializer.CanvasFromJsonElement(wrapper.Info.GetRawText());

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

        private GridLayoutModel ParseGridInfo(CustomLayouts.CustomLayoutWrapper wrapper)
        {
            CustomLayouts deserializer = new CustomLayouts();
            var info = deserializer.GridFromJsonElement(wrapper.Info.GetRawText());

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
            if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty])
            {
                return LayoutType.Blank;
            }
            else if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus])
            {
                return LayoutType.Focus;
            }
            else if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows])
            {
                return LayoutType.Rows;
            }
            else if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns])
            {
                return LayoutType.Columns;
            }
            else if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid])
            {
                return LayoutType.Grid;
            }
            else if (tag == Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid])
            {
                return LayoutType.PriorityGrid;
            }

            return LayoutType.Custom;
        }

        private string LayoutTypeToJsonTag(LayoutType type)
        {
            switch (type)
            {
                case LayoutType.Blank:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty];
                case LayoutType.Focus:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus];
                case LayoutType.Columns:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns];
                case LayoutType.Rows:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows];
                case LayoutType.Grid:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid];
                case LayoutType.PriorityGrid:
                    return Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid];
                case LayoutType.Custom:
                    return Constants.CustomLayoutJsonTag;
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
