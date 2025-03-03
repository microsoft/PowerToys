// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;

using FancyZonesEditorCommon.Utils;
using static FancyZonesEditorCommon.Data.AppliedLayouts;
using static FancyZonesEditorCommon.Data.AppliedLayouts.AppliedLayoutWrapper;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutHotkeys;
using static FancyZonesEditorCommon.Data.LayoutTemplates;

namespace FancyZonesEditorCommon.Data
{
    public class EditorData<T>
    {
        public string GetDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        protected JsonSerializerOptions JsonOptions
        {
            get
            {
                return new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                };
            }
        }

        public AppliedLayoutsListWrapper ReadAppliedLayout(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            AppliedLayoutsListWrapper res = new AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayoutWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("applied-layouts", out JsonElement appliedLayoutsElement) && appliedLayoutsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in appliedLayoutsElement.EnumerateArray())
                    {
                        var device = item.GetProperty("device");
                        var appliedLayout = item.GetProperty("applied-layout");

                        res.AppliedLayouts.Add(new AppliedLayoutWrapper
                        {
                            Device = new DeviceIdWrapper
                            {
                                Monitor = device.GetProperty("monitor").GetString(),
                                MonitorInstance = device.GetProperty("monitor-instance").GetString(),
                                MonitorNumber = device.GetProperty("monitor-number").GetInt32(),
                                SerialNumber = device.GetProperty("serial-number").GetString(),
                                VirtualDesktop = device.GetProperty("virtual-desktop").GetString(),
                            },
                            AppliedLayout = new LayoutWrapper
                            {
                                Uuid = appliedLayout.GetProperty("uuid").GetString(),
                                Type = appliedLayout.GetProperty("type").GetString(),
                                ShowSpacing = appliedLayout.GetProperty("show-spacing").GetBoolean(),
                                Spacing = appliedLayout.GetProperty("spacing").GetInt32(),
                                ZoneCount = appliedLayout.GetProperty("zone-count").GetInt32(),
                                SensitivityRadius = appliedLayout.GetProperty("sensitivity-radius").GetInt32(),
                            },
                        });
                    }
                }
            }

            return res;
        }

        public ParamsWrapper ReadEditorParams(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            ParamsWrapper res = new ParamsWrapper()
            {
                ProcessId = 0,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;

                res.ProcessId = root.GetProperty("process-id").GetInt32();
                res.SpanZonesAcrossMonitors = root.GetProperty("span-zones-across-monitors").GetBoolean();
                res.Monitors = new List<NativeMonitorDataWrapper>();

                JsonElement monitorsElement = root.GetProperty("monitors");
                foreach (JsonElement monitorElement in monitorsElement.EnumerateArray())
                {
                    res.Monitors.Add(new NativeMonitorDataWrapper
                    {
                        Monitor = monitorElement.GetProperty("monitor").GetString(),
                        MonitorInstanceId = monitorElement.GetProperty("monitor-instance-id").GetString(),
                        MonitorSerialNumber = monitorElement.GetProperty("monitor-serial-number").GetString(),
                        MonitorNumber = monitorElement.GetProperty("monitor-number").GetInt32(),
                        VirtualDesktop = monitorElement.GetProperty("virtual-desktop").GetString(),
                        Dpi = monitorElement.GetProperty("dpi").GetInt32(),
                        TopCoordinate = monitorElement.GetProperty("top-coordinate").GetInt32(),
                        LeftCoordinate = monitorElement.GetProperty("left-coordinate").GetInt32(),
                        WorkAreaWidth = monitorElement.GetProperty("work-area-width").GetInt32(),
                        WorkAreaHeight = monitorElement.GetProperty("work-area-height").GetInt32(),
                        MonitorWidth = monitorElement.GetProperty("monitor-width").GetInt32(),
                        MonitorHeight = monitorElement.GetProperty("monitor-height").GetInt32(),
                        IsSelected = monitorElement.GetProperty("is-selected").GetBoolean(),
                    });
                }
            }

            return res;
        }

        public LayoutHotkeysWrapper ReadLayoutHotkeys(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            LayoutHotkeysWrapper res = new LayoutHotkeysWrapper()
            {
                LayoutHotkeys = new List<LayoutHotkeyWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;

                JsonElement layoutElements = root.GetProperty("layout-hotkeys");
                foreach (JsonElement layoutElement in layoutElements.EnumerateArray())
                {
                    res.LayoutHotkeys.Add(new LayoutHotkeyWrapper
                    {
                        Key = layoutElement.GetProperty("key").GetInt32(),
                        LayoutId = layoutElement.GetProperty("layout-id").GetString(),
                    });
                }
            }

            return res;
        }

        public TemplateLayoutsListWrapper ReadTemplateLayouts(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            TemplateLayoutsListWrapper res = new TemplateLayoutsListWrapper()
            {
                LayoutTemplates = new List<TemplateLayoutWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;

                JsonElement layoutElements = root.GetProperty("layout-templates");
                foreach (JsonElement layoutElement in layoutElements.EnumerateArray())
                {
                    res.LayoutTemplates.Add(new TemplateLayoutWrapper
                    {
                        Type = layoutElement.GetProperty("type").GetString(),
                        ShowSpacing = layoutElement.GetProperty("show-spacing").GetBoolean(),
                        Spacing = layoutElement.GetProperty("spacing").GetInt32(),
                        ZoneCount = layoutElement.GetProperty("zone-count").GetInt32(),
                        SensitivityRadius = layoutElement.GetProperty("sensitivity-radius").GetInt32(),
                    });
                }
            }

            return res;
        }

        public CustomLayoutListWrapper ReadCustomLayout(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            CustomLayoutListWrapper res = new CustomLayoutListWrapper()
            {
                CustomLayouts = new List<CustomLayoutWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;

                JsonElement layoutElements = root.GetProperty("custom-layouts");
                foreach (JsonElement layoutElement in layoutElements.EnumerateArray())
                {
                    res.CustomLayouts.Add(new CustomLayoutWrapper
                    {
                        Uuid = layoutElement.GetProperty("uuid").GetString(),
                        Name = layoutElement.GetProperty("name").GetString(),
                        Type = layoutElement.GetProperty("type").GetString(),
                        Info = layoutElement.GetProperty("info"),
                    });
                }
            }

            return res;
        }

        public DefaultLayoutsListWrapper ReadDefaultLayouts(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            DefaultLayoutsListWrapper res = new DefaultLayoutsListWrapper()
            {
                DefaultLayouts = new List<DefaultLayoutWrapper>(),
            };

            using (JsonDocument doc = JsonDocument.Parse(data))
            {
                JsonElement root = doc.RootElement;

                JsonElement layoutElements = root.GetProperty("default-layouts");
                foreach (JsonElement layoutElement in layoutElements.EnumerateArray())
                {
                    DefaultLayoutWrapper layout = new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = layoutElement.GetProperty("monitor-configuration").GetString(),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Uuid = layoutElement.GetProperty("layout").GetProperty("uuid").GetString(),
                            Type = layoutElement.GetProperty("layout").GetProperty("type").GetString(),
                            ShowSpacing = layoutElement.GetProperty("layout").GetProperty("show-spacing").GetBoolean(),
                            Spacing = layoutElement.GetProperty("layout").GetProperty("spacing").GetInt32(),
                            ZoneCount = layoutElement.GetProperty("layout").GetProperty("zone-count").GetInt32(),
                            SensitivityRadius = layoutElement.GetProperty("layout").GetProperty("sensitivity-radius").GetInt32(),
                        },
                    };
                }
            }

            return res;
        }

        public string Serialize(T data)
        {
            return JsonSerializer.Serialize(data, JsonOptions);
        }
    }
}
