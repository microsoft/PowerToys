// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    // Settings
    //  These are the configuration settings used by the rest of the editor
    //  Other UIs in the editor will subscribe to change events on the properties to stay up to date as these properties change
    public class MainWindowSettingsModel : INotifyPropertyChanged
    {
        private enum DeviceIdParts
        {
            Name = 0,
            Width,
            Height,
            VirtualDesktopId,
        }

        private readonly CanvasLayoutModel _focusModel;
        private readonly GridLayoutModel _rowsModel;
        private readonly GridLayoutModel _columnsModel;
        private readonly GridLayoutModel _gridModel;
        private readonly GridLayoutModel _priorityGridModel;

        public const ushort _focusModelId = 0xFFFF;
        public const ushort _rowsModelId = 0xFFFE;
        public const ushort _columnsModelId = 0xFFFD;
        public const ushort _gridModelId = 0xFFFC;
        public const ushort _priorityGridModelId = 0xFFFB;
        public const ushort _blankCustomModelId = 0xFFFA;
        public const ushort _lastDefinedId = _blankCustomModelId;

        // Non-localizable strings
        public static readonly string RegistryPath = "SOFTWARE\\SuperFancyZones";
        public static readonly string FullRegistryPath = "HKEY_CURRENT_USER\\" + RegistryPath;

        private const int _multiplier = 10000;

        public bool IsCustomLayoutActive
        {
            get
            {
                foreach (LayoutModel model in CustomModels)
                {
                    if (model.IsSelected)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public MainWindowSettingsModel()
        {
            // Initialize the five default layout models: Focus, Columns, Rows, Grid, and PriorityGrid
            DefaultModels = new List<LayoutModel>(5);
            _focusModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Focus, LayoutType.Focus);
            _focusModel.InitTemplateZones();
            DefaultModels.Add(_focusModel);

            _columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new List<int>(1) { _multiplier },
            };
            _columnsModel.InitTemplateZones();
            DefaultModels.Add(_columnsModel);

            _rowsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Rows, LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new List<int>(1) { _multiplier },
            };
            _rowsModel.InitTemplateZones();
            DefaultModels.Add(_rowsModel);

            _gridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Grid, LayoutType.Grid);
            _gridModel.InitTemplateZones();
            DefaultModels.Add(_gridModel);

            _priorityGridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Priority_Grid, LayoutType.PriorityGrid);
            _priorityGridModel.InitTemplateZones();
            DefaultModels.Add(_priorityGridModel);
        }

        // IsShiftKeyPressed - is the shift key currently being held down
        public bool IsShiftKeyPressed
        {
            get
            {
                return _isShiftKeyPressed;
            }

            set
            {
                if (_isShiftKeyPressed != value)
                {
                    _isShiftKeyPressed = value;
                    FirePropertyChanged(nameof(IsShiftKeyPressed));
                }
            }
        }

        private bool _isShiftKeyPressed;

        // IsCtrlKeyPressed - is the ctrl key currently being held down
        public bool IsCtrlKeyPressed
        {
            get
            {
                return _isCtrlKeyPressed;
            }

            set
            {
                if (_isCtrlKeyPressed != value)
                {
                    _isCtrlKeyPressed = value;
                    FirePropertyChanged(nameof(IsCtrlKeyPressed));
                }
            }
        }

        private bool _isCtrlKeyPressed;

        public IList<LayoutModel> DefaultModels { get; }

        public static ObservableCollection<LayoutModel> CustomModels
        {
            get
            {
                return _customModels;
            }
        }

        private static ObservableCollection<LayoutModel> _customModels = new ObservableCollection<LayoutModel>();

        public static CanvasLayoutModel BlankModel
        {
            get
            {
                return _blankModel;
            }
        }

        private static CanvasLayoutModel _blankModel = new CanvasLayoutModel(string.Empty, LayoutType.Blank);

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        public LayoutModel UpdateSelectedLayoutModel()
        {
            ResetAppliedModel();
            ResetSelectedModel();

            LayoutModel foundModel = null;
            LayoutSettings currentApplied = App.Overlay.CurrentLayoutSettings;

            // set new layout
            if (currentApplied.Type == LayoutType.Blank)
            {
                foundModel = BlankModel;
            }
            else if (currentApplied.Type == LayoutType.Custom)
            {
                foreach (LayoutModel model in CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpperInvariant() + "}" == currentApplied.ZonesetUuid.ToUpperInvariant())
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }
            else
            {
                foreach (LayoutModel model in DefaultModels)
                {
                    if (model.Type == currentApplied.Type)
                    {
                        // found match
                        foundModel = model;
                        foundModel.TemplateZoneCount = currentApplied.ZoneCount;
                        foundModel.SensitivityRadius = currentApplied.SensitivityRadius;
                        if (foundModel is GridLayoutModel)
                        {
                            ((GridLayoutModel)foundModel).ShowSpacing = currentApplied.ShowSpacing;
                            ((GridLayoutModel)foundModel).Spacing = currentApplied.Spacing;
                        }

                        foundModel.InitTemplateZones();
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = DefaultModels[4]; // PriorityGrid
            }

            foundModel.IsSelected = true;
            foundModel.IsApplied = true;

            FirePropertyChanged(nameof(IsCustomLayoutActive));
            return foundModel;
        }

        public void ResetSelectedModel()
        {
            foreach (LayoutModel model in CustomModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }

            foreach (LayoutModel model in DefaultModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }
        }

        public void ResetAppliedModel()
        {
            foreach (LayoutModel model in CustomModels)
            {
                if (model.IsApplied)
                {
                    model.IsApplied = false;
                    break;
                }
            }

            foreach (LayoutModel model in DefaultModels)
            {
                if (model.IsApplied)
                {
                    model.IsApplied = false;
                    break;
                }
            }
        }

        public void UpdateDefaultModels()
        {
            foreach (LayoutModel model in DefaultModels)
            {
                if (App.Overlay.CurrentLayoutSettings.Type == model.Type && App.Overlay.CurrentLayoutSettings.ZoneCount != model.TemplateZoneCount)
                {
                    model.TemplateZoneCount = App.Overlay.CurrentLayoutSettings.ZoneCount;
                    model.InitTemplateZones();
                }
            }
        }

        // implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
