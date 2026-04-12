// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Settings.UI.Library.Enumerations;

namespace ViewModelTests
{
    [TestClass]
    public class SettingsDefaultsAndSerializationTests
    {
        [TestClass]
        public class AlwaysOnTopSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new AlwaysOnTopProperties();

                Assert.IsTrue(settings.FrameEnabled.Value);
                Assert.IsFalse(settings.ShowInSystemMenu.Value);
                Assert.AreEqual(15, settings.FrameThickness.Value);
                Assert.AreEqual("#0099cc", settings.FrameColor.Value);
                Assert.IsTrue(settings.FrameAccentColor.Value);
                Assert.AreEqual(100, settings.FrameOpacity.Value);
                Assert.IsTrue(settings.SoundEnabled.Value);
                Assert.IsTrue(settings.DoNotActivateOnGameMode.Value);
                Assert.IsTrue(settings.RoundCornersEnabled.Value);
                Assert.AreEqual(string.Empty, settings.ExcludedApps.Value);

                Assert.IsTrue(settings.Hotkey.Value.Win);
                Assert.IsTrue(settings.Hotkey.Value.Ctrl);
                Assert.IsFalse(settings.Hotkey.Value.Alt);
                Assert.IsFalse(settings.Hotkey.Value.Shift);
                Assert.AreEqual(0x54, settings.Hotkey.Value.Code);

                Assert.IsTrue(settings.IncreaseOpacityHotkey.Value.Win);
                Assert.IsTrue(settings.IncreaseOpacityHotkey.Value.Ctrl);
                Assert.AreEqual(0xBB, settings.IncreaseOpacityHotkey.Value.Code);

                Assert.IsTrue(settings.DecreaseOpacityHotkey.Value.Win);
                Assert.IsTrue(settings.DecreaseOpacityHotkey.Value.Ctrl);
                Assert.AreEqual(0xBD, settings.DecreaseOpacityHotkey.Value.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new AlwaysOnTopProperties();
                original.FrameThickness = new IntProperty(25);
                original.FrameColor = new StringProperty("#FF0000");
                original.FrameOpacity = new IntProperty(80);
                original.SoundEnabled = new BoolProperty(false);
                original.ExcludedApps = new StringProperty("notepad.exe");

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<AlwaysOnTopProperties>(json);

                Assert.AreEqual(original.FrameEnabled.Value, deserialized.FrameEnabled.Value);
                Assert.AreEqual(original.ShowInSystemMenu.Value, deserialized.ShowInSystemMenu.Value);
                Assert.AreEqual(original.FrameThickness.Value, deserialized.FrameThickness.Value);
                Assert.AreEqual(original.FrameColor.Value, deserialized.FrameColor.Value);
                Assert.AreEqual(original.FrameAccentColor.Value, deserialized.FrameAccentColor.Value);
                Assert.AreEqual(original.FrameOpacity.Value, deserialized.FrameOpacity.Value);
                Assert.AreEqual(original.SoundEnabled.Value, deserialized.SoundEnabled.Value);
                Assert.AreEqual(original.DoNotActivateOnGameMode.Value, deserialized.DoNotActivateOnGameMode.Value);
                Assert.AreEqual(original.RoundCornersEnabled.Value, deserialized.RoundCornersEnabled.Value);
                Assert.AreEqual(original.ExcludedApps.Value, deserialized.ExcludedApps.Value);
                Assert.AreEqual(original.Hotkey.Value.Win, deserialized.Hotkey.Value.Win);
                Assert.AreEqual(original.Hotkey.Value.Code, deserialized.Hotkey.Value.Code);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<AlwaysOnTopProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class AwakeSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new AwakeProperties();

                Assert.IsFalse(settings.KeepDisplayOn);
                Assert.AreEqual(AwakeMode.PASSIVE, settings.Mode);
                Assert.AreEqual(0u, settings.IntervalHours);
                Assert.AreEqual(1u, settings.IntervalMinutes);
                Assert.IsNotNull(settings.CustomTrayTimes);
                Assert.AreEqual(0, settings.CustomTrayTimes.Count);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new AwakeProperties();
                original.KeepDisplayOn = true;
                original.Mode = AwakeMode.TIMED;
                original.IntervalHours = 2;
                original.IntervalMinutes = 30;
                original.CustomTrayTimes = new Dictionary<string, uint> { { "quick", 15 } };

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<AwakeProperties>(json);

                Assert.AreEqual(original.KeepDisplayOn, deserialized.KeepDisplayOn);
                Assert.AreEqual(original.Mode, deserialized.Mode);
                Assert.AreEqual(original.IntervalHours, deserialized.IntervalHours);
                Assert.AreEqual(original.IntervalMinutes, deserialized.IntervalMinutes);
                Assert.AreEqual(1, deserialized.CustomTrayTimes.Count);
                Assert.AreEqual(15u, deserialized.CustomTrayTimes["quick"]);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_FillsDefaults()
            {
                var deserialized = JsonSerializer.Deserialize<AwakeProperties>("{}");

                Assert.IsNotNull(deserialized);
                Assert.IsFalse(deserialized.KeepDisplayOn);
                Assert.AreEqual(AwakeMode.PASSIVE, deserialized.Mode);
                Assert.AreEqual(0u, deserialized.IntervalHours);
                Assert.AreEqual(1u, deserialized.IntervalMinutes);
            }

            [TestMethod]
            public void Deserialization_FromPartialJson_PreservesProvidedValues()
            {
                string json = "{\"keepDisplayOn\":true,\"mode\":1}";
                var deserialized = JsonSerializer.Deserialize<AwakeProperties>(json);

                Assert.IsTrue(deserialized.KeepDisplayOn);
                Assert.AreEqual(AwakeMode.INDEFINITE, deserialized.Mode);
                Assert.AreEqual(0u, deserialized.IntervalHours);
            }
        }

