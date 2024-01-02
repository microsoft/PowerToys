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

        public static DefaultLayoutsModel DefaultLayouts { get; } = new DefaultLayoutsModel();

        public MainWindowSettingsModel()
        {
            // Initialize default layout models: Blank, Focus, Columns, Rows, Grid, and PriorityGrid
            var blankModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Blank, LayoutType.Blank)
            {
                TemplateZoneCount = 0,
                SensitivityRadius = 0,
            };
            TemplateModels.Insert((int)LayoutType.Blank, blankModel);

            var focusModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Focus, LayoutType.Focus);
            focusModel.InitTemplateZones();
            TemplateModels.Insert((int)LayoutType.Focus, focusModel);

            var columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new List<int>(1) { GridLayoutModel.GridMultiplier },
            };
            columnsModel.InitTemplateZones();
            TemplateModels.Insert((int)LayoutType.Columns, columnsModel);

            var rowsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Rows, LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new List<int>(1) { GridLayoutModel.GridMultiplier },
            };
            rowsModel.InitTemplateZones();
            TemplateModels.Insert((int)LayoutType.Rows, rowsModel);

            var gridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Grid, LayoutType.Grid);
            gridModel.InitTemplateZones();
            TemplateModels.Insert((int)LayoutType.Grid, gridModel);

            var priorityGridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Priority_Grid, LayoutType.PriorityGrid);
            priorityGridModel.InitTemplateZones();
            TemplateModels.Insert((int)LayoutType.PriorityGrid, priorityGridModel);

            // set default layouts
            DefaultLayouts.Set(rowsModel, MonitorConfigurationType.Vertical);
            DefaultLayouts.Set(priorityGridModel, MonitorConfigurationType.Horizontal);
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

        public LayoutModel BlankModel
        {
            get
            {
                return TemplateModels[(int)LayoutType.Blank];
            }
        }

        public static IList<LayoutModel> TemplateModels { get; } = new List<LayoutModel>(6);

        public static ObservableCollection<LayoutModel> CustomModels
        {
            get
            {
                return _customModels;
            }

            set
            {
                foreach (LayoutModel model in _customModels)
                {
                    LayoutHotkeys.PropertyChanged -= model.LayoutHotkeys_PropertyChanged;
                }

                _customModels.Clear();
                _customModels = value;

                foreach (LayoutModel model in _customModels)
                {
                    LayoutHotkeys.PropertyChanged += model.LayoutHotkeys_PropertyChanged;
                }
            }
        }

        private static ObservableCollection<LayoutModel> _customModels = new ObservableCollection<LayoutModel>();

        public static int CustomModelsCount
        {
            get
            {
                return _customModels.Count;
            }
        }

        public static LayoutHotkeysModel LayoutHotkeys { get; } = new LayoutHotkeysModel();

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

        private LayoutModel _selectedModel;

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

        private LayoutModel _appliedModel;

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        public void InitModels()
        {
            foreach (var model in TemplateModels)
            {
                model.InitTemplateZones();
            }
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
                    if (string.Equals(model.Uuid, currentApplied.ZonesetUuid, StringComparison.OrdinalIgnoreCase))
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }
            else
            {
                foreach (LayoutModel model in TemplateModels)
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

            SetSelectedModel(foundModel);
            SetAppliedModel(foundModel);
            FirePropertyChanged(nameof(IsCustomLayoutActive));
            return foundModel;
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

        public void UpdateTemplateModels()
        {
            foreach (LayoutModel model in TemplateModels)
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
