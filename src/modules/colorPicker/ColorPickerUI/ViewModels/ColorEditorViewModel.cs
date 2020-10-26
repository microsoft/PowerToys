// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using ColorPicker.Common;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;

namespace ColorPicker.ViewModels
{
    [Export(typeof(IColorEditorViewModel))]
    public class ColorEditorViewModel : ViewModelBase, IColorEditorViewModel
    {
        private readonly IUserSettings _userSettings;
        private Color _selectedColor;
        private bool _initializing;
        private int _selectedColorIndex;

        [ImportingConstructor]
        public ColorEditorViewModel(IUserSettings userSettings)
        {
            CloseWindowCommand = new RelayCommand(() => CloseWindowRequested?.Invoke(this, EventArgs.Empty));
            OpenColorPickerCommand = new RelayCommand(() => OpenColorPickerRequested?.Invoke(this, EventArgs.Empty));
            RemoveColorCommand = new RelayCommand(DeleteSelectedColor);
            ColorsHistory.CollectionChanged += ColorsHistory_CollectionChanged;
            _userSettings = userSettings;
        }

        public event EventHandler CloseWindowRequested;

        public event EventHandler OpenColorPickerRequested;

        public ICommand CloseWindowCommand { get; }

        public ICommand OpenColorPickerCommand { get; }

        public ICommand RemoveColorCommand { get; }

        public ObservableCollection<Color> ColorsHistory { get; } = new ObservableCollection<Color>();

        public Color SelectedColor
        {
            get
            {
                return _selectedColor;
            }

            set
            {
                _selectedColor = value;
                OnPropertyChanged();
            }
        }

        public int SelectedColorIndex
        {
            get
            {
                return _selectedColorIndex;
            }

            set
            {
                _selectedColorIndex = value;
                if (value >= 0)
                {
                    SelectedColor = ColorsHistory[_selectedColorIndex];
                }

                OnPropertyChanged();
            }
        }

        public void Initialize()
        {
            _initializing = true;

            ColorsHistory.Clear();
            foreach (var item in _userSettings.ColorHistory)
            {
                var parts = item.Split('|');
                ColorsHistory.Add(new Color()
                {
                    A = byte.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    R = byte.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    G = byte.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    B = byte.Parse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture),
                });
                SelectedColorIndex = 0;
            }

            _initializing = false;
        }

        private void ColorsHistory_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_initializing)
            {
                _userSettings.ColorHistory.Clear();
                foreach (var item in ColorsHistory)
                {
                    _userSettings.ColorHistory.Add(item.A + "|" + item.R + "|" + item.G + "|" + item.B);
                }
            }
        }

        private void DeleteSelectedColor()
        {
            // select new color on the same index if possible, otherwise the last one
            var indexToSelect = SelectedColorIndex == ColorsHistory.Count - 1 ? ColorsHistory.Count - 2 : SelectedColorIndex;
            ColorsHistory.RemoveAt(SelectedColorIndex);
            SelectedColorIndex = indexToSelect;
        }
    }
}
