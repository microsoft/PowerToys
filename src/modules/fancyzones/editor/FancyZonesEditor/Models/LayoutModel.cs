// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

using FancyZonesEditorCommon.Data;

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

            MainWindowSettingsModel.DefaultLayouts.PropertyChanged += DefaultLayouts_PropertyChanged;
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

        protected LayoutModel(LayoutModel other)
        {
            _guid = other._guid;
            _name = other._name;
            Type = other.Type;
            _isSelected = other._isSelected;
            _isApplied = other._isApplied;
            _sensitivityRadius = other._sensitivityRadius;
            _zoneCount = other._zoneCount;
            _quickKey = other._quickKey;
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
                    FirePropertyChanged(nameof(Name));
                }
            }
        }

        private string _name;

        public LayoutType Type { get; set; }

#pragma warning disable CA1720 // Identifier contains type name (Not worth the effort to change this now.)
        public Guid Guid
#pragma warning restore CA1720 // Identifier contains type name
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
                return "{" + Guid.ToString().ToUpperInvariant() + "}";
            }
        }

        public bool IsCustom
        {
            get
            {
                return Type == LayoutType.Custom;
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
                    FirePropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isSelected;

        // IsApplied (not-persisted) - tracks whether or not this LayoutModel is applied in the picker
        public bool IsApplied
        {
            get
            {
                return _isApplied;
            }

            set
            {
                if (_isApplied != value)
                {
                    _isApplied = value;
                    FirePropertyChanged(nameof(IsApplied));
                }
            }
        }

        private bool _isApplied;

        public bool IsHorizontalDefault
        {
            get
            {
                return MainWindowSettingsModel.DefaultLayouts.Layouts[(int)MonitorConfigurationType.Horizontal].Uuid == this.Uuid;
            }
        }

        public bool CanBeSetAsHorizontalDefault
        {
            get
            {
                return MainWindowSettingsModel.DefaultLayouts.Layouts[(int)MonitorConfigurationType.Horizontal].Uuid != this.Uuid;
            }
        }

        public bool IsVerticalDefault
        {
            get
            {
                return MainWindowSettingsModel.DefaultLayouts.Layouts[MonitorConfigurationType.Vertical].Uuid == this.Uuid;
            }
        }

        public bool CanBeSetAsVerticalDefault
        {
            get
            {
                return MainWindowSettingsModel.DefaultLayouts.Layouts[MonitorConfigurationType.Vertical].Uuid != this.Uuid;
            }
        }

        public int SensitivityRadius
        {
            get
            {
                return _sensitivityRadius;
            }

            set
            {
                if (value != _sensitivityRadius)
                {
                    _sensitivityRadius = value;
                    FirePropertyChanged(nameof(SensitivityRadius));
                }
            }
        }

        private int _sensitivityRadius = LayoutDefaultSettings.DefaultSensitivityRadius;

        public int SensitivityRadiusMinimum
        {
            get
            {
                return 0;
            }
        }

        public int SensitivityRadiusMaximum
        {
            get
            {
                return 1000;
            }
        }

        public List<string> QuickKeysAvailable
        {
            get
            {
                List<string> result = new List<string>();
                foreach (var pair in MainWindowSettingsModel.LayoutHotkeys.SelectedKeys)
                {
                    if (string.IsNullOrEmpty(pair.Value) || pair.Value == Uuid)
                    {
                        result.Add(pair.Key);
                    }
                }

                return result;
            }
        }

        public string QuickKey
        {
            get
            {
                return _quickKey == -1 ? Properties.Resources.Quick_Key_None : _quickKey.ToString(CultureInfo.CurrentCulture);
            }

            set
            {
                var intValue = -1;
                string none = Properties.Resources.Quick_Key_None;

                if (value != none && int.TryParse(value, out var parsedInt))
                {
                    intValue = parsedInt;
                }

                if (intValue != _quickKey)
                {
                    string prev = _quickKey == -1 ? none : _quickKey.ToString(CultureInfo.CurrentCulture);
                    _quickKey = intValue;

                    if (intValue != -1)
                    {
                        MainWindowSettingsModel.LayoutHotkeys.SelectKey(value, Uuid);
                    }
                    else
                    {
                        MainWindowSettingsModel.LayoutHotkeys.FreeKey(prev);
                    }

                    FirePropertyChanged(nameof(QuickKey));
                }
            }
        }

        private int _quickKey = -1;

        // TemplateZoneCount - number of zones selected in the picker window for template layouts
        public int TemplateZoneCount
        {
            get
            {
                return _zoneCount;
            }

            set
            {
                if (value != _zoneCount)
                {
                    _zoneCount = value;
                    InitTemplateZones();
                    FirePropertyChanged(nameof(TemplateZoneCount));
                    FirePropertyChanged(nameof(IsZoneAddingAllowed));
                }
            }
        }

        public int TemplateZoneCountMinimum
        {
            get
            {
                return 1;
            }
        }

        public int TemplateZoneCountMaximum
        {
            get
            {
                return 128;
            }
        }

        private int _zoneCount = LayoutDefaultSettings.DefaultZoneCount;

        public bool IsZoneAddingAllowed
        {
            get
            {
                return TemplateZoneCount < LayoutDefaultSettings.MaxZones;
            }
        }

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
            var customModels = MainWindowSettingsModel.CustomModels;
            if (_quickKey != -1)
            {
                MainWindowSettingsModel.LayoutHotkeys.FreeKey(QuickKey);
                foreach (var module in customModels)
                {
                    module.FirePropertyChanged(nameof(QuickKeysAvailable));
                }
            }

            int i = customModels.IndexOf(this);
            if (i != -1)
            {
                customModels.RemoveAt(i);
            }
        }

        public void RestoreTo(LayoutModel layout)
        {
            layout.SensitivityRadius = SensitivityRadius;
            layout.TemplateZoneCount = TemplateZoneCount;
        }

        // Adds new custom Layout
        public void AddCustomLayout(LayoutModel model)
        {
            bool updated = false;
            var customModels = MainWindowSettingsModel.CustomModels;
            for (int i = 0; i < customModels.Count && !updated; i++)
            {
                if (customModels[i].Uuid == model.Uuid)
                {
                    customModels[i] = model;
                    updated = true;
                }
            }

            if (!updated)
            {
                customModels.Add(model);
            }
        }

        // InitTemplateZones
        // Creates zones based on template zones count
        public abstract void InitTemplateZones();

        // Callbacks that the base LayoutModel makes to derived types
        protected abstract void PersistData();

        public abstract LayoutModel Clone();

        public void Persist()
        {
            PersistData();
            FirePropertyChanged(nameof(PersistData));
        }

        public void LayoutHotkeys_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            foreach (var pair in MainWindowSettingsModel.LayoutHotkeys.SelectedKeys)
            {
                if (pair.Value == Uuid)
                {
                    QuickKey = pair.Key.ToString();
                    break;
                }
            }
        }

        public void DefaultLayouts_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            FirePropertyChanged(nameof(IsHorizontalDefault));
            FirePropertyChanged(nameof(IsVerticalDefault));
            FirePropertyChanged(nameof(CanBeSetAsHorizontalDefault));
            FirePropertyChanged(nameof(CanBeSetAsVerticalDefault));
        }
    }
}