        [TestClass]
        public class MouseHighlighterSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new MouseHighlighterProperties();

                Assert.AreEqual("#a6FFFF00", settings.LeftButtonClickColor.Value);
                Assert.AreEqual("#a60000FF", settings.RightButtonClickColor.Value);
                Assert.AreEqual("#00FF0000", settings.AlwaysColor.Value);
                Assert.AreEqual(166, settings.HighlightOpacity.Value);
                Assert.AreEqual(20, settings.HighlightRadius.Value);
                Assert.AreEqual(500, settings.HighlightFadeDelayMs.Value);
                Assert.AreEqual(250, settings.HighlightFadeDurationMs.Value);
                Assert.IsFalse(settings.AutoActivate.Value);
                Assert.IsFalse(settings.SpotlightMode.Value);

                Assert.IsTrue(settings.ActivationShortcut.Win);
                Assert.IsFalse(settings.ActivationShortcut.Ctrl);
                Assert.IsFalse(settings.ActivationShortcut.Alt);
                Assert.IsTrue(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x48, settings.ActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new MouseHighlighterProperties();
                original.LeftButtonClickColor = new StringProperty("#FF0000");
                original.HighlightRadius = new IntProperty(30);
                original.AutoActivate = new BoolProperty(true);

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<MouseHighlighterProperties>(json);

                Assert.AreEqual(original.LeftButtonClickColor.Value, deserialized.LeftButtonClickColor.Value);
                Assert.AreEqual(original.RightButtonClickColor.Value, deserialized.RightButtonClickColor.Value);
                Assert.AreEqual(original.AlwaysColor.Value, deserialized.AlwaysColor.Value);
                Assert.AreEqual(original.HighlightOpacity.Value, deserialized.HighlightOpacity.Value);
                Assert.AreEqual(original.HighlightRadius.Value, deserialized.HighlightRadius.Value);
                Assert.AreEqual(original.HighlightFadeDelayMs.Value, deserialized.HighlightFadeDelayMs.Value);
                Assert.AreEqual(original.HighlightFadeDurationMs.Value, deserialized.HighlightFadeDurationMs.Value);
                Assert.AreEqual(original.AutoActivate.Value, deserialized.AutoActivate.Value);
                Assert.AreEqual(original.SpotlightMode.Value, deserialized.SpotlightMode.Value);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<MouseHighlighterProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class FindMyMouseSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new FindMyMouseProperties();

                Assert.AreEqual(0, settings.ActivationMethod.Value);
                Assert.IsFalse(settings.IncludeWinKey.Value);
                Assert.IsTrue(settings.DoNotActivateOnGameMode.Value);
                Assert.AreEqual("#80000000", settings.BackgroundColor.Value);
                Assert.AreEqual("#80FFFFFF", settings.SpotlightColor.Value);
                Assert.AreEqual(100, settings.SpotlightRadius.Value);
                Assert.AreEqual(500, settings.AnimationDurationMs.Value);
                Assert.AreEqual(9, settings.SpotlightInitialZoom.Value);
                Assert.AreEqual(string.Empty, settings.ExcludedApps.Value);
                Assert.AreEqual(1000, settings.ShakingMinimumDistance.Value);
                Assert.AreEqual(1000, settings.ShakingIntervalMs.Value);
                Assert.AreEqual(400, settings.ShakingFactor.Value);

                Assert.IsTrue(settings.ActivationShortcut.Win);
                Assert.IsFalse(settings.ActivationShortcut.Ctrl);
                Assert.IsFalse(settings.ActivationShortcut.Alt);
                Assert.IsTrue(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x46, settings.ActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new FindMyMouseProperties();
                original.SpotlightRadius = new IntProperty(200);
                original.BackgroundColor = new StringProperty("#FF000000");
                original.ShakingFactor = new IntProperty(500);
                original.ExcludedApps = new StringProperty("explorer.exe");

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<FindMyMouseProperties>(json);

                Assert.AreEqual(original.ActivationMethod.Value, deserialized.ActivationMethod.Value);
                Assert.AreEqual(original.IncludeWinKey.Value, deserialized.IncludeWinKey.Value);
                Assert.AreEqual(original.DoNotActivateOnGameMode.Value, deserialized.DoNotActivateOnGameMode.Value);
                Assert.AreEqual(original.BackgroundColor.Value, deserialized.BackgroundColor.Value);
                Assert.AreEqual(original.SpotlightColor.Value, deserialized.SpotlightColor.Value);
                Assert.AreEqual(original.SpotlightRadius.Value, deserialized.SpotlightRadius.Value);
                Assert.AreEqual(original.AnimationDurationMs.Value, deserialized.AnimationDurationMs.Value);
                Assert.AreEqual(original.SpotlightInitialZoom.Value, deserialized.SpotlightInitialZoom.Value);
                Assert.AreEqual(original.ExcludedApps.Value, deserialized.ExcludedApps.Value);
                Assert.AreEqual(original.ShakingMinimumDistance.Value, deserialized.ShakingMinimumDistance.Value);
                Assert.AreEqual(original.ShakingIntervalMs.Value, deserialized.ShakingIntervalMs.Value);
                Assert.AreEqual(original.ShakingFactor.Value, deserialized.ShakingFactor.Value);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<FindMyMouseProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class PowerAccentSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new PowerAccentProperties();

                Assert.AreEqual(PowerAccentActivationKey.Both, settings.ActivationKey);
                Assert.IsTrue(settings.DoNotActivateOnGameMode);
                Assert.AreEqual("Top center", settings.ToolbarPosition.Value);
                Assert.AreEqual(300, settings.InputTime.Value);
                Assert.AreEqual("ALL", settings.SelectedLang.Value);
                Assert.AreEqual(string.Empty, settings.ExcludedApps.Value);
                Assert.IsFalse(settings.ShowUnicodeDescription);
                Assert.IsFalse(settings.SortByUsageFrequency);
                Assert.IsFalse(settings.StartSelectionFromTheLeft);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new PowerAccentProperties();
                original.ActivationKey = PowerAccentActivationKey.Space;
                original.ToolbarPosition = new StringProperty("Bottom center");
                original.InputTime = new IntProperty(500);
                original.SelectedLang = new StringProperty("FR");
                original.ShowUnicodeDescription = true;
                original.SortByUsageFrequency = true;

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<PowerAccentProperties>(json);

                Assert.AreEqual(original.ActivationKey, deserialized.ActivationKey);
                Assert.AreEqual(original.DoNotActivateOnGameMode, deserialized.DoNotActivateOnGameMode);
                Assert.AreEqual(original.ToolbarPosition.Value, deserialized.ToolbarPosition.Value);
                Assert.AreEqual(original.InputTime.Value, deserialized.InputTime.Value);
                Assert.AreEqual(original.SelectedLang.Value, deserialized.SelectedLang.Value);
                Assert.AreEqual(original.ExcludedApps.Value, deserialized.ExcludedApps.Value);
                Assert.AreEqual(original.ShowUnicodeDescription, deserialized.ShowUnicodeDescription);
                Assert.AreEqual(original.SortByUsageFrequency, deserialized.SortByUsageFrequency);
                Assert.AreEqual(original.StartSelectionFromTheLeft, deserialized.StartSelectionFromTheLeft);
            }

            [TestMethod]
            public void Deserialization_FromPartialJson_FillsDefaults()
            {
                string json = "{\"activation_key\":1,\"do_not_activate_on_game_mode\":false}";
                var deserialized = JsonSerializer.Deserialize<PowerAccentProperties>(json);

                Assert.AreEqual(PowerAccentActivationKey.Space, deserialized.ActivationKey);
                Assert.IsFalse(deserialized.DoNotActivateOnGameMode);
                Assert.IsFalse(deserialized.ShowUnicodeDescription);
                Assert.IsFalse(deserialized.SortByUsageFrequency);
            }
        }

