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

        private readonly CanvasLayoutModel _blankModel;
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
            // Initialize default layout models: Blank, Focus, Columns, Rows, Grid, and PriorityGrid
            _blankModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Blank, LayoutType.Blank);
            _blankModel.TemplateZoneCount = 0;
            _blankModel.SensitivityRadius = 0;
            DefaultModels.Add(_blankModel);

            _focusModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Focus, LayoutType.Focus);
            _focusModel.InitTemplateZones();
            DefaultModels.Add(_focusModel);

            _columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new List<int>(1) { GridLayoutModel.GridMultiplier },
            };
            _columnsModel.InitTemplateZones();
            DefaultModels.Add(_columnsModel);

            _rowsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Rows, LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new List<int>(1) { GridLayoutModel.GridMultiplier },
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

        public static IList<LayoutModel> DefaultModels { get; } = new List<LayoutModel>(6);

        public static ObservableCollection<LayoutModel> CustomModels
        {
            get
            {
                return _customModels;
            }
        }

        private static ObservableCollection<LayoutModel> _customModels = new ObservableCollection<LayoutModel>();

        public LayoutModel SelectedModel
        {
            get
            {
                return _selectedModel;
            }

            private set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    FirePropertyChanged(nameof(SelectedModel));
                }
            }
        }

        private LayoutModel _selectedModel = null;

        public LayoutModel AppliedModel
        {
            get
            {
                return _appliedModel;
            }

            private set
            {
                if (_appliedModel != value)
                {
                    _appliedModel = value;
                    FirePropertyChanged(nameof(AppliedModel));
                }
            }
        }

        private LayoutModel _appliedModel = null;

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        public LayoutModel UpdateSelectedLayoutModel()
        {
            LayoutModel foundModel = null;
            LayoutSettings currentApplied = App.Overlay.CurrentLayoutSettings;

            // set new layout
            if (currentApplied.Type == LayoutType.Custom)
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
                        if (foundModel is GridLayoutModel grid)
                        {
                            grid.ShowSpacing = currentApplied.ShowSpacing;
                            grid.Spacing = currentApplied.Spacing;
                        }

                        foundModel.InitTemplateZones();
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = DefaultModels[5]; // PriorityGrid
            }

            SetSelectedModel(foundModel);
            SetAppliedModel(foundModel);
            FirePropertyChanged(nameof(IsCustomLayoutActive));
            return foundModel;
        }

        public void RestoreSelectedModel(LayoutModel model)
        {
            if (SelectedModel == null || model == null)
            {
                return;
            }

            SelectedModel.SensitivityRadius = model.SensitivityRadius;
            SelectedModel.TemplateZoneCount = model.TemplateZoneCount;
            SelectedModel.IsSelected = model.IsSelected;
            SelectedModel.IsApplied = model.IsApplied;
            SelectedModel.Name = model.Name;

            if (model is GridLayoutModel grid)
            {
                ((GridLayoutModel)SelectedModel).Spacing = grid.Spacing;
                ((GridLayoutModel)SelectedModel).ShowSpacing = grid.ShowSpacing;
            }
        }

        public void SetSelectedModel(LayoutModel model)
        {
            if (_selectedModel != null)
            {
                _selectedModel.IsSelected = false;
            }

            if (model != null)
            {
                model.IsSelected = true;
            }

            SelectedModel = model;
        }

        public void SetAppliedModel(LayoutModel model)
        {
            if (_appliedModel != null)
            {
                _appliedModel.IsApplied = false;
            }

            if (model != null)
            {
                model.IsApplied = true;
            }

            AppliedModel = model;
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
