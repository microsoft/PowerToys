// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
    public class DefaultLayoutsTest : UITestBase
    {
        public DefaultLayoutsTest()
            : base(PowerToysModule.FancyZone)
        {
            FancyZonesEditorHelper.Files.ParamsIOHelper.RestoreData();
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
                        Dpi = 96,
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
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.TestClean();
        }

        [TestMethod]
        public void ClickMonitor()
        {
            Assert.IsNotNull(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1"));
            Assert.IsNotNull(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2"));

            // verify that the monitor 1 is selected initially
            Assert.IsTrue(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1").Selected);
            Assert.IsFalse(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Selected);

            Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Click();

            // verify that the monitor 2 is selected after click
            Assert.IsFalse(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1").Selected);
            Assert.IsTrue(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Selected);
        }
    }
}