        [TestClass]
        public class PeekSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new PeekProperties();

                Assert.IsTrue(settings.AlwaysRunNotElevated.Value);
                Assert.IsFalse(settings.CloseAfterLosingFocus.Value);
                Assert.IsTrue(settings.ConfirmFileDelete.Value);
                Assert.IsTrue(settings.EnableSpaceToActivate.Value);

                Assert.IsFalse(settings.ActivationShortcut.Win);
                Assert.IsTrue(settings.ActivationShortcut.Ctrl);
                Assert.IsFalse(settings.ActivationShortcut.Alt);
                Assert.IsFalse(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x20, settings.ActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new PeekProperties();
                original.AlwaysRunNotElevated = new BoolProperty(false);
                original.CloseAfterLosingFocus = new BoolProperty(true);
                original.ConfirmFileDelete = new BoolProperty(false);
                original.EnableSpaceToActivate = new BoolProperty(false);

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<PeekProperties>(json);

                Assert.AreEqual(original.AlwaysRunNotElevated.Value, deserialized.AlwaysRunNotElevated.Value);
                Assert.AreEqual(original.CloseAfterLosingFocus.Value, deserialized.CloseAfterLosingFocus.Value);
                Assert.AreEqual(original.ConfirmFileDelete.Value, deserialized.ConfirmFileDelete.Value);
                Assert.AreEqual(original.EnableSpaceToActivate.Value, deserialized.EnableSpaceToActivate.Value);
                Assert.AreEqual(original.ActivationShortcut.Code, deserialized.ActivationShortcut.Code);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<PeekProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class CursorWrapSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new CursorWrapProperties();

                Assert.IsFalse(settings.AutoActivate.Value);
                Assert.IsTrue(settings.DisableWrapDuringDrag.Value);
                Assert.AreEqual(0, settings.WrapMode.Value);
                Assert.AreEqual(0, settings.ActivationMode.Value);
                Assert.IsFalse(settings.DisableCursorWrapOnSingleMonitor.Value);

                Assert.IsTrue(settings.ActivationShortcut.Win);
                Assert.IsFalse(settings.ActivationShortcut.Ctrl);
                Assert.IsTrue(settings.ActivationShortcut.Alt);
                Assert.IsFalse(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x55, settings.ActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new CursorWrapProperties();
                original.AutoActivate = new BoolProperty(true);
                original.DisableWrapDuringDrag = new BoolProperty(false);
                original.WrapMode = new IntProperty(1);
                original.ActivationMode = new IntProperty(2);
                original.DisableCursorWrapOnSingleMonitor = new BoolProperty(true);

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<CursorWrapProperties>(json);

                Assert.AreEqual(original.AutoActivate.Value, deserialized.AutoActivate.Value);
                Assert.AreEqual(original.DisableWrapDuringDrag.Value, deserialized.DisableWrapDuringDrag.Value);
                Assert.AreEqual(original.WrapMode.Value, deserialized.WrapMode.Value);
                Assert.AreEqual(original.ActivationMode.Value, deserialized.ActivationMode.Value);
                Assert.AreEqual(original.DisableCursorWrapOnSingleMonitor.Value, deserialized.DisableCursorWrapOnSingleMonitor.Value);
                Assert.AreEqual(original.ActivationShortcut.Code, deserialized.ActivationShortcut.Code);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<CursorWrapProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class MousePointerCrosshairsSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new MousePointerCrosshairsProperties();

                Assert.AreEqual("#FF0000", settings.CrosshairsColor.Value);
                Assert.AreEqual(75, settings.CrosshairsOpacity.Value);
                Assert.AreEqual(20, settings.CrosshairsRadius.Value);
                Assert.AreEqual(5, settings.CrosshairsThickness.Value);
                Assert.AreEqual("#FFFFFF", settings.CrosshairsBorderColor.Value);
                Assert.AreEqual(1, settings.CrosshairsBorderSize.Value);
                Assert.AreEqual(0, settings.CrosshairsOrientation.Value);
                Assert.IsFalse(settings.CrosshairsAutoHide.Value);
                Assert.IsFalse(settings.CrosshairsIsFixedLengthEnabled.Value);
                Assert.AreEqual(1, settings.CrosshairsFixedLength.Value);
                Assert.IsFalse(settings.AutoActivate.Value);
                Assert.AreEqual(25, settings.GlidingTravelSpeed.Value);
                Assert.AreEqual(5, settings.GlidingDelaySpeed.Value);

                Assert.IsTrue(settings.ActivationShortcut.Win);
                Assert.IsFalse(settings.ActivationShortcut.Ctrl);
                Assert.IsTrue(settings.ActivationShortcut.Alt);
                Assert.IsFalse(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x50, settings.ActivationShortcut.Code);

                Assert.IsTrue(settings.GlidingCursorActivationShortcut.Win);
                Assert.IsTrue(settings.GlidingCursorActivationShortcut.Alt);
                Assert.AreEqual(0xBE, settings.GlidingCursorActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new MousePointerCrosshairsProperties();
                original.CrosshairsColor = new StringProperty("#00FF00");
                original.CrosshairsOpacity = new IntProperty(50);
                original.CrosshairsRadius = new IntProperty(30);
                original.CrosshairsThickness = new IntProperty(10);
                original.CrosshairsBorderColor = new StringProperty("#000000");
                original.CrosshairsBorderSize = new IntProperty(2);
                original.CrosshairsOrientation = new IntProperty(1);
                original.CrosshairsAutoHide = new BoolProperty(true);
                original.CrosshairsIsFixedLengthEnabled = new BoolProperty(true);
                original.CrosshairsFixedLength = new IntProperty(500);
                original.AutoActivate = new BoolProperty(true);
                original.GlidingTravelSpeed = new IntProperty(50);
                original.GlidingDelaySpeed = new IntProperty(10);

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<MousePointerCrosshairsProperties>(json);

                Assert.AreEqual(original.CrosshairsColor.Value, deserialized.CrosshairsColor.Value);
                Assert.AreEqual(original.CrosshairsOpacity.Value, deserialized.CrosshairsOpacity.Value);
                Assert.AreEqual(original.CrosshairsRadius.Value, deserialized.CrosshairsRadius.Value);
                Assert.AreEqual(original.CrosshairsThickness.Value, deserialized.CrosshairsThickness.Value);
                Assert.AreEqual(original.CrosshairsBorderColor.Value, deserialized.CrosshairsBorderColor.Value);
                Assert.AreEqual(original.CrosshairsBorderSize.Value, deserialized.CrosshairsBorderSize.Value);
                Assert.AreEqual(original.CrosshairsOrientation.Value, deserialized.CrosshairsOrientation.Value);
                Assert.AreEqual(original.CrosshairsAutoHide.Value, deserialized.CrosshairsAutoHide.Value);
                Assert.AreEqual(original.CrosshairsIsFixedLengthEnabled.Value, deserialized.CrosshairsIsFixedLengthEnabled.Value);
                Assert.AreEqual(original.CrosshairsFixedLength.Value, deserialized.CrosshairsFixedLength.Value);
                Assert.AreEqual(original.AutoActivate.Value, deserialized.AutoActivate.Value);
                Assert.AreEqual(original.GlidingTravelSpeed.Value, deserialized.GlidingTravelSpeed.Value);
                Assert.AreEqual(original.GlidingDelaySpeed.Value, deserialized.GlidingDelaySpeed.Value);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<MousePointerCrosshairsProperties>("{}");
                Assert.IsNotNull(deserialized);
            }
        }

        [TestClass]
        public class MeasureToolSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new MeasureToolProperties();

                Assert.AreEqual(0, settings.UnitsOfMeasure.Value);
                Assert.AreEqual(30, settings.PixelTolerance.Value);
                Assert.IsFalse(settings.ContinuousCapture);
                Assert.IsTrue(settings.DrawFeetOnCross);
                Assert.IsFalse(settings.PerColorChannelEdgeDetection);
                Assert.AreEqual("#FF4500", settings.MeasureCrossColor.Value);
                Assert.AreEqual((int)MeasureToolMeasureStyle.None, settings.DefaultMeasureStyle.Value);

                Assert.IsTrue(settings.ActivationShortcut.Win);
                Assert.IsTrue(settings.ActivationShortcut.Ctrl);
                Assert.IsFalse(settings.ActivationShortcut.Alt);
                Assert.IsTrue(settings.ActivationShortcut.Shift);
                Assert.AreEqual(0x4D, settings.ActivationShortcut.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new MeasureToolProperties();
                original.UnitsOfMeasure = new IntProperty(1);
                original.PixelTolerance = new IntProperty(50);
                original.ContinuousCapture = true;
                original.DrawFeetOnCross = false;
                original.PerColorChannelEdgeDetection = true;
                original.MeasureCrossColor = new StringProperty("#00FF00");
                original.DefaultMeasureStyle = new IntProperty((int)MeasureToolMeasureStyle.Bounds);

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<MeasureToolProperties>(json);

                Assert.AreEqual(original.UnitsOfMeasure.Value, deserialized.UnitsOfMeasure.Value);
                Assert.AreEqual(original.PixelTolerance.Value, deserialized.PixelTolerance.Value);
                Assert.AreEqual(original.ContinuousCapture, deserialized.ContinuousCapture);
                Assert.AreEqual(original.DrawFeetOnCross, deserialized.DrawFeetOnCross);
                Assert.AreEqual(original.PerColorChannelEdgeDetection, deserialized.PerColorChannelEdgeDetection);
                Assert.AreEqual(original.MeasureCrossColor.Value, deserialized.MeasureCrossColor.Value);
                Assert.AreEqual(original.DefaultMeasureStyle.Value, deserialized.DefaultMeasureStyle.Value);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<MeasureToolProperties>("{}");
                Assert.IsNotNull(deserialized);
            }

            [TestMethod]
            public void Deserialization_FromPartialJson_PreservesProvidedValues()
            {
                string json = "{\"ContinuousCapture\":{\"value\":true},\"PixelTolerance\":{\"value\":60}}";
                var deserialized = JsonSerializer.Deserialize<MeasureToolProperties>(json);

                Assert.IsTrue(deserialized.ContinuousCapture);
                Assert.AreEqual(60, deserialized.PixelTolerance.Value);
            }
        }

