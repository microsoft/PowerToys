// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using ColorPicker.Models;
using Windows.UI;

namespace ColorPicker.ViewModelContracts
{
    public interface IColorEditorViewModel
    {
        event EventHandler OpenColorPickerRequested;

        event EventHandler OpenSettingsRequested;

        ICommand OpenColorPickerCommand { get; }

        ICommand OpenSettingsCommand { get; }

        ICommand RemoveColorsCommand { get; }

        ICommand ExportColorsGroupedByColorCommand { get; }

        ICommand ExportColorsGroupedByFormatCommand { get; }

        ObservableCollection<ColorFormatModel> ColorRepresentations { get; }

        ObservableCollection<Color> ColorsHistory { get; }

        Color SelectedColor { get; set; }

        int SelectedColorIndex { get; set; }

        // HWND of the host ColorEditorWindow, assigned by AppStateHandler once the window exists.
        // The WinUI FileSavePicker used by the export commands must be initialized with a valid
        // owner window handle (InitializeWithWindow) on an unpackaged desktop app.
        IntPtr WindowHandle { get; set; }

        void Initialize();
    }
}
