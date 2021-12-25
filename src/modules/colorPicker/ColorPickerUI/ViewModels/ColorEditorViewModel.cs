// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using ColorPicker.Common;
using ColorPicker.Helpers;
using ColorPicker.Models;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

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

        [ImportingConstructor]
        public ColorEditorViewModel(IUserSettings userSettings)
        {
            OpenColorPickerCommand = new RelayCommand(() => OpenColorPickerRequested?.Invoke(this, EventArgs.Empty));
            OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
            RemoveColorCommand = new RelayCommand(DeleteSelectedColor);

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

        public event EventHandler OpenSettingsRequested;

        public ICommand OpenColorPickerCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand RemoveColorCommand { get; }

        public ICommand SelectedColorChangedCommand { get; }

        public ICommand HideColorFormatCommand { get; }

        public ObservableCollection<Color> ColorsHistory { get; } = new ObservableCollection<Color>();

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
                _userSettings.ColorHistory.ClearWithoutNotification();
                foreach (var item in ColorsHistory)
                {
                    _userSettings.ColorHistory.AddWithoutNotification(item.A + "|" + item.R + "|" + item.G + "|" + item.B);
                }

                _userSettings.ColorHistory.ReleaseNotification();
            }
        }

        private void DeleteSelectedColor()
        {
            // select new color on the same index if possible, otherwise the last one
            var indexToSelect = SelectedColorIndex == ColorsHistory.Count - 1 ? ColorsHistory.Count - 2 : SelectedColorIndex;
            ColorsHistory.RemoveAt(SelectedColorIndex);
            SelectedColorIndex = indexToSelect;
            SessionEventHelper.Event.EditorHistoryColorRemoved = true;
        }

        private void SetupAllColorRepresentations()
        {
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HEX.ToString(),
#pragma warning disable CA1304 // Specify CultureInfo
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HEX).ToLower(); },
#pragma warning restore CA1304 // Specify CultureInfo
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.RGB.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.RGB); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSL.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSL); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSV.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSV); },
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CMYK.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CMYK); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSB.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSB); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSI.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSI); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HWB.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HWB); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.NCol.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.NCol); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CIELAB.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CIELAB); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CIEXYZ.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CIEXYZ); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.VEC4.ToString(),
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.VEC4); },
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "Decimal",
                    Convert = (Color color) => { return ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.DecimalValue); },
                });

            _userSettings.VisibleColorFormats.CollectionChanged += VisibleColorFormats_CollectionChanged;

            // Any other custom format to be added here as well that are read from settings
        }

        private void VisibleColorFormats_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SetupAvailableColorRepresentations();
        }

        private void SetupAvailableColorRepresentations()
        {
            ColorRepresentations.Clear();

            foreach (var colorFormat in _userSettings.VisibleColorFormats)
            {
                var colorRepresentation = _allColorRepresentations.FirstOrDefault(it => it.FormatName.ToUpperInvariant() == colorFormat.ToUpperInvariant());
                if (colorRepresentation != null)
                {
                    ColorRepresentations.Add(colorRepresentation);
                }
            }
        }
    }
}