        [TestClass]
        public class WorkspacesSettingsTests
        {
            [TestMethod]
            public void DefaultValues_AreCorrect()
            {
                var settings = new WorkspacesProperties();

                Assert.AreEqual(WorkspacesProperties.SortByProperty.LastLaunched, settings.SortBy);

                Assert.IsTrue(settings.Hotkey.Value.Win);
                Assert.IsTrue(settings.Hotkey.Value.Ctrl);
                Assert.IsFalse(settings.Hotkey.Value.Alt);
                Assert.IsFalse(settings.Hotkey.Value.Shift);
                Assert.AreEqual(0xC0, settings.Hotkey.Value.Code);
            }

            [TestMethod]
            public void JsonRoundTrip_PreservesAllFields()
            {
                var original = new WorkspacesProperties();
                original.SortBy = WorkspacesProperties.SortByProperty.Name;

                var json = JsonSerializer.Serialize(original);
                var deserialized = JsonSerializer.Deserialize<WorkspacesProperties>(json);

                Assert.AreEqual(original.SortBy, deserialized.SortBy);
                Assert.AreEqual(original.Hotkey.Value.Win, deserialized.Hotkey.Value.Win);
                Assert.AreEqual(original.Hotkey.Value.Ctrl, deserialized.Hotkey.Value.Ctrl);
                Assert.AreEqual(original.Hotkey.Value.Code, deserialized.Hotkey.Value.Code);
            }

            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<WorkspacesProperties>("{}");
                Assert.IsNotNull(deserialized);
            }

            [TestMethod]
            public void Deserialization_FromPartialJson_PreservesProvidedValues()
            {
                string json = "{\"sortby\":2}";
                var deserialized = JsonSerializer.Deserialize<WorkspacesProperties>(json);

                Assert.AreEqual(WorkspacesProperties.SortByProperty.Name, deserialized.SortBy);
            }
        }
    }
}
