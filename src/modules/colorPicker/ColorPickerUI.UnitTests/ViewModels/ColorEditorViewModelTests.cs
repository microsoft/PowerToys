// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using ColorPicker.Common;
using ColorPicker.Helpers;
using ColorPicker.Models;
using ColorPicker.Settings;
using ColorPicker.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace ColorPicker.UnitTests.ViewModels
{
    [TestClass]
    public class ColorEditorViewModelTests
    {
        // Minimal in-test stub – no mocking framework.
        private sealed class StubUserSettings : IUserSettings
        {
            public SettingItem<string> ActivationShortcut { get; } = new SettingItem<string>(string.Empty);

            public SettingItem<bool> ChangeCursor { get; } = new SettingItem<bool>(false);

            public SettingItem<string> CopiedColorRepresentation { get; set; } = new SettingItem<string>(string.Empty);

            public SettingItem<string> CopiedColorRepresentationFormat { get; set; } = new SettingItem<string>(string.Empty);

            public SettingItem<ColorPickerActivationAction> ActivationAction { get; } =
                new SettingItem<ColorPickerActivationAction>(ColorPickerActivationAction.OpenEditor);

            public SettingItem<ColorPickerClickAction> PrimaryClickAction { get; } =
                new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorThenEditor);

            public SettingItem<ColorPickerClickAction> MiddleClickAction { get; } =
                new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorAndClose);

            public SettingItem<ColorPickerClickAction> SecondaryClickAction { get; } =
                new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.Close);

            public RangeObservableCollection<string> ColorHistory { get; } = new RangeObservableCollection<string>();

            public SettingItem<int> ColorHistoryLimit { get; } = new SettingItem<int>(20);

            public ObservableCollection<System.Collections.Generic.KeyValuePair<string, string>> VisibleColorFormats { get; } =
                new ObservableCollection<System.Collections.Generic.KeyValuePair<string, string>>();

            public SettingItem<bool> ShowColorName { get; } = new SettingItem<bool>(false);

            public void SendSettingsTelemetry()
            {
            }
        }

        private static readonly Color AnyColor = new Color { A = 255, R = 100, G = 150, B = 200 };

        private ColorEditorViewModel? _vm;

        [TestInitialize]
        public void Init()
        {
            _vm = new ColorEditorViewModel(new StubUserSettings());

            // Ensure SessionEventHelper.Event is non-null so telemetry flag assertions don't NullRef.
            SessionEventHelper.Start(ColorPickerActivationAction.OpenEditor);
        }

        // ─── RemoveColorsCommand – null input ────────────────────────
        [TestMethod]
        [Description("Execute(null) must not throw – CanExecute guard or defensive return prevents it.")]
        public void RemoveColorsCommand_Execute_NullInput_DoesNotThrow()
        {
            _vm!.RemoveColorsCommand.Execute(null);
        }

        [TestMethod]
        [Description("Execute(null) must not change ColorsHistory.")]
        public void RemoveColorsCommand_Execute_NullInput_DoesNotMutateColorsHistory()
        {
            int before = _vm!.ColorsHistory.Count;
            _vm.RemoveColorsCommand.Execute(null);
            Assert.AreEqual(before, _vm.ColorsHistory.Count);
        }

        [TestMethod]
        [Description("Execute(null) must not set the EditorHistoryColorRemoved telemetry flag.")]
        public void RemoveColorsCommand_Execute_NullInput_DoesNotSetTelemetryFlag()
        {
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;
            _vm!.RemoveColorsCommand.Execute(null);
            Assert.IsFalse(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        [TestMethod]
        [Description("CanExecute(null) must return false – no valid selection.")]
        public void RemoveColorsCommand_CanExecute_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.RemoveColorsCommand.CanExecute(null));
        }

        // ─── RemoveColorsCommand – empty IList input ─────────────────
        [TestMethod]
        [Description("Execute(empty IList) must not throw.")]
        public void RemoveColorsCommand_Execute_EmptyList_DoesNotThrow()
        {
            _vm!.RemoveColorsCommand.Execute(new List<Color>());
        }

        [TestMethod]
        [Description("Execute(empty IList) must not change SelectedColorIndex.")]
        public void RemoveColorsCommand_Execute_EmptyList_DoesNotMutateSelectedColorIndex()
        {
            int before = _vm!.SelectedColorIndex;
            _vm.RemoveColorsCommand.Execute(new List<Color>());
            Assert.AreEqual(before, _vm.SelectedColorIndex);
        }

        [TestMethod]
        [Description("Execute(empty IList) must not set the EditorHistoryColorRemoved telemetry flag.")]
        public void RemoveColorsCommand_Execute_EmptyList_DoesNotSetTelemetryFlag()
        {
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;
            _vm!.RemoveColorsCommand.Execute(new List<Color>());
            Assert.IsFalse(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        [TestMethod]
        [Description("CanExecute(empty IList) must return false.")]
        public void RemoveColorsCommand_CanExecute_EmptyList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.RemoveColorsCommand.CanExecute(new List<Color>()));
        }

        // ─── RemoveColorsCommand – non-empty IList input ─────────────
        [TestMethod]
        [Description("CanExecute(non-empty IList) must return true.")]
        public void RemoveColorsCommand_CanExecute_NonEmptyList_ReturnsTrue()
        {
            var list = new List<Color> { AnyColor };
            Assert.IsTrue(_vm!.RemoveColorsCommand.CanExecute(list));
        }

        [TestMethod]
        public void RemoveColorsCommand_Execute_MiddleColor_RemovesAndSelectsNext()
        {
            var first = new Color { A = 255, R = 10, G = 20, B = 30 };
            var middle = new Color { A = 255, R = 40, G = 50, B = 60 };
            var last = new Color { A = 255, R = 70, G = 80, B = 90 };
            _vm!.ColorsHistory.Add(first);
            _vm.ColorsHistory.Add(middle);
            _vm.ColorsHistory.Add(last);
            _vm.SelectedColorIndex = 1;
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;

            _vm.RemoveColorsCommand.Execute(new List<Color> { middle });

            CollectionAssert.AreEqual(new[] { first, last }, _vm.ColorsHistory.ToArray());
            Assert.AreEqual(1, _vm.SelectedColorIndex);
            Assert.AreEqual(last, _vm.SelectedColor);
            Assert.IsTrue(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        [TestMethod]
        public void RemoveColorsCommand_Execute_AllColors_ClearsSelection()
        {
            var first = new Color { A = 255, R = 10, G = 20, B = 30 };
            var second = new Color { A = 255, R = 40, G = 50, B = 60 };
            _vm!.ColorsHistory.Add(first);
            _vm.ColorsHistory.Add(second);
            _vm.SelectedColorIndex = 1;

            _vm.RemoveColorsCommand.Execute(new List<Color> { first, second });

            Assert.AreEqual(0, _vm.ColorsHistory.Count);
            Assert.AreEqual(-1, _vm.SelectedColorIndex);
        }

        // ─── ExportColorsGroupedByColorCommand – CanExecute guard ────
        [TestMethod]
        [Description("CanExecute(null) on ExportByColor must return false.")]
        public void ExportColorsGroupedByColorCommand_CanExecute_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByColorCommand.CanExecute(null));
        }

        [TestMethod]
        [Description("CanExecute(empty IList) on ExportByColor must return false.")]
        public void ExportColorsGroupedByColorCommand_CanExecute_EmptyList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByColorCommand.CanExecute(new List<Color>()));
        }

        [TestMethod]
        [Description("CanExecute(non-empty IList) on ExportByColor must return true.")]
        public void ExportColorsGroupedByColorCommand_CanExecute_NonEmptyList_ReturnsTrue()
        {
            Assert.IsTrue(_vm!.ExportColorsGroupedByColorCommand.CanExecute(new List<Color> { AnyColor }));
        }

        // ─── ExportColorsGroupedByFormatCommand – CanExecute guard ───
        [TestMethod]
        [Description("CanExecute(null) on ExportByFormat must return false.")]
        public void ExportColorsGroupedByFormatCommand_CanExecute_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByFormatCommand.CanExecute(null));
        }

        [TestMethod]
        [Description("CanExecute(empty IList) on ExportByFormat must return false.")]
        public void ExportColorsGroupedByFormatCommand_CanExecute_EmptyList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByFormatCommand.CanExecute(new List<Color>()));
        }

        [TestMethod]
        [Description("CanExecute(non-empty IList) on ExportByFormat must return true.")]
        public void ExportColorsGroupedByFormatCommand_CanExecute_NonEmptyList_ReturnsTrue()
        {
            Assert.IsTrue(_vm!.ExportColorsGroupedByFormatCommand.CanExecute(new List<Color> { AnyColor }));
        }

        // ─── RemoveColorsCommand – wrong-element-type IList input ──────
        [TestMethod]
        [Description("CanExecute(non-empty List<string>) must return false – wrong element type.")]
        public void RemoveColorsCommand_CanExecute_NonEmptyStringList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.RemoveColorsCommand.CanExecute(new List<string> { "color" }));
        }

        [TestMethod]
        public void SelectedColorChangedCommand_Execute_ExistingColorMovesToFrontWithoutDuplicate()
        {
            var first = new Color { A = 255, R = 10, G = 20, B = 30 };
            var selected = new Color { A = 255, R = 40, G = 50, B = 60 };
            var last = new Color { A = 255, R = 70, G = 80, B = 90 };
            _vm!.ColorsHistory.Add(first);
            _vm.ColorsHistory.Add(selected);
            _vm.ColorsHistory.Add(last);

            _vm.SelectedColorChangedCommand.Execute(selected);

            CollectionAssert.AreEqual(new[] { selected, first, last }, _vm.ColorsHistory.ToArray());
            Assert.AreEqual(0, _vm.SelectedColorIndex);
        }

        [TestMethod]
        [Description("Execute(non-empty List<string>) must not throw and must not mutate ColorsHistory.")]
        public void RemoveColorsCommand_Execute_NonEmptyStringList_DoesNotMutateColorsHistory()
        {
            int before = _vm!.ColorsHistory.Count;
            _vm.RemoveColorsCommand.Execute(new List<string> { "color" });
            Assert.AreEqual(before, _vm.ColorsHistory.Count);
        }

        [TestMethod]
        [Description("Execute(non-empty List<string>) must not set the EditorHistoryColorRemoved telemetry flag.")]
        public void RemoveColorsCommand_Execute_NonEmptyStringList_DoesNotSetTelemetryFlag()
        {
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;
            _vm!.RemoveColorsCommand.Execute(new List<string> { "color" });
            Assert.IsFalse(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        // ─── ExportColorsGroupedByColorCommand – wrong-element-type IList input ──────
        [TestMethod]
        [Description("CanExecute(non-empty List<string>) on ExportByColor must return false – wrong element type.")]
        public void ExportColorsGroupedByColorCommand_CanExecute_NonEmptyStringList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByColorCommand.CanExecute(new List<string> { "color" }));
        }

        // ─── ExportColorsGroupedByFormatCommand – wrong-element-type IList input ──────
        [TestMethod]
        [Description("CanExecute(non-empty List<string>) on ExportByFormat must return false – wrong element type.")]
        public void ExportColorsGroupedByFormatCommand_CanExecute_NonEmptyStringList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByFormatCommand.CanExecute(new List<string> { "color" }));
        }

        // ─── RemoveColorsCommand – non-IList parameter ───────────────
        [TestMethod]
        [Description("CanExecute(non-IList) must return false for RemoveColorsCommand.")]
        public void RemoveColorsCommand_CanExecute_NonIList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.RemoveColorsCommand.CanExecute(42));
        }

        [TestMethod]
        [Description("CanExecute(non-IList) must return false for ExportByColor.")]
        public void ExportColorsGroupedByColorCommand_CanExecute_NonIList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByColorCommand.CanExecute(42));
        }

        [TestMethod]
        [Description("CanExecute(non-IList) must return false for ExportByFormat.")]
        public void ExportColorsGroupedByFormatCommand_CanExecute_NonIList_ReturnsFalse()
        {
            Assert.IsFalse(_vm!.ExportColorsGroupedByFormatCommand.CanExecute(42));
        }

        [TestMethod]
        [Description("Execute(non-IList) on RemoveColorsCommand must be a no-op: no ColorsHistory mutation, no telemetry.")]
        public void RemoveColorsCommand_Execute_NonIList_IsNoOp()
        {
            _vm!.ColorsHistory.Add(AnyColor);
            int before = _vm.ColorsHistory.Count;
            int indexBefore = _vm.SelectedColorIndex;
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;

            _vm.RemoveColorsCommand.Execute(42);

            Assert.AreEqual(before, _vm.ColorsHistory.Count);
            Assert.AreEqual(indexBefore, _vm.SelectedColorIndex);
            Assert.IsFalse(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        [TestMethod]
        [Description("Execute(non-IList) on ExportByColor must return at guard without throwing.")]
        public async Task ExportColorsGroupedByColorCommand_Execute_NonIList_ReturnsAtGuard()
        {
            _vm!.ExportColorsGroupedByColorCommand.Execute(42);
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByColorCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }

        [TestMethod]
        [Description("Execute(non-IList) on ExportByFormat must return at guard without throwing.")]
        public async Task ExportColorsGroupedByFormatCommand_Execute_NonIList_ReturnsAtGuard()
        {
            _vm!.ExportColorsGroupedByFormatCommand.Execute(42);
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByFormatCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }

        // ─── RemoveColorsCommand – mixed [Color, string] IList input ────────────────
        [TestMethod]
        [Description("Execute(mixed [Color, string] list) must be a no-op even when the Color in the list is present in ColorsHistory.")]
        public void RemoveColorsCommand_Execute_MixedList_IsNoOp()
        {
            // Seed ColorsHistory so AnyColor is a real candidate for deletion.
            _vm!.ColorsHistory.Add(AnyColor);
            var mixed = new List<object> { AnyColor, "extra" };
            SessionEventHelper.Event.EditorHistoryColorRemoved = false;
            int countBefore = _vm.ColorsHistory.Count;
            int indexBefore = _vm.SelectedColorIndex;

            _vm.RemoveColorsCommand.Execute(mixed);

            // Color must still be present at its original position.
            Assert.AreEqual(countBefore, _vm.ColorsHistory.Count);
            Assert.IsTrue(_vm.ColorsHistory.Contains(AnyColor));
            Assert.AreEqual(AnyColor, _vm.ColorsHistory[0]);
            Assert.AreEqual(indexBefore, _vm.SelectedColorIndex);
            Assert.IsFalse(SessionEventHelper.Event.EditorHistoryColorRemoved);
        }

        [TestMethod]
        public void SelectedColor_Set_UpdatesEveryColorRepresentation()
        {
            var format = new ColorFormatModel
            {
                FormatName = "Red",
                Convert = color => color.R.ToString(CultureInfo.InvariantCulture),
            };
            _vm!.ColorRepresentations.Add(format);

            _vm.SelectedColor = AnyColor;

            Assert.AreEqual("100", format.ColorText);
            Assert.AreEqual("Red 100", format.CopyHelperText);
        }

        // ─── ExportColorsGroupedByColorCommand – Execute guards ──────────────────────
        [TestMethod]
        [Description("Execute(mixed [Color, string] list) on ExportByColor must return at guard without throwing.")]
        public async Task ExportColorsGroupedByColorCommand_Execute_MixedList_ReturnsAtGuard()
        {
            var mixed = new List<object> { AnyColor, "extra" };
            _vm!.ExportColorsGroupedByColorCommand.Execute(mixed);
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByColorCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }

        [TestMethod]
        [Description("Execute(non-empty List<string>) on ExportByColor must return at guard without throwing.")]
        public async Task ExportColorsGroupedByColorCommand_Execute_WrongElementType_ReturnsAtGuard()
        {
            _vm!.ExportColorsGroupedByColorCommand.Execute(new List<string> { "color" });
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByColorCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }

        // ─── ExportColorsGroupedByFormatCommand – Execute guards ─────────────────────
        [TestMethod]
        [Description("Execute(mixed [Color, string] list) on ExportByFormat must return at guard without throwing.")]
        public async Task ExportColorsGroupedByFormatCommand_Execute_MixedList_ReturnsAtGuard()
        {
            var mixed = new List<object> { AnyColor, "extra" };
            _vm!.ExportColorsGroupedByFormatCommand.Execute(mixed);
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByFormatCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }

        [TestMethod]
        [Description("Execute(non-empty List<string>) on ExportByFormat must return at guard without throwing.")]
        public async Task ExportColorsGroupedByFormatCommand_Execute_WrongElementType_ReturnsAtGuard()
        {
            _vm!.ExportColorsGroupedByFormatCommand.Execute(new List<string> { "color" });
            var cmd = (IAsyncRelayCommand)_vm.ExportColorsGroupedByFormatCommand;
            if (cmd.ExecutionTask is { } task)
            {
                await task;
            }
        }
    }
}
