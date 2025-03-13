// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class FirstLunchTest : UITestBase
    {
        public FirstLunchTest()
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
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.DeleteFile();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.DeleteFile();
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.DeleteFile();
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.DeleteFile();

            // verify editor opens without errors
            this.RestartScopeExe();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void FirstLaunch()
        {
            Session.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.MainWindow)).Click();
            Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.MainWindow)));
        }
    }
}
