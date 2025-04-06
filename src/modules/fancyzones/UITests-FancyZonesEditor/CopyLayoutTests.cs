// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CopyLayoutTests : UITestBase
    {
        public CopyLayoutTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayouts = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2967, 7033 },
                        ColumnsPercentage = new List<int> { 2410, 6040, 1550 },
                        CellChildMap = new int[][] { [0, 1, 1], [0, 2, 3] },
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
            },
        };

        private static readonly LayoutHotkeys.LayoutHotkeysWrapper Hotkeys = new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper>
            {
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    LayoutId = CustomLayouts.CustomLayouts[0].Uuid,
                    Key = 0,
                },
            },
        };

        private static readonly DefaultLayouts.DefaultLayoutsListWrapper DefaultLayouts = new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
            {
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = MonitorConfigurationType.Vertical.TypeToString(),
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "custom",
                        Uuid = CustomLayouts.CustomLayouts[0].Uuid,
                    },
                },
            },
        };

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
                        Dpi = 192,
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

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(DefaultLayouts));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(Hotkeys));

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
        public void CopyTemplate_FromEditLayoutWindow()
        {
            string copiedLayoutName = TestConstants.TemplateLayoutNames[LayoutType.Focus] + " (1)";
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Focus]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyTemplate_FromContextMenu()
        {
            string copiedLayoutName = TestConstants.TemplateLayoutNames[LayoutType.Rows] + " (1)";
            FancyZonesEditorHelper.ClickContextMenuItem(Session, TestConstants.TemplateLayoutNames[LayoutType.Rows], ElementName.CreateCustomLayout);

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyTemplate_DefaultLayout()
        {
            string copiedLayoutName = TestConstants.TemplateLayoutNames[LayoutType.PriorityGrid] + " (1)";
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.PriorityGrid]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the default layout wasn't changed
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.PriorityGrid]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked));
            Assert.IsNotNull(horizontalDefaultButton);
            Session.Find<Button>(ElementName.Cancel).Click();

            Session.Find<Element>(copiedLayoutName).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            horizontalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonUnchecked));
            Assert.IsNotNull(horizontalDefaultButton);
            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the default layouts file wasn't changed
            var defaultLayouts = new DefaultLayouts();
            var defaultLayoutData = defaultLayouts.Read(defaultLayouts.File);
            Assert.AreEqual(defaultLayouts.Serialize(DefaultLayouts), defaultLayouts.Serialize(defaultLayoutData));
        }

        [TestMethod]
        public void CopyCustomLayout_FromEditLayoutWindow()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyCustomLayout_FromContextMenu()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            FancyZonesEditorHelper.ClickContextMenuItem(Session, CustomLayouts.CustomLayouts[0].Name, ElementName.Duplicate);

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyCustomLayout_DefaultLayout()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the default layout wasn't changed
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonChecked));
            Assert.IsNotNull(verticalDefaultButton);
            Session.Find<Button>(ElementName.Cancel).Click();

            Session.Find<Element>(copiedLayoutName).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            verticalDefaultButton = Session.Find<Button>(By.AccessibilityId(AccessibilityId.VerticalDefaultButtonUnchecked));
            Assert.IsNotNull(verticalDefaultButton);
            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the default layouts file wasn't changed
            var defaultLayouts = new DefaultLayouts();
            var defaultLayoutData = defaultLayouts.Read(defaultLayouts.File);
            Assert.AreEqual(defaultLayouts.Serialize(DefaultLayouts), defaultLayouts.Serialize(defaultLayoutData));
        }

        [TestMethod]
        public void CopyCustomLayout_Hotkey()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(Session.Find<Element>(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the hotkey wasn't changed
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(AccessibilityId.HotkeyComboBox));
            Assert.IsNotNull(hotkeyComboBox);
            Assert.AreEqual("0", hotkeyComboBox.Text);
            Session.Find<Button>(ElementName.Cancel).Click();

            Session.Find<Element>(copiedLayoutName).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(AccessibilityId.HotkeyComboBox));
            Assert.IsNotNull(hotkeyComboBox);
            Assert.AreEqual("None", hotkeyComboBox.Text);
            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the hotkey file wasn't changed
            var hotkeys = new LayoutHotkeys();
            var hotkeyData = hotkeys.Read(hotkeys.File);
            Assert.AreEqual(hotkeys.Serialize(Hotkeys), hotkeys.Serialize(hotkeyData));
        }

        public void ClickCopyLayout()
        {
            if (Session.FindAll<Element>(By.AccessibilityId(AccessibilityId.CopyTemplate)).Count != 0)
            {
                Session.Find<Element>(By.AccessibilityId(AccessibilityId.CopyTemplate)).Click();
            }
            else
            {
                Session.Find<Element>(By.AccessibilityId(AccessibilityId.DuplicateLayoutButton)).Click();
            }
        }
    }
}
