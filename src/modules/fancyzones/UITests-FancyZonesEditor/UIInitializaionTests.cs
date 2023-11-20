// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.EditorParameters;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class UIInitializaionTests
    {
        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;
        private static IOTestHelper? _ioHelper;

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

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close(_context!);
            _ioHelper?.RestoreData();
        }

        [TestMethod]
        public void EditorParams_VerifySelectedMonitor()
        {
            EditorParameters editorParameters = new EditorParameters();
            _ioHelper = new IOTestHelper(editorParameters.File);
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
                        Dpi = 96,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = false,
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
                        IsSelected = true,
                    },
                },
            };
            _ioHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);

            Assert.IsFalse(_session.GetMonitorItem(1)?.Selected);
            Assert.IsTrue(_session.GetMonitorItem(2)?.Selected);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorScaling()
        {
            EditorParameters editorParameters = new EditorParameters();
            _ioHelper = new IOTestHelper(editorParameters.File);
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
            _ioHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var scaling = monitor.FindElementByAccessibilityId("ScalingText");
            Assert.AreEqual("200%", scaling.Text);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorResolution()
        {
            EditorParameters editorParameters = new EditorParameters();
            _ioHelper = new IOTestHelper(editorParameters.File);
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
            _ioHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var resolution = monitor.FindElementByAccessibilityId("ResolutionText");
            Assert.AreEqual("1920 × 1080", resolution.Text);
        }

        [TestMethod]
        public void EditorParams_SpanAcrossMonitors()
        {
            EditorParameters editorParameters = new EditorParameters();
            _ioHelper = new IOTestHelper(editorParameters.File);
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = true,
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
            _ioHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            Assert.IsNotNull(monitor);
            Assert.IsTrue(monitor.Selected);
        }
    }
}
