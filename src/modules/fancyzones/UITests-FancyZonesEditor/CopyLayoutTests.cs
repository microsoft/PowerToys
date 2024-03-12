// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.EditorParameters;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CopyLayoutTests
    {
        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayouts = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid],
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
                    MonitorConfiguration = MonitorConfigurationTypeEnumExtensions.MonitorConfigurationTypeToString(MonitorConfigurationType.Vertical),
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "custom",
                        Uuid = CustomLayouts.CustomLayouts[0].Uuid,
                    },
                },
            },
        };

        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _context = null;
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
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayouts));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.Empty],
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.Focus],
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.Rows],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.Columns],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.Grid],
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutTypes[Constants.TemplateLayouts.PriorityGrid],
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(DefaultLayouts));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(Hotkeys));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            _session = new FancyZonesEditorSession(_context!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close();
            FancyZonesEditorSession.Files.Restore();
        }

        [TestMethod]
        public void CopyTemplate_FromEditLayoutWindow()
        {
            string copiedLayoutName = Constants.TemplateLayoutNames[Constants.TemplateLayouts.Focus] + " (1)";
            _session?.ClickEditLayout(Constants.TemplateLayoutNames[Constants.TemplateLayouts.Focus]);
            _session?.ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyTemplate_FromContextMenu()
        {
            string copiedLayoutName = Constants.TemplateLayoutNames[Constants.TemplateLayouts.Rows] + " (1)";
            _session?.ClickContextMenuItem(Constants.TemplateLayoutNames[Constants.TemplateLayouts.Rows], "Create custom layout");

            // verify the layout is copied
            _session?.WaitElementDisplayedByName(copiedLayoutName);
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
        }

        [TestMethod]
        public void CopyTemplate_DefaultLayout()
        {
            string copiedLayoutName = Constants.TemplateLayoutNames[Constants.TemplateLayouts.PriorityGrid] + " (1)";
            _session?.ClickEditLayout(Constants.TemplateLayoutNames[Constants.TemplateLayouts.PriorityGrid]);
            _session?.ClickCopyLayout();

            // verify the layout is copied
            _session?.ClickSave();
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the default layout wasn't changed
            _session?.ClickEditLayout(Constants.TemplateLayoutNames[Constants.TemplateLayouts.PriorityGrid]);
            var horizontalDefaultButton = _session?.GetHorizontalDefaultButton(true);
            Assert.IsNotNull(horizontalDefaultButton);
            _session?.ClickCancel();

            _session?.ClickEditLayout(copiedLayoutName);
            horizontalDefaultButton = _session?.GetHorizontalDefaultButton(false);
            Assert.IsNotNull(horizontalDefaultButton);
            _session?.ClickCancel();

            // verify the default layouts file wasn't changed
            var defaultLayouts = new DefaultLayouts();
            var defaultLayoutData = defaultLayouts.Read(defaultLayouts.File);
            Assert.AreEqual(defaultLayouts.Serialize(DefaultLayouts), defaultLayouts.Serialize(defaultLayoutData));
        }

        [TestMethod]
        public void CopyCustomLayout_FromEditLayoutWindow()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            _session?.ClickEditLayout(CustomLayouts.CustomLayouts[0].Name);
            _session?.ClickCopyLayout();

            // verify the layout is copied
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

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
            _session?.ClickContextMenuItem(CustomLayouts.CustomLayouts[0].Name, "Duplicate");

            // verify the layout is copied
            _session?.WaitElementDisplayedByName(copiedLayoutName);
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

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
            _session?.ClickEditLayout(CustomLayouts.CustomLayouts[0].Name);
            _session?.ClickCopyLayout();

            // verify the layout is copied
            _session?.ClickSave();
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the default layout wasn't changed
            _session?.ClickEditLayout(CustomLayouts.CustomLayouts[0].Name);
            var horizontalDefaultButton = _session?.GetVerticalDefaultButton(true);
            Assert.IsNotNull(horizontalDefaultButton);
            _session?.ClickCancel();

            _session?.ClickEditLayout(copiedLayoutName);
            horizontalDefaultButton = _session?.GetVerticalDefaultButton(false);
            Assert.IsNotNull(horizontalDefaultButton);
            _session?.ClickCancel();

            // verify the default layouts file wasn't changed
            var defaultLayouts = new DefaultLayouts();
            var defaultLayoutData = defaultLayouts.Read(defaultLayouts.File);
            Assert.AreEqual(defaultLayouts.Serialize(DefaultLayouts), defaultLayouts.Serialize(defaultLayoutData));
        }

        [TestMethod]
        public void CopyCustomLayout_Hotkey()
        {
            string copiedLayoutName = CustomLayouts.CustomLayouts[0].Name + " (1)";
            _session?.ClickEditLayout(CustomLayouts.CustomLayouts[0].Name);
            _session?.ClickCopyLayout();

            // verify the layout is copied
            _session?.ClickSave();
            Assert.IsNotNull(_session?.GetLayout(copiedLayoutName)); // new name is presented

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count + 1, data.CustomLayouts.Count);

            // verify the hotkey wasn't changed
            _session?.ClickEditLayout(CustomLayouts.CustomLayouts[0].Name);
            var hotkeyComboBox = _session?.GetHotkeyComboBox();
            Assert.IsNotNull(hotkeyComboBox);
            Assert.AreEqual("0", hotkeyComboBox.Text);
            _session?.ClickCancel();

            _session?.ClickEditLayout(copiedLayoutName);
            hotkeyComboBox = _session?.GetHotkeyComboBox();
            Assert.IsNotNull(hotkeyComboBox);
            Assert.AreEqual("None", hotkeyComboBox.Text);
            _session?.ClickCancel();

            // verify the hotkey file wasn't changed
            var hotkeys = new LayoutHotkeys();
            var hotkeyData = hotkeys.Read(hotkeys.File);
            Assert.AreEqual(hotkeys.Serialize(Hotkeys), hotkeys.Serialize(hotkeyData));
        }
    }
}
