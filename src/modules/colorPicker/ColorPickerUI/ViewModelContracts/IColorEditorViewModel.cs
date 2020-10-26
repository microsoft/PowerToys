// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorPicker.ViewModelContracts
{
    public interface IColorEditorViewModel
    {
        event EventHandler CloseWindowRequested;

        event EventHandler OpenColorPickerRequested;

        ICommand CloseWindowCommand { get; }

        ICommand OpenColorPickerCommand { get; }

        ICommand RemoveColorCommand { get; }

        ObservableCollection<Color> ColorsHistory { get; }

        Color SelectedColor { get; set; }

        int SelectedColorIndex { get; set; }

        void Initialize();
    }
}
