// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.DirectoryServices;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZones.UITests.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace UITests_FancyZones
{
    [TestClass]
    public class DragingWindowTests : UITestBase
    {
        private static readonly IOTestHelper AppZoneHistory = new FancyZonesEditorFiles().AppZoneHistoryIOHelper;

        public DragingWindowTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // set a custom layout with 2 subzones
            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = CustomLayoutsList;
            Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            // clear the app zone history
            AppZoneHistory.DeleteFile();

            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);
            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);

            int tries = 5;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            bool switchWindowEnable = TestContext.TestName == "TestSwitchShortCutDisable" ? false : true;
            this.RestartScopeExe();
        }

        /// <summary>
        /// Test  in the FancyZones
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Adding-entry Button works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesOnShiftDuringDrag()
        {
        }

        [TestMethod]
        public void TestShowZonesOnDragDuringShift()
        {
        }

        [TestMethod]
        public void TestShowZonesWithNonPrimaryMouseClick()
        {
        }

        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOff()
        {
        }

        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOn()
        {
        }

        [TestMethod]
        public void TestMakeDraggedWindowTransparentOn()
        {
        }

        [TestMethod]
        public void TestMakeDraggedWindowTransparentOff()
        {
        }

        private void Pull(int tries = 5, string direction = "up")
        {
            Key keyToSend = direction == "up" ? Key.Up : Key.Down;
            for (int i = 0; i < tries; i++)
            {
                SendKeys(keyToSend);
            }
        }

        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayoutsList = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{63F09977-D327-4DAC-98F4-0C886CAE9517}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom Column",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 1,
                        Columns = 2,
                        RowsPercentage = new List<int> { 10000 },
                        ColumnsPercentage = new List<int> { 5000, 5000 },
                        CellChildMap = new int[][] { [0, 1] },
                        SensitivityRadius = 20,
                        ShowSpacing = true,
                        Spacing = 10,
                    }),
                },
            },
        };
    }
}
