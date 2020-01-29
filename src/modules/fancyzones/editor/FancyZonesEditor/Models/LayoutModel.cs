// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FancyZonesEditor.Models
{
    public enum LayoutType
    {
        Focus,
        Columns,
        Rows,
        Grid,
        PriorityGrid,
        Custom,
        Blank,
    }

    // Base LayoutModel
    //  Manages common properties and base persistence
    public abstract class LayoutModel : INotifyPropertyChanged
    {
        private static readonly string _registryPath = Settings.RegistryPath + "\\Layouts";
        private static readonly string _fullRegistryPath = Settings.FullRegistryPath + "\\Layouts";

        protected LayoutModel()
        {
            _guid = Guid.NewGuid();
            Type = LayoutType.Custom;
        }

        protected LayoutModel(string name)
            : this()
        {
            _guid = Guid.NewGuid();
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
                    FirePropertyChanged("Name");
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
                    FirePropertyChanged("IsSelected");
                }
            }
        }

        private bool _isSelected;

        // implementation of INotifyProeprtyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged(string propertyName)
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

        public static void SerializeDeletedCustomZoneSets()
        {
            FileStream outputStream = File.Open(Settings.CustomZoneSetsTmpFile, FileMode.Create);
            var writer = new Utf8JsonWriter(outputStream, options: default);
            writer.WriteStartObject();
            writer.WriteStartArray("deleted-custom-zone-sets");
            foreach (string zoneSet in _deletedCustomModels)
            {
                writer.WriteStringValue(zoneSet);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            outputStream.Close();
        }

        // Loads all the custom Layouts from tmp file passed by FancuZonesLib
        public static ObservableCollection<LayoutModel> LoadCustomModels()
        {
            _customModels = new ObservableCollection<LayoutModel>();

            FileStream inputStream = File.Open(Settings.CustomZoneSetsTmpFile, FileMode.Open);
            var jsonObject = JsonDocument.Parse(inputStream, options: default);
            JsonElement.ArrayEnumerator customZoneSetsEnumerator = jsonObject.RootElement.GetProperty("custom-zone-sets").EnumerateArray();
            while (customZoneSetsEnumerator.MoveNext())
            {
                var current = customZoneSetsEnumerator.Current;
                string name = current.GetProperty("name").GetString();
                string type = current.GetProperty("type").GetString();
                string uuid = current.GetProperty("uuid").GetString();
                var info = current.GetProperty("info");
                if (type.Equals("grid"))
                {
                    int rows = info.GetProperty("rows").GetInt32();
                    int columns = info.GetProperty("columns").GetInt32();
                    int[] rowsPercentage = new int[rows];
                    JsonElement.ArrayEnumerator rowsPercentageEnumerator = info.GetProperty("rows-percentage").EnumerateArray();
                    int i = 0;
                    while (rowsPercentageEnumerator.MoveNext())
                    {
                        rowsPercentage[i++] = rowsPercentageEnumerator.Current.GetInt32();
                    }

                    i = 0;
                    int[] columnsPercentage = new int[columns];
                    JsonElement.ArrayEnumerator columnsPercentageEnumerator = info.GetProperty("columns-percentage").EnumerateArray();
                    while (columnsPercentageEnumerator.MoveNext())
                    {
                        columnsPercentage[i++] = columnsPercentageEnumerator.Current.GetInt32();
                    }

                    i = 0;
                    JsonElement.ArrayEnumerator cellChildMapRows = info.GetProperty("cell-child-map").EnumerateArray();
                    int[,] cellChildMap = new int[rows, columns];
                    while (cellChildMapRows.MoveNext())
                    {
                        int j = 0;
                        JsonElement.ArrayEnumerator cellChildMapRowElems = cellChildMapRows.Current.EnumerateArray();
                        while (cellChildMapRowElems.MoveNext())
                        {
                            cellChildMap[i, j++] = cellChildMapRowElems.Current.GetInt32();
                        }

                        i++;
                    }

                    _customModels.Add(new GridLayoutModel(uuid, name, LayoutType.Custom, rows, columns, rowsPercentage, columnsPercentage, cellChildMap));
                }
                else if (type.Equals("canvas"))
                {
                    int referenceWidth = info.GetProperty("ref-width").GetInt32();
                    int referenceHeight = info.GetProperty("ref-height").GetInt32();
                    JsonElement.ArrayEnumerator zonesEnumerator = info.GetProperty("zones").EnumerateArray();
                    IList<Int32Rect> zones = new List<Int32Rect>();
                    while (zonesEnumerator.MoveNext())
                    {
                        int x = zonesEnumerator.Current.GetProperty("X").GetInt32();
                        int y = zonesEnumerator.Current.GetProperty("Y").GetInt32();
                        int width = zonesEnumerator.Current.GetProperty("width").GetInt32();
                        int height = zonesEnumerator.Current.GetProperty("height").GetInt32();
                        zones.Add(new Int32Rect(x, y, width, height));
                    }

                    _customModels.Add(new CanvasLayoutModel(uuid, name, LayoutType.Custom, referenceWidth, referenceHeight, zones));
                }
            }

            return _customModels;
        }

        private static ObservableCollection<LayoutModel> _customModels = null;
        private static List<string> _deletedCustomModels = new List<string>();

        // Callbacks that the base LayoutModel makes to derived types
        protected abstract void PersistData();

        public abstract LayoutModel Clone();

        public void Persist(System.Windows.Int32Rect[] zones)
        {
            PersistData();
            Apply(zones);
        }

        public void Apply(System.Windows.Int32Rect[] zones)
        {
            int zoneCount = zones.Length;
            FileStream outputStream = File.Open(Settings.ActiveZoneSetTmpFile, FileMode.Create);
            var writer = new Utf8JsonWriter(outputStream, options: default);

            writer.WriteStartObject();
            writer.WriteString("device-id", Settings.UniqueKey);

            writer.WriteStartObject("active-zoneset");
            writer.WriteString("uuid", "{" + Guid.ToString().ToUpper() + "}");
            bool custom = false;
            switch (Type)
            {
                case LayoutType.Focus:
                    writer.WriteString("type", "focus");
                    break;
                case LayoutType.Rows:
                    writer.WriteString("type", "rows");
                    break;
                case LayoutType.Columns:
                    writer.WriteString("type", "columns");
                    break;
                case LayoutType.Grid:
                    writer.WriteString("type", "grid");
                    break;
                case LayoutType.PriorityGrid:
                    writer.WriteString("type", "priority-grid");
                    break;
                case LayoutType.Custom:
                    writer.WriteString("type", "custom");
                    custom = true;
                    break;
            }

            if (!custom)
            {
                writer.WriteNumber("zone-count", zoneCount);
            }

            writer.WriteEndObject();

            writer.WriteBoolean("editor-show-spacing", Settings._settingsToPersist.ShowSpacing);
            writer.WriteNumber("editor-spacing", Settings._settingsToPersist.Spacing);
            writer.WriteNumber("editor-zone-count", Settings._settingsToPersist.ZoneCount);
            writer.WriteEndObject();
            writer.Flush();
            outputStream.Close();
        }
    }
}
