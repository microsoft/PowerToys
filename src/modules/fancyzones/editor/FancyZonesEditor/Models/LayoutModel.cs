// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FancyZonesEditor.Models
{
    // Base LayoutModel
    //  Manages common properties and base persistence
    public abstract class LayoutModel : INotifyPropertyChanged
    {
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

        public string Uuid
        {
            get
            {
                return "{" + Guid.ToString().ToUpper() + "}";
            }
        }

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

        // Adds new custom Layout
        public void AddCustomLayout(LayoutModel model)
        {
            bool updated = false;
            for (int i = 0; i < _customModels.Count && !updated; i++)
            {
                if (_customModels[i].Uuid == model.Uuid)
                {
                    _customModels[i] = model;
                    updated = true;
                }
            }

            if (!updated)
            {
                _customModels.Add(model);
            }
        }

        // Add custom layouts json data that would be serialized to a temp file
        public void AddCustomLayoutJson(JsonElement json)
        {
            _createdCustomLayouts.Add(json);
        }

        public static void SerializeDeletedCustomZoneSets()
        {
            App.FancyZonesEditorIO.SerializeDeletedCustomZoneSets(_deletedCustomModels);
        }

        public static void SerializeCreatedCustomZonesets()
        {
            App.FancyZonesEditorIO.SerializeCreatedCustomZonesets(_createdCustomLayouts);
        }

        // Loads all the custom Layouts from tmp file passed by FancyZonesLib
        public static ObservableCollection<LayoutModel> LoadCustomModels()
        {
            _customModels = new ObservableCollection<LayoutModel>();
            App.FancyZonesEditorIO.ParseLayouts(ref _customModels, ref _deletedCustomModels);
            return _customModels;
        }

        private static ObservableCollection<LayoutModel> _customModels = null;
        private static List<string> _deletedCustomModels = new List<string>();
        private static List<JsonElement> _createdCustomLayouts = new List<JsonElement>();

        // Callbacks that the base LayoutModel makes to derived types
        protected abstract void PersistData();

        public abstract LayoutModel Clone();

        public void Persist()
        {
            PersistData();
            Apply();
        }

        public void Apply()
        {
            // update settings
            App.Overlay.LayoutData[App.Overlay.CurrentDesktopId].ZonesetUuid = Uuid;
            App.Overlay.LayoutData[App.Overlay.CurrentDesktopId].Type = Type;

            // update temp file
            App.FancyZonesEditorIO.SerializeAppliedLayouts();
        }
    }
}
