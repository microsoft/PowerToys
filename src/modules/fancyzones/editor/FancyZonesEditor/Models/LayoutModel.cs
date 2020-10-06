// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;

namespace FancyZonesEditor.Models
{
    public enum LayoutType
    {
        Blank = -1,
        Focus,
        Columns,
        Rows,
        Grid,
        PriorityGrid,
        Custom,
    }

    // Base LayoutModel
    //  Manages common properties and base persistence
    public abstract class LayoutModel : INotifyPropertyChanged
    {
        // Localizable strings
        private const string ErrorMessageBoxTitle = "FancyZones Editor Exception Handler";
        private const string ErrorMessageBoxMessage = "Please report the bug to ";
        private const string ErrorLayoutMalformedData = "Layout '{0}' has malformed data";
        private const string ErrorSerializingDeletedLayouts = "Error serializing deleted layouts";
        private const string ErrorLoadingCustomLayouts = "Error loading custom layouts";
        private const string ErrorApplyingLayout = "Error applying layout";

        // Non-localizable strings
        private const string NameStr = "name";
        private const string CustomZoneSetsJsonTag = "custom-zone-sets";
        private const string TypeJsonTag = "type";
        private const string UuidJsonTag = "uuid";
        private const string InfoJsonTag = "info";
        private const string GridJsonTag = "grid";
        private const string RowsJsonTag = "rows";
        private const string ColumnsJsonTag = "columns";
        private const string RowsPercentageJsonTag = "rows-percentage";
        private const string ColumnsPercentageJsonTag = "columns-percentage";
        private const string CellChildMapJsonTag = "cell-child-map";
        private const string ZonesJsonTag = "zones";
        private const string CanvasJsonTag = "canvas";
        private const string RefWidthJsonTag = "ref-width";
        private const string RefHeightJsonTag = "ref-height";
        private const string XJsonTag = "X";
        private const string YJsonTag = "Y";
        private const string WidthJsonTag = "width";
        private const string HeightJsonTag = "height";
        private const string FocusJsonTag = "focus";
        private const string PriorityGridJsonTag = "priority-grid";
        private const string CustomJsonTag = "custom";

        private const string PowerToysIssuesURL = "https://aka.ms/powerToysReportBug";

        public static void ShowExceptionMessageBox(string message, Exception exception = null)
        {
            string fullMessage = ErrorMessageBoxMessage + PowerToysIssuesURL + " \n" + message;
            if (exception != null)
            {
                fullMessage += ": " + exception.Message;
            }

            MessageBox.Show(fullMessage, ErrorMessageBoxTitle);
        }

        protected LayoutModel()
        {
            _guid = Guid.NewGuid();
            Type = LayoutType.Custom;
        }

        protected LayoutModel(string name)
            : this()
        {
            Name = name;
        }

        protected LayoutModel(string uuid, string name, LayoutType type)
            : this()
        {
            _guid = Guid.Parse(uuid);
            Name = name;
            Type = type;
        }

        protected LayoutModel(string name, LayoutType type)
            : this(name)
        {
            _guid = Guid.NewGuid();
            Type = type;
        }

