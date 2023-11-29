// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutHotkeys;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class LayoutHotkeysTests
    {
        private static readonly CustomLayoutListWrapper CustomLayouts = new CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
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
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
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
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
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
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 3",
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
                    Uuid = "{1CDB1CC5-51B1-4E49-9C8C-B7A371CCB489}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 4",
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
                    Uuid = "{B1F600A5-9C2B-44C1-BF96-42D39E9DC004}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 5",
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
                    Uuid = "{DFBE08C3-7C34-482B-811F-C7DBFE368A96}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 6",
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
                    Uuid = "{4DB29206-24CE-421C-BFF4-35987D1A744B}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 7",
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
                    Uuid = "{51E1BBBA-1C6F-4E3C-85A2-4BFBAE154963}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 8",
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
                    Uuid = "{61F9E568-DB74-44FF-8AA8-4093E80D9BCF}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 9",
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
                    Uuid = "{8D328880-9E16-4CA8-B4A3-F6AE1C762CD5}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Layout 10",
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

        private static readonly LayoutHotkeysWrapper Hotkeys = new LayoutHotkeysWrapper
        {
            LayoutHotkeys = new List<LayoutHotkeyWrapper>
            {
                new LayoutHotkeyWrapper
                {
                    LayoutId = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Key = 0,
                },
                new LayoutHotkeyWrapper
                {
                    LayoutId = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}",
                    Key = 1,
                },
            },
        };

        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;
        private static IOTestHelper? _editorParamsIOHelper;
        private static IOTestHelper? _appliedLayoutsIOHelper;
        private static IOTestHelper? _customLayoutsIOHelper;
        private static IOTestHelper? _layoutHotkeysIOHelper;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;

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
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _appliedLayoutsIOHelper = new IOTestHelper(new AppliedLayouts().File);

            CustomLayouts customLayouts = new CustomLayouts();
            _customLayoutsIOHelper = new IOTestHelper(customLayouts.File);
            _customLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayouts));
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _editorParamsIOHelper?.RestoreData();
            _appliedLayoutsIOHelper?.RestoreData();
            _customLayoutsIOHelper?.RestoreData();
            _context = null;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            var layoutHotkeys = new LayoutHotkeys();
            _layoutHotkeysIOHelper = new IOTestHelper(layoutHotkeys.File);
            _layoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(Hotkeys));

            try
            {
                _session = new FancyZonesEditorSession(_context!);
            }
            catch (Exception ex)
            {
                _context?.WriteLine("Unable to start session. " + ex.Message);
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                _session?.Click_Cancel(); // in case if test has failed
            }
            catch
            {
            }

            _session?.Close();

            _layoutHotkeysIOHelper?.RestoreData();
        }

        [TestMethod]
        public void Initialize()
        {
            foreach (var layout in CustomLayouts.CustomLayouts)
            {
                _session?.Click_EditLayout(layout.Name);

                var hotkeyComboBox = _session?.GetHotkeyComboBox();
                Assert.IsNotNull(hotkeyComboBox);

                // verify the selected key
                string expected = "None";
                if (Hotkeys.LayoutHotkeys.Any(x => x.LayoutId == layout.Uuid))
                {
                    expected = $"{Hotkeys.LayoutHotkeys.Find(x => x.LayoutId == layout.Uuid).Key}";
                }

                Assert.AreEqual($"{expected}", hotkeyComboBox.Text);

                // verify the available values
                hotkeyComboBox.Click();
                var popup = _session?.GetHotkeyPopup();
                Assert.IsNotNull(popup, "Hotkey combo box wasn't opened");

                try
                {
                    popup.FindElementByName(expected); // the current value should be available

                    // 0 and 1 are assigned, all others should be available
                    for (int i = 2; i < 10; i++)
                    {
                        popup.FindElementByName($"{i}");
                    }
                }
                catch
                {
                    Assert.Fail("Hotkey is missed");
                }

                _session?.Click_Cancel();
                _session?.WaitUntilHidden(hotkeyComboBox!); // let the dialog window close
            }
        }

        [TestMethod]
        public void Assign_Save()
        {
            var layout = CustomLayouts.CustomLayouts[4]; // a layout without assigned hotkey
            _session?.Click_EditLayout(layout.Name);

            // assign hotkey
            const string key = "3";
            var hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            var popup = _session?.GetHotkeyPopup();
            _session?.Click(popup?.FindElementByName($"{key}")!); // assign a free hotkey
            Assert.AreEqual(key, hotkeyComboBox?.Text);

            // verify the file
            _session?.Click_Save();
            _session?.WaitUntilHidden(hotkeyComboBox!); // let the dialog window close
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsTrue(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = int.Parse(key, CultureInfo.InvariantCulture), LayoutId = layout.Uuid }));

            // verify the availability
            _session?.Click_EditLayout(CustomLayouts.CustomLayouts[5].Name);
            hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            popup = _session?.GetHotkeyPopup();
            try
            {
                popup?.FindElementByName($"{key}"); // verify the key is not available
                Assert.Fail(key, "The assigned key is still available for other layouts.");
            }
            catch
            {
                // key not found as expected
            }
        }

        [TestMethod]
        public void Assign_Cancel()
        {
            var layout = CustomLayouts.CustomLayouts[4]; // a layout without assigned hotkey
            _session?.Click_EditLayout(layout.Name);

            // assign a hotkey
            const string key = "3";
            var hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            var popup = _session?.GetHotkeyPopup();
            _session?.Click(popup?.FindElementByName($"{key}")!);
            Assert.AreEqual(key, hotkeyComboBox?.Text);

            // verify the file
            _session?.Click_Cancel();
            _session?.WaitUntilHidden(hotkeyComboBox!); // let the dialog window close
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.AreEqual(Hotkeys.ToString(), actualData.ToString());

            // verify the availability
            _session?.Click_EditLayout(CustomLayouts.CustomLayouts[5].Name);
            hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            popup = _session?.GetHotkeyPopup();
            try
            {
                popup?.FindElementByName($"{key}"); // verify the key is available
            }
            catch
            {
                Assert.Fail("The key is not available for other layouts.");
            }
        }

        [TestMethod]
        public void Reset_Save()
        {
            var layout = CustomLayouts.CustomLayouts[0]; // a layout with assigned hotkey
            int assignedKey = Hotkeys.LayoutHotkeys.Find(x => x.LayoutId == layout.Uuid).Key;
            _session?.Click_EditLayout(layout.Name);
            const string None = "None";

            // reset the hotkey
            var hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            var popup = _session?.GetHotkeyPopup();
            _session?.Click(popup?.FindElementByName(None)!);
            Assert.AreEqual(None, hotkeyComboBox?.Text);

            // verify the file
            _session?.Click_Save();
            _session?.WaitUntilHidden(hotkeyComboBox!); // let the dialog window close
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsFalse(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = assignedKey, LayoutId = layout.Uuid }));

            // verify the previously assigned key is available
            _session?.Click_EditLayout(CustomLayouts.CustomLayouts[6].Name);
            hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            popup = _session?.GetHotkeyPopup();
            try
            {
                popup?.FindElementByName($"{assignedKey}"); // verify the key is available
            }
            catch
            {
                Assert.Fail("The key is not available for other layouts.");
            }
        }

        [TestMethod]
        public void Reset_Cancel()
        {
            var layout = CustomLayouts.CustomLayouts[0]; // a layout with assigned hotkey
            int assignedKey = Hotkeys.LayoutHotkeys.Find(x => x.LayoutId == layout.Uuid).Key;
            _session?.Click_EditLayout(layout.Name);
            const string None = "None";

            // assign hotkey
            var hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            var popup = _session?.GetHotkeyPopup();
            _session?.Click(popup?.FindElementByName(None)!); // reset the hotkey
            Assert.AreEqual(None, hotkeyComboBox?.Text);

            // verify the file
            _session?.Click_Cancel();
            _session?.WaitUntilHidden(hotkeyComboBox!); // let the dialog window close
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsTrue(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = assignedKey, LayoutId = layout.Uuid }));

            // verify the previously assigned key is not available
            _session?.Click_EditLayout(CustomLayouts.CustomLayouts[6].Name);
            hotkeyComboBox = _session?.GetHotkeyComboBox();
            hotkeyComboBox?.Click();
            popup = _session?.GetHotkeyPopup();
            try
            {
                popup?.FindElementByName($"{assignedKey}"); // verify the key is not available
                Assert.Fail("The key is still available for other layouts.");
            }
            catch
            {
                // the key is not available as expected
            }
        }
    }
}
