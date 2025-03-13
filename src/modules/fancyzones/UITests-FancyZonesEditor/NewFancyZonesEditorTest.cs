// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;
using static FancyZonesEditorCommon.Data.EditorParameters;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class NewFancyZonesEditorTest
    {
        public NewFancyZonesEditorTest()
        {
            // FancyZonesEditorHelper.InitFancyZonesLayout();
        }

        [TestClass]
        public class TestCaseFirstLaunch : UITestBase
        {
            public TestCaseFirstLaunch()
                : base(PowerToysModule.FancyZone)
            {
            }

            [TestInitialize]
            public void TestInitialize()
            {
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
                        Dpi = 192, // 200% scaling
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

                // files not yet exist
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

                CustomLayouts customLayouts = new CustomLayouts();
                CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
                {
                    CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
                };
                FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

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

                // verify editor opens without errors
                this.RestartScopeExe();
            }

            [TestMethod]
            public void FirstLaunch() // verify the session is initialized
            {
                Assert.IsNotNull(Session.Find<Element>("FancyZones Layout"));
            }
        }
    }
}
