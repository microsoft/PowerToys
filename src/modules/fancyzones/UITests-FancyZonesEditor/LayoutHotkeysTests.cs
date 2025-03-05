// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutHotkeys;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class LayoutHotkeysTests : UITestBase
    {
        public LayoutHotkeysTests()
            : base(PowerToysModule.FancyZone)
        {
        }

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
                new CustomLayoutWrapper
                {
                    Uuid = "{1CDB1CC5-51B1-4E49-9C8C-B7A371CCB489}",
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    Type = CustomLayout.Canvas.TypeToString(),
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
                    LayoutId = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Key = 1,
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

            var layoutHotkeys = new LayoutHotkeys();
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(Hotkeys));

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
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

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
            this.TestClean();
        }

        [TestMethod]
        public void Initialize()
        {
            foreach (var layout in CustomLayouts.CustomLayouts)
            {
                Session.Find<Element>(layout.Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();

                var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
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

                var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
                Assert.IsNotNull(popup, "Hotkey combo box wasn't opened");

                try
                {
                    popup.Find<Element>(expected); // the current value should be available

                    // 0 and 1 are assigned, all others should be available
                    for (int i = 2; i < 10; i++)
                    {
                        popup.Find<Element>($"{i}");
                    }
                }
                catch
                {
                    Assert.Fail("Hotkey is missed");
                }

                Session.Find<Button>(ElementName.Cancel).DoubleClick();
            }
        }

        [TestMethod]
        public void Assign_Save()
        {
            var layout = CustomLayouts.CustomLayouts[4]; // a layout without assigned hotkey
            Session.Find<Element>(layout.Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();

            // assign hotkey
            const string key = "3";
            var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();

            var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);

            popup.Find<Element>($"{key}").Click(); // assign a free hotkey
            Assert.AreEqual(key, hotkeyComboBox.Text);

            // verify the file
            Session.Find<Button>(ElementName.Save).Click();
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsTrue(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = int.Parse(key, CultureInfo.InvariantCulture), LayoutId = layout.Uuid }));

            // verify the availability
            Session.Find<Element>(CustomLayouts.CustomLayouts[5].Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();

            popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            try
            {
                popup.Find<Element>($"{key}"); // verify the key is not available
                Assert.Fail($"{key} The assigned key is still available for other layouts.");
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
            Session.Find<Element>(layout.Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();

            // assign a hotkey
            const string key = "3";
            var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            popup.Find<Element>($"{key}").Click();
            Assert.AreEqual(key, hotkeyComboBox.Text);

            // verify the file
            Session.Find<Button>(ElementName.Cancel).Click();
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.AreEqual(Hotkeys.ToString(), actualData.ToString());

            // verify the availability
            Session.Find<Element>(CustomLayouts.CustomLayouts[5].Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            try
            {
                popup.Find<Element>($"{key}"); // verify the key is available
            }
            catch
            {
                Assert.Fail("The key is not available for other layouts.");
            }
        }

        [TestMethod]
        public void Assign_AllPossibleValues()
        {
            for (int i = 0; i < 10; i++)
            {
                string layoutName = $"Layout {i}";
                Session.Find<Element>(layoutName).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();

                var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
                Assert.IsNotNull(hotkeyComboBox);
                hotkeyComboBox.Click();
                var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
                Assert.IsNotNull(popup);
                popup.Find<Element>($"{i}").Click();

                Session.Find<Button>(ElementName.Save).Click();
            }

            // check there nothing except None
            {
                int layout = 10;
                string layoutName = $"Layout {layout}";
                Session.Find<Element>(layoutName).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
                var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
                Assert.IsNotNull(hotkeyComboBox);
                hotkeyComboBox.Click();
                var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
                Assert.IsNotNull(popup);

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        popup.Find<Element>($"{i}");
                        Assert.Fail("The assigned key is still available for other layouts.");
                    }
                    catch
                    {
                    }
                }

                popup.Find<Element>($"None").Click();
                Session.Find<Button>(ElementName.Save).Click();
            }
        }

        [TestMethod]
        public void Reset_Save()
        {
            var layout = CustomLayouts.CustomLayouts[0]; // a layout with assigned hotkey
            int assignedKey = Hotkeys.LayoutHotkeys.Find(x => x.LayoutId == layout.Uuid).Key;
            Session.Find<Element>(layout.Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            const string None = "None";

            // reset the hotkey
            var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            popup.Find<Element>(None).Click();
            Assert.AreEqual(None, hotkeyComboBox.Text);

            // verify the file
            Session.Find<Button>(ElementName.Save).Click();
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsFalse(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = assignedKey, LayoutId = layout.Uuid }));

            // verify the previously assigned key is available
            Session.Find<Element>(CustomLayouts.CustomLayouts[6].Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            try
            {
                popup.Find<Element>($"{assignedKey}"); // verify the key is available
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
            Session.Find<Element>(layout.Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            const string None = "None";

            // assign hotkey
            var hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            popup.Find<Element>(None).Click(); // reset the hotkey
            Assert.AreEqual(None, hotkeyComboBox.Text);

            // verify the file
            Session.Find<Button>(ElementName.Cancel).Click();
            var hotkeys = new LayoutHotkeys();
            var actualData = hotkeys.Read(hotkeys.File);
            Assert.IsTrue(actualData.LayoutHotkeys.Contains(new LayoutHotkeyWrapper { Key = assignedKey, LayoutId = layout.Uuid }));

            // verify the previously assigned key is not available
            Session.Find<Element>(CustomLayouts.CustomLayouts[6].Name).FindByAccessibilityId<Button>(AccessibilityId.EditLayoutButton).Click();
            hotkeyComboBox = Session.FindByAccessibilityId<Element>(AccessibilityId.HotkeyComboBox);
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            try
            {
                popup.Find<Element>($"{assignedKey}"); // verify the key is not available
                Assert.Fail("The key is still available for other layouts.");
            }
            catch
            {
                // the key is not available as expected
            }
        }
    }
}