        // Name - the display name for this layout model - is also used as the key in the registry
        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                if (_name != value)
                {
                    _name = value;
                    FirePropertyChanged();
                }
            }
        }

        private string _name;

        public LayoutType Type { get; set; }

        public Guid Guid
        {
            get
            {
                return _guid;
            }
        }

        private Guid _guid;

        // IsSelected (not-persisted) - tracks whether or not this LayoutModel is selected in the picker
        // TODO: once we switch to a picker per monitor, we need to move this state to the view
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }

            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    FirePropertyChanged();
                }
            }
        }

        private bool _isSelected;

        // implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Removes this Layout from the registry and the loaded CustomModels list
        public void Delete()
        {
            int i = _customModels.IndexOf(this);
            if (i != -1)
            {
                _customModels.RemoveAt(i);
                _deletedCustomModels.Add(Guid.ToString().ToUpper());
            }
        }

        private struct DeletedCustomZoneSetsWrapper
        {
            public List<string> DeletedCustomZoneSets { get; set; }
        }

        public static void SerializeDeletedCustomZoneSets()
        {
            DeletedCustomZoneSetsWrapper deletedLayouts = new DeletedCustomZoneSetsWrapper
            {
                DeletedCustomZoneSets = _deletedCustomModels,
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new DashCaseNamingPolicy(),
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(deletedLayouts, options);
                File.WriteAllText(Settings.DeletedCustomZoneSetsTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                ShowExceptionMessageBox(ErrorSerializingDeletedLayouts, ex);
            }
        }

        // Loads all the custom Layouts from tmp file passed by FancyZonesLib
        public static ObservableCollection<LayoutModel> LoadCustomModels()
        {
            _customModels = new ObservableCollection<LayoutModel>();

            try
            {
                FileStream inputStream = File.Open(Settings.FancyZonesSettingsFile, FileMode.Open);
                JsonDocument jsonObject = JsonDocument.Parse(inputStream, options: default);
                JsonElement.ArrayEnumerator customZoneSetsEnumerator = jsonObject.RootElement.GetProperty(CustomZoneSetsJsonTag).EnumerateArray();

                while (customZoneSetsEnumerator.MoveNext())
                {
                    var current = customZoneSetsEnumerator.Current;
                    string name = current.GetProperty(NameStr).GetString();
                    string type = current.GetProperty(TypeJsonTag).GetString();
                    string uuid = current.GetProperty(UuidJsonTag).GetString();
                    var info = current.GetProperty(InfoJsonTag);

                    if (type.Equals(GridJsonTag))
                    {
                        bool error = false;

                        int rows = info.GetProperty(RowsJsonTag).GetInt32();
                        int columns = info.GetProperty(ColumnsJsonTag).GetInt32();

                        List<int> rowsPercentage = new List<int>(rows);
                        JsonElement.ArrayEnumerator rowsPercentageEnumerator = info.GetProperty(RowsPercentageJsonTag).EnumerateArray();

                        List<int> columnsPercentage = new List<int>(columns);
                        JsonElement.ArrayEnumerator columnsPercentageEnumerator = info.GetProperty(ColumnsPercentageJsonTag).EnumerateArray();

                        if (rows <= 0 || columns <= 0 || rowsPercentageEnumerator.Count() != rows || columnsPercentageEnumerator.Count() != columns)
                        {
                            error = true;
                        }

                        while (!error && rowsPercentageEnumerator.MoveNext())
                        {
                            int percentage = rowsPercentageEnumerator.Current.GetInt32();
                            if (percentage <= 0)
                            {
                                error = true;
                                break;
                            }

                            rowsPercentage.Add(percentage);
                        }

                        while (!error && columnsPercentageEnumerator.MoveNext())
                        {
                            int percentage = columnsPercentageEnumerator.Current.GetInt32();
                            if (percentage <= 0)
                            {
                                error = true;
                                break;
                            }

                            columnsPercentage.Add(percentage);
                        }

                        int i = 0;
                        JsonElement.ArrayEnumerator cellChildMapRows = info.GetProperty(CellChildMapJsonTag).EnumerateArray();
                        int[,] cellChildMap = new int[rows, columns];

                        if (cellChildMapRows.Count() != rows)
                        {
                            error = true;
                        }

                        while (!error && cellChildMapRows.MoveNext())
                        {
                            int j = 0;
                            JsonElement.ArrayEnumerator cellChildMapRowElems = cellChildMapRows.Current.EnumerateArray();
                            if (cellChildMapRowElems.Count() != columns)
                            {
                                error = true;
                                break;
                            }

                            while (cellChildMapRowElems.MoveNext())
                            {
                                cellChildMap[i, j++] = cellChildMapRowElems.Current.GetInt32();
                            }

                            i++;
                        }

                        if (error)
                        {
                            ShowExceptionMessageBox(string.Format(ErrorLayoutMalformedData, name));
                            _deletedCustomModels.Add(Guid.Parse(uuid).ToString().ToUpper());
                            continue;
                        }

                        _customModels.Add(new GridLayoutModel(uuid, name, LayoutType.Custom, rows, columns, rowsPercentage, columnsPercentage, cellChildMap));
                    }
                    else if (type.Equals(CanvasJsonTag))
                    {
                        int lastWorkAreaWidth = info.GetProperty(RefWidthJsonTag).GetInt32();
                        int lastWorkAreaHeight = info.GetProperty(RefHeightJsonTag).GetInt32();

                        JsonElement.ArrayEnumerator zonesEnumerator = info.GetProperty(ZonesJsonTag).EnumerateArray();
                        IList<Int32Rect> zones = new List<Int32Rect>();

                        bool error = false;

                        if (lastWorkAreaWidth <= 0 || lastWorkAreaHeight <= 0)
                        {
                            error = true;
                        }

                        while (!error && zonesEnumerator.MoveNext())
                        {
                            int x = zonesEnumerator.Current.GetProperty(XJsonTag).GetInt32();
                            int y = zonesEnumerator.Current.GetProperty(YJsonTag).GetInt32();
                            int width = zonesEnumerator.Current.GetProperty(WidthJsonTag).GetInt32();
                            int height = zonesEnumerator.Current.GetProperty(HeightJsonTag).GetInt32();

                            if (width <= 0 || height <= 0)
                            {
                                error = true;
                                break;
                            }

                            zones.Add(new Int32Rect(x, y, width, height));
                        }

                        if (error)
                        {
                            ShowExceptionMessageBox(string.Format(ErrorLayoutMalformedData, name));
                            _deletedCustomModels.Add(Guid.Parse(uuid).ToString().ToUpper());
                            continue;
                        }

                        _customModels.Add(new CanvasLayoutModel(uuid, name, LayoutType.Custom, zones, lastWorkAreaWidth, lastWorkAreaHeight));
                    }
                }

                inputStream.Close();
            }
            catch (Exception ex)
            {
                ShowExceptionMessageBox(ErrorLoadingCustomLayouts, ex);
                return new ObservableCollection<LayoutModel>();
            }

            return _customModels;
        }

        private static ObservableCollection<LayoutModel> _customModels = null;
        private static List<string> _deletedCustomModels = new List<string>();

        // Callbacks that the base LayoutModel makes to derived types
        protected abstract void PersistData();

        public abstract LayoutModel Clone();

        public void Persist()
        {
            PersistData();
            Apply();
        }

        private struct ActiveZoneSetWrapper
        {
            public string Uuid { get; set; }

            public string Type { get; set; }
        }

        private struct AppliedZoneSet
        {
            public string DeviceId { get; set; }

            public ActiveZoneSetWrapper ActiveZoneset { get; set; }

            public bool EditorShowSpacing { get; set; }

            public int EditorSpacing { get; set; }

            public int EditorZoneCount { get; set; }

            public int EditorSensitivityRadius { get; set; }
        }

        public void Apply()
        {
            ActiveZoneSetWrapper activeZoneSet = new ActiveZoneSetWrapper
            {
                Uuid = "{" + Guid.ToString().ToUpper() + "}",
            };

            switch (Type)
            {
                case LayoutType.Focus:
                    activeZoneSet.Type = FocusJsonTag;
                    break;
                case LayoutType.Rows:
                    activeZoneSet.Type = RowsJsonTag;
                    break;
                case LayoutType.Columns:
                    activeZoneSet.Type = ColumnsJsonTag;
                    break;
                case LayoutType.Grid:
                    activeZoneSet.Type = GridJsonTag;
                    break;
                case LayoutType.PriorityGrid:
                    activeZoneSet.Type = PriorityGridJsonTag;
                    break;
                case LayoutType.Custom:
                    activeZoneSet.Type = CustomJsonTag;
                    break;
            }

            Settings settings = ((App)Application.Current).ZoneSettings;

            AppliedZoneSet zoneSet = new AppliedZoneSet
            {
                DeviceId = Settings.UniqueKey,
                ActiveZoneset = activeZoneSet,
                EditorShowSpacing = settings.ShowSpacing,
                EditorSpacing = settings.Spacing,
                EditorZoneCount = settings.ZoneCount,
                EditorSensitivityRadius = settings.SensitivityRadius,
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new DashCaseNamingPolicy(),
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(zoneSet, options);
                File.WriteAllText(Settings.ActiveZoneSetTmpFile, jsonString);
            }
            catch (Exception ex)
            {
                ShowExceptionMessageBox(ErrorApplyingLayout, ex);
            }
        }
    }
}
