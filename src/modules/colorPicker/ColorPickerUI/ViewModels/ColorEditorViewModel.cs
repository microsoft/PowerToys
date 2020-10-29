// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ColorPicker.Common;
using ColorPicker.Helpers;
using ColorPicker.Models;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;

namespace ColorPicker.ViewModels
{
    [Export(typeof(IColorEditorViewModel))]
    public class ColorEditorViewModel : ViewModelBase, IColorEditorViewModel
    {
        private readonly IUserSettings _userSettings;
        private readonly List<ColorFormatModel> _allColorRepresentations = new List<ColorFormatModel>();
        private Color _selectedColor;
        private bool _initializing;
        private int _selectedColorIndex;
        private string _selectedColorFormat;
        private bool _settingSelectedColorFormat;

        [ImportingConstructor]
        public ColorEditorViewModel(IUserSettings userSettings)
        {
            OpenColorPickerCommand = new RelayCommand(() => OpenColorPickerRequested?.Invoke(this, EventArgs.Empty));
            RemoveColorCommand = new RelayCommand(DeleteSelectedColor);
            HideColorFormatCommand = new RelayCommand((removed) =>
            {
                AvailableColorFormats.Add(((ColorFormatModel)removed).FormatName);
                ColorRepresentations.Remove((ColorFormatModel)removed);
            });
            SelectedColorChangedCommand = new RelayCommand((newColor) =>
            {
                ColorsHistory.Insert(0, (Color)newColor);
                SelectedColorIndex = 0;
            });
            ColorsHistory.CollectionChanged += ColorsHistory_CollectionChanged;
            _userSettings = userSettings;
            SetupAllColorRepresentations();
            SetupAvailableColorRepresentations();
        }

        public event EventHandler OpenColorPickerRequested;

        public ICommand OpenColorPickerCommand { get; }

        public ICommand RemoveColorCommand { get; }

        public ICommand SelectedColorChangedCommand { get; }

        public ICommand HideColorFormatCommand { get; }

        public ObservableCollection<Color> ColorsHistory { get; } = new ObservableCollection<Color>();

        public ObservableCollection<string> AvailableColorFormats { get; } = new ObservableCollection<string>();

        public string SelectedColorFormat
        {
            get
            {
                return _selectedColorFormat;
            }

            set
            {
                if (!_settingSelectedColorFormat)
                {
                    _selectedColorFormat = value;
                    if (value != null)
                    {
                        ColorRepresentations.Add(_allColorRepresentations.First(it => it.FormatName == value));
                        _settingSelectedColorFormat = true;
                        AvailableColorFormats.Remove(value);
                    }

                    _selectedColorFormat = null;
                    OnPropertyChanged();

                    _settingSelectedColorFormat = false;
                }
            }
        }

        public ObservableCollection<ColorFormatModel> ColorRepresentations { get; } = new ObservableCollection<ColorFormatModel>();

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

        private void SetupAllColorRepresentations()
        {
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "HEX",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentation(color, Microsoft.PowerToys.Settings.UI.Library.ColorRepresentationType.HEX); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "RGB",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentation(color, Microsoft.PowerToys.Settings.UI.Library.ColorRepresentationType.RGB); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "HSL",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentation(color, Microsoft.PowerToys.Settings.UI.Library.ColorRepresentationType.HSL); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "HSV",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentation(color, Microsoft.PowerToys.Settings.UI.Library.ColorRepresentationType.HSV); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "CMYK",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentation(color, Microsoft.PowerToys.Settings.UI.Library.ColorRepresentationType.CMYK); },
                });

            // Any other custom format to be added here as well that are read from settings
        }

        private void SetupAvailableColorRepresentations()
        {
            foreach (var colorFormat in _userSettings.VisibleColorFormats)
            {
                var colorRepresentation = _allColorRepresentations.FirstOrDefault(it => it.FormatName.ToUpperInvariant() == colorFormat.ToUpperInvariant());
                if (colorRepresentation != null)
                {
                    ColorRepresentations.Add(colorRepresentation);
                }
            }

            foreach (var colorFormat in _allColorRepresentations.Except(ColorRepresentations))
            {
                AvailableColorFormats.Add(colorFormat.FormatName);
            }
        }
    }
}
