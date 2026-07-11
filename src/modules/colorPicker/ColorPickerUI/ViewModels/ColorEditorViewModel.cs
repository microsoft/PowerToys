// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ColorPicker.Helpers;
using ColorPicker.Models;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI;

namespace ColorPicker.ViewModels
{
    public class ColorEditorViewModel : ObservableObject, IColorEditorViewModel
    {
        private readonly IUserSettings _userSettings;
        private readonly List<ColorFormatModel> _allColorRepresentations = new List<ColorFormatModel>();
        private Color _selectedColor;
        private bool _initializing;
        private int _selectedColorIndex;

        public ColorEditorViewModel(IUserSettings userSettings)
        {
            OpenColorPickerCommand = new RelayCommand(() => OpenColorPickerRequested?.Invoke(this, EventArgs.Empty));
            OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
            CopyColorTextCommand = new RelayCommand<ColorFormatModel>(CopyColorText);

            RemoveColorsCommand = new RelayCommand<object>(DeleteSelectedColors, IsNonEmptyList);
            ExportColorsGroupedByColorCommand = new AsyncRelayCommand<object>(ExportSelectedColorsByColor, IsNonEmptyList);
            ExportColorsGroupedByFormatCommand = new AsyncRelayCommand<object>(ExportSelectedColorsByFormat, IsNonEmptyList);
            SelectedColorChangedCommand = new RelayCommand<object>((newColor) =>
            {
                if (ColorsHistory.Contains((Color)newColor))
                {
                    ColorsHistory.Remove((Color)newColor);
                }

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

        public ICommand CopyColorTextCommand { get; }

        public ICommand RemoveColorsCommand { get; }

        public ICommand ExportColorsGroupedByColorCommand { get; }

        public ICommand ExportColorsGroupedByFormatCommand { get; }

        public ICommand SelectedColorChangedCommand { get; }

        /// <summary>
        /// Gets or sets the editor window's native handle, used to anchor the WinUI
        /// <see cref="FileSavePicker"/> raised by the export commands (a desktop-app
        /// requirement). The host ColorEditorWindow assigns it once its HWND is available.
        /// </summary>
        public IntPtr WindowHandle { get; set; }

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
                if (SetProperty(ref _selectedColor, value))
                {
                    foreach (var colorFormat in ColorRepresentations)
                    {
                        colorFormat.UpdateColor(value);
                    }
                }
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

        private static bool IsNonEmptyList(object parameter) =>
            parameter is IList { Count: > 0 } list &&
            list.Cast<object>().All(item => item is Color);

        private static void CopyColorText(ColorFormatModel colorFormat)
        {
            ArgumentNullException.ThrowIfNull(colorFormat);

            ClipboardHelper.CopyToClipboard(colorFormat.ColorText);
            SessionEventHelper.Event.EditorColorCopiedToClipboard = true;
        }

        private void DeleteSelectedColors(object selectedColors)
        {
            if (!IsNonEmptyList(selectedColors))
            {
                return;
            }

            var list = (IList)selectedColors;
            var colorsToRemove = list.Cast<Color>().ToList();

            var indicesToRemove = colorsToRemove.Select(color => ColorsHistory.IndexOf(color)).ToList();

            foreach (var color in colorsToRemove)
            {
                ColorsHistory.Remove(color);
            }

            SelectedColorIndex = ComputeWhichIndexToSelectAfterDeletion(colorsToRemove.Count + ColorsHistory.Count, indicesToRemove);
            SessionEventHelper.Event.EditorHistoryColorRemoved = true;
        }

        private Task ExportSelectedColorsByColor(object selectedColors)
        {
            return ExportColors(selectedColors, GroupExportedColorsBy.Color);
        }

        private Task ExportSelectedColorsByFormat(object selectedColors)
        {
            return ExportColors(selectedColors, GroupExportedColorsBy.Format);
        }

        private async Task ExportColors(object colorsToExport, GroupExportedColorsBy method)
        {
            if (!IsNonEmptyList(colorsToExport))
            {
                return;
            }

            var colors = SerializationHelper.ConvertToDesiredColorFormats((IList)colorsToExport, ColorRepresentations, method);

            // WinUI 3 replaces WPF's SaveFileDialog with the WinRT FileSavePicker, which must be
            // anchored to the owning window's HWND in a desktop (unpackaged) app.
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "colors",
            };
            picker.FileTypeChoices.Add("Text Files", new List<string> { ".txt" });
            picker.FileTypeChoices.Add("Json Files", new List<string> { ".json" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var extension = Path.GetExtension(file.Name);

                var contentToWrite = extension.ToUpperInvariant() switch
                {
                    ".TXT" => colors.ToTxt(';'),
                    ".JSON" => colors.ToJson(),
                    _ => string.Empty,
                };

                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, contentToWrite);
                var status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status is FileUpdateStatus.Complete or FileUpdateStatus.CompleteAndRenamed)
                {
                    SessionEventHelper.Event.EditorColorsExported = true;
                }
                else
                {
                    Logger.LogError($"Export failed with file update status: {status}");
                }
            }
        }

        // Will select the closest color to the last selected one in color history
        private static int ComputeWhichIndexToSelectAfterDeletion(int colorsCount, List<int> indicesToRemove)
        {
            var newIndices = Enumerable.Range(0, colorsCount).ToList();
            foreach (var index in indicesToRemove)
            {
                newIndices[index] = -1;
            }

            var appearancesOfMinusOne = 0;
            for (var i = 0; i < newIndices.Count; i++)
            {
                if (newIndices[i] < 0)
                {
                    appearancesOfMinusOne++;
                    continue;
                }

                newIndices[i] -= appearancesOfMinusOne;
            }

            var lastSelectedIndex = indicesToRemove.Last();
            for (int i = lastSelectedIndex - 1, j = lastSelectedIndex + 1; ; i--, j++)
            {
                if (j < newIndices.Count && newIndices[j] != -1)
                {
                    return newIndices[j];
                }

                if (i >= 0 && newIndices[i] != -1)
                {
                    return newIndices[i];
                }

                if (j >= newIndices.Count && i < 0)
                {
                    break;
                }
            }

            return -1;
        }

        private void SetupAllColorRepresentations()
        {
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HEX.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HEX.ToString()).ToLowerInvariant(),
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.RGB.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.RGB.ToString()),
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSL.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSL.ToString()),
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSV.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSV.ToString()),
                });

            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CMYK.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CMYK.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSB.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSB.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HSI.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HSI.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.HWB.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.HWB.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.NCol.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.NCol.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CIEXYZ.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CIEXYZ.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.CIELAB.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.CIELAB.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.Oklab.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.Oklab.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.Oklch.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.Oklch.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = ColorRepresentationType.VEC4.ToString(),
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, ColorRepresentationType.VEC4.ToString()),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "Decimal",
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, "Decimal"),
                });
            _allColorRepresentations.Add(
                new ColorFormatModel()
                {
                    FormatName = "HEX Int",
                    Convert = (Color color) => ColorRepresentationHelper.GetStringRepresentationFromMediaColor(color, "HEX Int"),
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
                var representation = new ColorFormatModel() { FormatName = colorFormat.Key.ToUpperInvariant(), Convert = null, FormatString = colorFormat.Value };
                representation.UpdateColor(SelectedColor);
                ColorRepresentations.Add(representation);
            }
        }
    }
}
