// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class DefaultLayoutsTest : UITestBase
    {
        public DefaultLayoutsTest()
            : base(PowerToysModule.FancyZone)
        {
        }

        private static readonly string Vertical = MonitorConfigurationType.Vertical.TypeToString();
        private static readonly string Horizontal = MonitorConfigurationType.Horizontal.TypeToString();

        private static readonly CustomLayoutListWrapper CustomLayouts = new CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Layout 0",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 1080,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper> { },
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Layout 1",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 1080,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper> { },
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Layout 2",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 1080,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper> { },
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{F5FDBC04-0760-4776-9F05-96AAC4AE613F}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Layout 3",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 1080,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper> { },
                    }),
                },
            },
        };

        private static readonly DefaultLayoutsListWrapper Layouts = new DefaultLayoutsListWrapper
        {
            DefaultLayouts = new List<DefaultLayoutWrapper>
            {
                new DefaultLayoutWrapper
                {
                    MonitorConfiguration = Horizontal,
                    Layout = new DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = LayoutType.Grid.TypeToString(),
                        ZoneCount = 4,
                        ShowSpacing = true,
                        Spacing = 5,
                        SensitivityRadius = 20,
                    },
                },
                new DefaultLayoutWrapper
                {
                    MonitorConfiguration = Vertical,
                    Layout = new DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "custom",
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        ZoneCount = 0,
                        ShowSpacing = false,
                        Spacing = 0,
                        SensitivityRadius = 0,
                    },
                },
            },
        };

        [TestInitialize]
        public void TestInitialize()
        {
            var defaultLayouts = new DefaultLayouts();
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(Layouts));

            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-2",
                        MonitorInstanceId = "instance-id-2",
                        MonitorSerialNumber = "serial-number-2",
                        MonitorNumber = 2,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 96,
                        LeftCoordinate = 1920,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = false,
                    },
                },
            };
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayouts));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Blank.TypeToString(),
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Focus.TypeToString(),
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Rows.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Columns.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Grid.TypeToString(),
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.PriorityGrid.TypeToString(),
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

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

            this.RestartScopeExe();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void Initialize()
        {
            CheckTemplateLayouts(LayoutType.Grid, null);
            CheckCustomLayouts(string.Empty, CustomLayouts.CustomLayouts[0].Uuid);
        }

        [TestMethod]
        public void Assign_Cancel()
        {
            // assign Focus as a default horizontal and vertical layout
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Focus]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked)).Click();

            // cancel
            Session.Find<Button>(ElementName.Cancel).Click();

            // check that default layouts weren't changed
            CheckTemplateLayouts(LayoutType.Grid, null);
            CheckCustomLayouts(string.Empty, CustomLayouts.CustomLayouts[0].Uuid);
        }

        [TestMethod]
        public void Assign_Save()
        {
            // assign Focus as a default horizontal and vertical layout
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Focus]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked)).Click();

            // save
            Session.Find<Button>(ElementName.Save).Click();

            // check that default layout was changed
            CheckTemplateLayouts(LayoutType.Focus, LayoutType.Focus);
            CheckCustomLayouts(string.Empty, string.Empty);
        }

        private void CheckTemplateLayouts(LayoutType? horizontalDefault, LayoutType? verticalDefault)
        {
            foreach (var (key, name) in TestConstants.TemplateLayoutNames)
            {
                if (key == LayoutType.Blank)
                {
                    continue;
                }

                Session.Find<Element>(name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

                bool isCheckedHorizontal = key == horizontalDefault;
                bool isCheckedVertical = key == verticalDefault;

                Button? horizontalDefaultButton;
                Button? verticalDefaultButton;

                if (isCheckedHorizontal)
                {
                    horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked));
                }
                else
                {
                    horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked));
                }

                if (isCheckedVertical)
                {
                    verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonChecked));
                }
                else
                {
                    verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked));
                }

                Assert.IsNotNull(horizontalDefaultButton, "Incorrect horizontal default layout set at " + name);
                Assert.IsNotNull(verticalDefaultButton, "Incorrect vertical default layout set at " + name);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }

        private void CheckCustomLayouts(string horizontalDefaultLayoutUuid, string verticalDefaultLayoutUuid)
        {
            foreach (var layout in CustomLayouts.CustomLayouts)
            {
                Session.Find<Element>(layout.Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

                bool isCheckedHorizontal = layout.Uuid == horizontalDefaultLayoutUuid;
                bool isCheckedVertical = layout.Uuid == verticalDefaultLayoutUuid;
                Button? horizontalDefaultButton;
                Button? verticalDefaultButton;

                if (isCheckedHorizontal)
                {
                    horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked));
                }
                else
                {
                    horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked));
                }

                if (isCheckedVertical)
                {
                    verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonChecked));
                }
                else
                {
                    verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked));
                }

                Assert.IsNotNull(horizontalDefaultButton, "Incorrect horizontal custom layout set at " + layout.Name);
                Assert.IsNotNull(verticalDefaultButton, "Incorrect vertical custom layout set at " + layout.Name);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }
    }
}
