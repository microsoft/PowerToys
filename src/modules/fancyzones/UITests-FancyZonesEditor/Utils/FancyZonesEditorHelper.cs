// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.FancyZonesEditor.UnitTests.Utils
{
    public class FancyZonesEditorHelper
    {
        private static FancyZonesEditorFiles? _files;

        public static FancyZonesEditorFiles Files
        {
            get
            {
                if (_files == null)
                {
                    _files = new FancyZonesEditorFiles();
                }

                return _files;
            }
        }

        public static class AccessibilityId
        {
            // main window
            public const string MainWindow = "MainWindow1";
            public const string Monitors = "Monitors";
            public const string NewLayoutButton = "NewLayoutButton";

            // layout card
            public const string EditLayoutButton = "EditLayoutButton";

            // edit layout window: common for template and custom layouts
            public const string DialogTitle = "EditLayoutDialogTitle";
            public const string SensitivitySlider = "SensitivityInput";
            public const string SpacingSlider = "Spacing";
            public const string SpacingToggle = "spaceAroundSetting";
            public const string HorizontalDefaultButtonUnchecked = "SetLayoutAsHorizontalDefaultButton";
            public const string VerticalDefaultButtonUnchecked = "SetLayoutAsVerticalDefaultButton";
            public const string HorizontalDefaultButtonChecked = "HorizontalDefaultLayoutButton";
            public const string VerticalDefaultButtonChecked = "VerticalDefaultLayoutButton";

            // edit template layout window
            public const string CopyTemplate = "createFromTemplateLayoutButton";
            public const string TemplateZoneSlider = "TemplateZoneCount";

            // edit custom layout window
            public const string DuplicateLayoutButton = "duplicateLayoutButton";
            public const string DeleteLayoutButton = "deleteLayoutButton";
            public const string KeySelectionComboBox = "quickKeySelectionComboBox";
            public const string EditZonesButton = "editZoneLayoutButton";
            public const string DeleteTextButton = "DeleteButton";
            public const string HotkeyComboBox = "quickKeySelectionComboBox";
            public const string NewZoneButton = "newZoneButton";
            public const string TopRightCorner = "NEResize";

            // layout creation dialog
            public const string GridRadioButton = "GridLayoutRadioButton";
            public const string CanvasRadioButton = "CanvasLayoutRadioButton";

            // confirmation dialog
            public const string PrimaryButton = "PrimaryButton";
            public const string SecondaryButton = "SecondaryButton";
        }

        public static class ElementName
        {
            public const string Save = "Save";
            public const string Cancel = "Cancel";

            // context menu
            public const string Edit = "Edit";
            public const string EditZones = "Edit zones";
            public const string Delete = "Delete";
            public const string Duplicate = "Duplicate";
            public const string CreateCustomLayout = "Create custom layout";

            // canvas layout editor
            public const string CanvasEditorWindow = "Canvas layout editor";

            // grid layout editor
            public const string GridLayoutEditor = "Grid layout editor";
            public const string MergeZonesButton = "Merge zones";
        }

        public static class ClassName
        {
            public const string ContextMenu = "ContextMenu";
            public const string TextBox = "TextBox";
            public const string Popup = "Popup";

            // layout editor
            public const string CanvasZone = "CanvasZone";
            public const string GridZone = "GridZone";
            public const string Button = "Button";
            public const string Thumb = "Thumb";
        }

        public static void ClickContextMenuItem(Session session, string layoutName, string menuItem)
        {
            session.Find<Element>(layoutName).Click(true);
            session.Find<Element>(By.ClassName(ClassName.ContextMenu)).Find<Element>(menuItem).Click();
        }

        public static Element? GetZone(Session session, int zoneNumber, string zoneClassName)
        {
            var zones = session.FindAll<Element>(By.ClassName(zoneClassName));
            foreach (var zone in zones)
            {
                try
                {
                    zone.Find<Element>(zoneNumber.ToString(CultureInfo.InvariantCulture));
                    Assert.IsNotNull(zone, "zone not found");
                    return zone;
                }
                catch
                {
                    // required number not found in the zone
                }
            }

            Assert.IsNotNull(zones, $"zoneClassName : {zoneClassName} not found");
            return null;
        }

        public static void MergeGridZones(Session session, int zoneNumber1, int zoneNumber2)
        {
            var zone1 = GetZone(session, zoneNumber1, ClassName.GridZone);
            var zone2 = GetZone(session, zoneNumber2, ClassName.GridZone);
            Assert.IsNotNull(zone1, "first zone not found");
            Assert.IsNotNull(zone2, "second zone not found");
            if (zone1 == null || zone2 == null)
            {
                Assert.Fail("zone is null");
                return;
            }

            zone1.Drag(zone2);

            session.Find<Element>(ElementName.MergeZonesButton).Click();
        }

        public static void MoveSplitter(Session session, int index, int xOffset, int yOffset)
        {
            var thumbs = session.FindAll<Element>(By.ClassName(ClassName.Thumb));
            if (thumbs.Count == 0 || index >= thumbs.Count)
            {
                return;
            }

            thumbs[index].Drag(xOffset, yOffset);
            Console.WriteLine($"Moving splitter {index} by ({xOffset}, {yOffset})");
        }

        public static void ClickDeleteZone(Session session, int zoneNumber)
        {
            var zone = FancyZonesEditorHelper.GetZone(session, zoneNumber, ClassName.CanvasZone);
            Assert.IsNotNull(zone);
            var button = zone.Find<Button>(By.ClassName(ClassName.Button));
            Assert.IsNotNull(button);
            button.Click();
        }

        public static void InitFancyZonesLayout()
        {
            // prepare files to launch Editor without errors
            EditorParameters editorParameters = new EditorParameters();
            EditorParameters.ParamsWrapper parameters = new EditorParameters.ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<EditorParameters.NativeMonitorDataWrapper>
                {
                    new EditorParameters.NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 96,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty],
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus],
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid],
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid],
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));
        }
    }
}
