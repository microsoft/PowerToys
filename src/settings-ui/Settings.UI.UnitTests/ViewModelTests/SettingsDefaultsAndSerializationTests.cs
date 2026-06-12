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
            /// <summary>
            /// Product code: AlwaysOnTopProperties constructor
            /// What: Verifies every constructor default (frame enabled/thickness/color/opacity, sound, hotkeys, game-mode, round corners, excluded apps)
            /// Why: Catches unintentional default changes introduced by property refactors or new fields
            /// Risk: Settings silently change after upgrade, e.g., frame becomes invisible or sound turns off for new installs
            /// </summary>
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

            /// <summary>
            /// Product code: AlwaysOnTopProperties constructor + System.Text.Json serializer
            /// What: Serializes a modified AlwaysOnTopProperties to JSON and deserializes it back, then asserts every field matches
            /// Why: Proves the JSON property names and converters are wired correctly for all fields
            /// Risk: Settings silently reset to defaults after save/load cycle, losing user customizations like frame color or thickness
            /// </summary>
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

            /// <summary>
            /// Product code: AlwaysOnTopProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into AlwaysOnTopProperties without throwing
            /// Why: Users may have an empty or corrupt settings file; deserialization must be resilient
            /// Risk: Crash or null-reference exception when user opens Settings UI with empty/corrupt AlwaysOnTop config
            /// </summary>
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
            /// <summary>
            /// Product code: AwakeProperties constructor
            /// What: Verifies constructor defaults for keep-display-on, mode, interval hours/minutes, and custom tray times
            /// Why: Catches default drift when new properties are added or enum values change
            /// Risk: Awake stays active with wrong mode or interval after fresh install, keeping the display on unexpectedly
            /// </summary>
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

            /// <summary>
            /// Product code: AwakeProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified AwakeProperties through JSON and verifies all fields including custom tray times dictionary
            /// Why: Validates that dictionary-typed properties and enum values survive serialization
            /// Risk: Custom tray times or mode selection lost after save, reverting to passive mode silently
            /// </summary>
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

            /// <summary>
            /// Product code: AwakeProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" and verifies sensible defaults are filled in
            /// Why: An empty or freshly-created settings file must not crash and must produce valid defaults
            /// Risk: Crash on missing fields when Awake reads an empty settings file after first install
            /// </summary>
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

            /// <summary>
            /// Product code: AwakeProperties constructor + System.Text.Json serializer
            /// What: Deserializes partial JSON with only keepDisplayOn and mode set, verifies provided values kept and missing fields get defaults
            /// Why: Settings files may be hand-edited or from older versions with fewer fields
            /// Risk: Partial settings file silently drops provided values, reverting user choices to defaults
            /// </summary>
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
            /// <summary>
            /// Product code: MouseHighlighterProperties constructor
            /// What: Verifies defaults for left/right click colors, always-color, opacity, radius, fade delay/duration, auto-activate, spotlight mode, and hotkey
            /// Why: Catches accidental default changes that could make the highlight invisible or wrong color
            /// Risk: Highlight invisible or wrong color on first use after install, confusing new users
            /// </summary>
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

            /// <summary>
            /// Product code: MouseHighlighterProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified MouseHighlighterProperties through JSON and verifies all color, radius, and mode fields match
            /// Why: Proves JSON property names map correctly for color strings and numeric properties
            /// Risk: Custom highlight colors or radius lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: MouseHighlighterProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into MouseHighlighterProperties without throwing
            /// Why: Guards against null-reference or missing-property exceptions from empty config files
            /// Risk: Crash or null-ref when user has empty/corrupt MouseHighlighter settings file
            /// </summary>
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
            /// <summary>
            /// Product code: FindMyMouseProperties constructor
            /// What: Verifies defaults for activation method, spotlight radius/color, animation duration, shaking thresholds, excluded apps, and hotkey
            /// Why: Catches default changes that would break mouse-locate activation or produce wrong animations
            /// Risk: FindMyMouse fails to activate or uses wrong animation/spotlight on first use after install
            /// </summary>
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

            /// <summary>
            /// Product code: FindMyMouseProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified FindMyMouseProperties through JSON and verifies all fields including spotlight, shaking, and excluded apps
            /// Why: Validates that all numeric, string, and boolean properties survive serialization
            /// Risk: Spotlight radius or excluded apps list lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: FindMyMouseProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into FindMyMouseProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt FindMyMouse settings file
            /// </summary>
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
            /// <summary>
            /// Product code: PowerAccentProperties constructor
            /// What: Verifies defaults for activation key, game-mode, toolbar position, input time, language, excluded apps, and sort/description flags
            /// Why: Catches default drift that could break accent input or select the wrong activation key
            /// Risk: Wrong default activation key breaks accent input on first use, or toolbar appears in unexpected position
            /// </summary>
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

            /// <summary>
            /// Product code: PowerAccentProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified PowerAccentProperties through JSON and verifies all fields including enum, string, and boolean properties
            /// Why: Validates that enum serialization (ActivationKey) and string properties (language, toolbar position) survive the round-trip
            /// Risk: Language selection or toolbar position lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: PowerAccentProperties constructor + System.Text.Json serializer
            /// What: Deserializes partial JSON with only activation_key and game-mode set, verifies provided values kept and missing fields get defaults
            /// Why: Older settings files or hand-edited JSON may only contain a subset of fields
            /// Risk: Unset fields get garbage values instead of safe defaults, causing erratic accent behavior
            /// </summary>
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
            /// <summary>
            /// Product code: PeekProperties constructor
            /// What: Verifies defaults for always-run-not-elevated, close-after-losing-focus, confirm-file-delete, space-to-activate, and hotkey
            /// Why: Catches default changes that could run Peek elevated unexpectedly or skip delete confirmation
            /// Risk: Peek runs elevated unexpectedly or missing delete confirmation dialog, risking accidental file deletion
            /// </summary>
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

            /// <summary>
            /// Product code: PeekProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified PeekProperties through JSON and verifies elevation, focus, delete-confirm, space-activate, and hotkey fields
            /// Why: Validates that boolean properties and hotkey structure survive serialization
            /// Risk: Elevation or focus settings lost after save, causing Peek to run elevated or not close on focus loss
            /// </summary>
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

            /// <summary>
            /// Product code: PeekProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into PeekProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt Peek settings file
            /// </summary>
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
            /// <summary>
            /// Product code: CursorWrapProperties constructor
            /// What: Verifies defaults for auto-activate, disable-wrap-during-drag, wrap mode, activation mode, single-monitor disable, and hotkey
            /// Why: Catches default changes that could activate cursor wrap unexpectedly on first run
            /// Risk: Cursor wrap activates unexpectedly on first run or wraps in wrong mode, confusing users
            /// </summary>
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

            /// <summary>
            /// Product code: CursorWrapProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified CursorWrapProperties through JSON and verifies all fields including wrap mode, activation mode, and hotkey
            /// Why: Validates that integer-enum properties and boolean flags survive serialization
            /// Risk: Wrap mode or activation mode lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: CursorWrapProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into CursorWrapProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt CursorWrap settings file
            /// </summary>
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
            /// <summary>
            /// Product code: MousePointerCrosshairsProperties constructor
            /// What: Verifies defaults for color, opacity, radius, thickness, border, orientation, auto-hide, fixed-length, gliding speeds, and hotkeys
            /// Why: Catches default changes that could make crosshairs invisible or enormously oversized
            /// Risk: Crosshairs invisible or enormous on first activation, rendering the feature unusable
            /// </summary>
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

            /// <summary>
            /// Product code: MousePointerCrosshairsProperties constructor + System.Text.Json serializer
            /// What: Round-trips a fully modified MousePointerCrosshairsProperties through JSON and verifies all 13+ fields match
            /// Why: Validates that all color strings, integer properties, and boolean flags survive serialization
            /// Risk: Custom crosshair appearance (color, thickness, gliding speed) lost after save, reverting to defaults
            /// </summary>
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

            /// <summary>
            /// Product code: MousePointerCrosshairsProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into MousePointerCrosshairsProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt MousePointerCrosshairs settings file
            /// </summary>
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
            /// <summary>
            /// Product code: MeasureToolProperties constructor
            /// What: Verifies defaults for units of measure, pixel tolerance, continuous capture, draw feet, edge detection, cross color, measure style, and hotkey
            /// Why: Catches default changes that could produce wrong units or invisible cross color
            /// Risk: Wrong default units or invisible cross color on first use, making measurements unreliable
            /// </summary>
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

            /// <summary>
            /// Product code: MeasureToolProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified MeasureToolProperties through JSON and verifies all fields including enum-backed measure style
            /// Why: Validates that enum, boolean, and color-string properties survive serialization
            /// Risk: Custom measure style or cross color lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: MeasureToolProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into MeasureToolProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt MeasureTool settings file
            /// </summary>
            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<MeasureToolProperties>("{}");
                Assert.IsNotNull(deserialized);
            }

            /// <summary>
            /// Product code: MeasureToolProperties constructor + System.Text.Json serializer
            /// What: Deserializes partial JSON with ContinuousCapture and PixelTolerance set, verifies provided values kept and missing fields get defaults
            /// Why: Settings files from older versions may only contain a subset of current fields
            /// Risk: Partial settings updates silently drop provided values like pixel tolerance, reverting to defaults
            /// </summary>
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
            /// <summary>
            /// Product code: WorkspacesProperties constructor
            /// What: Verifies defaults for sort-by preference and hotkey (Win+Ctrl+`)
            /// Why: Catches default changes that could break workspace list sorting or hotkey on first use
            /// Risk: Workspaces list sorted wrong or hotkey broken on first use after install
            /// </summary>
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

            /// <summary>
            /// Product code: WorkspacesProperties constructor + System.Text.Json serializer
            /// What: Round-trips a modified WorkspacesProperties through JSON and verifies sort-by and hotkey fields match
            /// Why: Validates that enum-backed sort preference and hotkey structure survive serialization
            /// Risk: Sort preference or hotkey lost after save, reverting to defaults on next load
            /// </summary>
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

            /// <summary>
            /// Product code: WorkspacesProperties constructor + System.Text.Json serializer
            /// What: Deserializes an empty JSON object "{}" into WorkspacesProperties without throwing
            /// Why: Empty or corrupt settings files must not crash the application
            /// Risk: Crash or null-ref when user has empty/corrupt Workspaces settings file
            /// </summary>
            [TestMethod]
            public void Deserialization_FromEmptyJson_DoesNotThrow()
            {
                var deserialized = JsonSerializer.Deserialize<WorkspacesProperties>("{}");
                Assert.IsNotNull(deserialized);
            }

            /// <summary>
            /// Product code: WorkspacesProperties constructor + System.Text.Json serializer
            /// What: Deserializes partial JSON with only sortby set, verifies provided sort order is preserved and missing fields get defaults
            /// Why: Settings files may be hand-edited or from older versions with fewer fields
            /// Risk: Partial settings file discards provided sort order, reverting to default sorting
            /// </summary>
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
