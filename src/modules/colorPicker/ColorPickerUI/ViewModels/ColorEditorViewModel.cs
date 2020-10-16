// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
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

        [ImportingConstructor]
        public ColorEditorViewModel(IUserSettings userSettings)
        {
            CloseWindowCommand = new RelayCommand(() => CloseWindowRequested?.Invoke(this, EventArgs.Empty));
            OpenColorPickerCommand = new RelayCommand(() => OpenColorPickerRequested?.Invoke(this, EventArgs.Empty));

            ColorsHistory.CollectionChanged += ColorsHistory_CollectionChanged;
            _userSettings = userSettings;
        }

        public event EventHandler CloseWindowRequested;

        public event EventHandler OpenColorPickerRequested;

        public ICommand CloseWindowCommand { get; }

        public ICommand OpenColorPickerCommand { get; }

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

        public void Initialize()
        {
            _initializing = true;

            // ColorsHistory.Clear();
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
                SelectedColor = ColorsHistory.First();
            }

            ColorsHistory.Add(Colors.Red);
            ColorsHistory.Add(Colors.LightBlue);
            ColorsHistory.Add(Colors.BlueViolet);
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
    }
}
