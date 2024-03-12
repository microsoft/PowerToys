// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CreateLayoutTests
    {
        private static FancyZonesEditorSession? _session;
        private static TestContext? _context;

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
            // prepare test editor parameters with 2 monitors before launching the editor
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
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

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
        public void CreateWithDefaultName()
        {
            string name = "Custom layout 1";
            _session?.ClickCreateNewLayout();
            _session?.ClickConfirm();
            _session?.ClickSave();

            // verify new layout presented
            Assert.IsNotNull(_session?.GetLayout(name));

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
        }

        [TestMethod]
        public void CreateWithCustomName()
        {
            string name = "Layout Name";
            _session?.ClickCreateNewLayout();
            var input = _session?.GetNameInput();
            input?.Clear();
            input?.SendKeys(name);
            _session?.ClickConfirm();
            _session?.ClickSave();

            // verify new layout presented
            Assert.IsNotNull(_session?.GetLayout(name));

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
        }

        [TestMethod]
        public void CreateGrid()
        {
            Constants.CustomLayoutType type = Constants.CustomLayoutType.Grid;
            _session?.ClickCreateNewLayout();
            _session?.SelectNewLayoutType(type);
            _session?.ClickConfirm();
            _session?.ClickSave();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == Constants.CustomLayoutTypeNames[type]));
        }

        [TestMethod]
        public void CreateCanvas()
        {
            Constants.CustomLayoutType type = Constants.CustomLayoutType.Canvas;
            _session?.ClickCreateNewLayout();
            _session?.SelectNewLayoutType(type);
            _session?.ClickConfirm();
            _session?.ClickSave();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == Constants.CustomLayoutTypeNames[type]));
        }

        [TestMethod]
        public void CancelGridCreation()
        {
            Constants.CustomLayoutType type = Constants.CustomLayoutType.Grid;
            _session?.ClickCreateNewLayout();
            _session?.SelectNewLayoutType(type);
            _session?.ClickConfirm();
            _session?.ClickCancel();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(0, data.CustomLayouts.Count);
        }

        [TestMethod]
        public void CancelCanvasCreation()
        {
            Constants.CustomLayoutType type = Constants.CustomLayoutType.Canvas;
            _session?.ClickCreateNewLayout();
            _session?.SelectNewLayoutType(type);
            _session?.ClickConfirm();
            _session?.ClickCancel();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(0, data.CustomLayouts.Count);
        }
    }
}
