// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor
{
    // Settings
    //  These are the configuration settings used by the rest of the editor
    //  Other UIs in the editor will subscribe to change events on the properties to stay up to date as these properties change
    public class Settings : INotifyPropertyChanged
    {
        private enum DeviceIdParts
        {
            Name = 0,
            Width,
            Height,
            VirtualDesktopId,
        }

        private static CanvasLayoutModel _blankCustomModel;
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

        private const int MaxNegativeSpacing = -10;

        // Non-localizable strings
        public static readonly string RegistryPath = "SOFTWARE\\SuperFancyZones";
        public static readonly string FullRegistryPath = "HKEY_CURRENT_USER\\" + RegistryPath;

        private const string LayoutTypeBlankStr = "blank";
        private const string NullUuidStr = "null";

        // hard coded data for all the "Priority Grid" configurations that are unique to "Grid"
        private static readonly byte[][] _priorityData = new byte[][]
        {
            new byte[] { 0, 0, 0, 0, 0, 1, 1, 39, 16, 39, 16, 0 },
            new byte[] { 0, 0, 0, 0, 0, 1, 2, 39, 16, 26, 11, 13, 5, 0, 1 },
            new byte[] { 0, 0, 0, 0, 0, 1, 3, 39, 16, 9, 196, 19, 136, 9, 196, 0, 1, 2 },
            new byte[] { 0, 0, 0, 0, 0, 2, 3, 19, 136, 19, 136, 9, 196, 19, 136, 9, 196, 0, 1, 2, 0, 1, 3 },
            new byte[] { 0, 0, 0, 0, 0, 2, 3, 19, 136, 19, 136, 9, 196, 19, 136, 9, 196, 0, 1, 2, 3, 1, 4 },
            new byte[] { 0, 0, 0, 0, 0, 3, 3, 13, 5, 13, 6, 13, 5, 9, 196, 19, 136, 9, 196, 0, 1, 2, 0, 1, 3, 4, 1, 5 },
            new byte[] { 0, 0, 0, 0, 0, 3, 3, 13, 5, 13, 6, 13, 5, 9, 196, 19, 136, 9, 196, 0, 1, 2, 3, 1, 4, 5, 1, 6 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 2, 5, 6, 1, 2, 7 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 2, 5, 6, 1, 7, 8 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 5, 6, 7, 1, 8, 9 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 5, 6, 7, 8, 9, 10 },
        };

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

        public Settings()
        {
            string tmpDirPath = _fileSystem.Path.GetTempPath();
            DesktopsCount = Screen.AllScreens.Length;
            Area = new WorkArea(DesktopsCount);
            AppliedLayouts = new List<LayoutSettings>(DesktopsCount);

            for (int i = 0; i < DesktopsCount; i++)
            {
                AppliedLayouts.Add(new LayoutSettings());
            }

            // Initialize the five default layout models: Focus, Columns, Rows, Grid, and PriorityGrid
            DefaultModels = new List<LayoutModel>(5);
            _focusModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Focus, LayoutType.Focus);
            DefaultModels.Add(_focusModel);

            _columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new List<int>(1) { _multiplier },
            };
            DefaultModels.Add(_columnsModel);

            _rowsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Rows, LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new List<int>(1) { _multiplier },
            };
            DefaultModels.Add(_rowsModel);

            _gridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Grid, LayoutType.Grid);
            DefaultModels.Add(_gridModel);

            _priorityGridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Priority_Grid, LayoutType.PriorityGrid);
            DefaultModels.Add(_priorityGridModel);

            _blankCustomModel = new CanvasLayoutModel(Properties.Resources.Custom_Layout_Create_New, LayoutType.Blank);

            UpdateLayoutModels();
        }

        // ZoneCount - number of zones selected in the picker window
        public int ZoneCount
        {
            get
            {
                if (CurrentDesktop < AppliedLayouts.Count)
                {
                    return AppliedLayouts[CurrentDesktop].ZoneCount;
                }

                return LayoutSettings.DefaultZoneCount;
            }

            set
            {
                if (AppliedLayouts[CurrentDesktop].ZoneCount != value)
                {
                    AppliedLayouts[CurrentDesktop].ZoneCount = value;
                    UpdateLayoutModels();
                    FirePropertyChanged(nameof(ZoneCount));
                }
            }
        }

        // Spacing - how much space in between zones of the grid do you want
        public int Spacing
        {
            get
            {
                if (CurrentDesktop < AppliedLayouts.Count)
                {
                    return AppliedLayouts[CurrentDesktop].Spacing;
                }

                return LayoutSettings.DefaultSpacing;
            }

            set
            {
                value = Math.Max(0, value);
                if (AppliedLayouts[CurrentDesktop].Spacing != value)
                {
                    AppliedLayouts[CurrentDesktop].Spacing = value;
                    FirePropertyChanged(nameof(Spacing));
                    UpdateLayoutModels();
                }
            }
        }

        // ShowSpacing - is the Spacing value used or ignored?
        public bool ShowSpacing
        {
            get
            {
                if (CurrentDesktop < AppliedLayouts.Count)
                {
                    return AppliedLayouts[CurrentDesktop].ShowSpacing;
                }

                return LayoutSettings.DefaultShowSpacing;
            }

            set
            {
                if (AppliedLayouts[CurrentDesktop].ShowSpacing != value)
                {
                    AppliedLayouts[CurrentDesktop].ShowSpacing = value;
                    FirePropertyChanged(nameof(ShowSpacing));
                    UpdateLayoutModels();
                }
            }
        }

        // SensitivityRadius - how much space inside the zone to highlight the adjacent zone too
        public int SensitivityRadius
        {
            get
            {
                if (CurrentDesktop < AppliedLayouts.Count)
                {
                    return AppliedLayouts[CurrentDesktop].SensitivityRadius;
                }

                return LayoutSettings.DefaultSensitivityRadius;
            }

            set
            {
                value = Math.Max(0, value);
                if (AppliedLayouts[CurrentDesktop].SensitivityRadius != value)
                {
                    AppliedLayouts[CurrentDesktop].SensitivityRadius = value;
                    FirePropertyChanged(nameof(SensitivityRadius));
                    UpdateLayoutModels();
                }
            }
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

        public static WorkArea Area { get; private set; }

        public static List<Rect> UsedWorkAreas { get; private set; }

        public static string ActiveZoneSetTmpFile { get; private set; }

        public static string AppliedZoneSetTmpFile { get; private set; }

        public static string DeletedCustomZoneSetsTmpFile { get; private set; }

        public static string FancyZonesSettingsFile { get; private set; }

        public static int PowerToysPID { get; private set; }

        public static int PreviousDesktop { get; private set; }

        public int CurrentDesktop
        {
            get
            {
                return _currentDesktop;
            }

            set
            {
                if (value != _currentDesktop)
                {
                    if (value < 0 || value >= DesktopsCount)
                    {
                        return;
                    }

                    PreviousDesktop = _currentDesktop;
                    _currentDesktop = value;
                    UpdateLayoutModels();

                    if (AppliedLayouts[PreviousDesktop].ZoneCount != AppliedLayouts[value].ZoneCount)
                    {
                        FirePropertyChanged(nameof(ZoneCount));
                    }

                    if (AppliedLayouts[PreviousDesktop].Spacing != AppliedLayouts[value].Spacing)
                    {
                        FirePropertyChanged(nameof(Spacing));
                    }

                    if (AppliedLayouts[PreviousDesktop].ShowSpacing != AppliedLayouts[value].ShowSpacing)
                    {
                        FirePropertyChanged(nameof(ShowSpacing));
                    }

                    if (AppliedLayouts[PreviousDesktop].SensitivityRadius != AppliedLayouts[value].SensitivityRadius)
                    {
                        FirePropertyChanged(nameof(SensitivityRadius));
                    }
                }
            }
        }

        public static int CurrentDesktopId
        {
            get { return _currentDesktop; }
        }

        private static int _currentDesktop = 0;

        public static int DesktopsCount { get; private set; }

        public static List<AppliedZoneset> AppliedLayouts { get; set; }

        public static bool SpanZonesAcrossMonitors { get; private set; }

        // UpdateLayoutModels
        // Update the five default layouts based on the new ZoneCount
        private void UpdateLayoutModels()
        {
            // Update the "Focus" Default Layout
            _focusModel.Zones.Clear();

            // Sanity check for imported settings that may have invalid data
            if (ZoneCount < 1)
            {
                ZoneCount = 3;
            }

            // If changing focus layout zones size and/or increment,
            // same change should be applied in ZoneSet.cpp (ZoneSet::CalculateFocusLayout)
            var workingArea = WorkArea.WorkingAreaRect;
            Int32Rect focusZoneRect = new Int32Rect(100, 100, (int)(workingArea.Width * 0.4), (int)(workingArea.Height * 0.4));
            int focusRectXIncrement = (ZoneCount <= 1) ? 0 : 50;
            int focusRectYIncrement = (ZoneCount <= 1) ? 0 : 50;

            for (int i = 0; i < ZoneCount; i++)
            {
                _focusModel.Zones.Add(focusZoneRect);
                focusZoneRect.X += focusRectXIncrement;
                focusZoneRect.Y += focusRectYIncrement;
            }

            // Update the "Rows" and "Columns" Default Layouts
            // They can share their model, just transposed
            _rowsModel.CellChildMap = new int[ZoneCount, 1];
            _columnsModel.CellChildMap = new int[1, ZoneCount];
            _rowsModel.Rows = _columnsModel.Columns = ZoneCount;
            _rowsModel.RowPercents = _columnsModel.ColumnPercents = new List<int>(ZoneCount);

            for (int i = 0; i < ZoneCount; i++)
            {
                _rowsModel.CellChildMap[i, 0] = i;
                _columnsModel.CellChildMap[0, i] = i;

                // Note: This is NOT equal to _multiplier / ZoneCount and is done like this to make
                // the sum of all RowPercents exactly (_multiplier).
                // _columnsModel is sharing the same array
                _rowsModel.RowPercents.Add(((_multiplier * (i + 1)) / ZoneCount) - ((_multiplier * i) / ZoneCount));
            }

            // Update the "Grid" Default Layout
            int rows = 1;
            while (ZoneCount / rows >= rows)
            {
                rows++;
            }

            rows--;
            int cols = ZoneCount / rows;
            if (ZoneCount % rows == 0)
            {
                // even grid
            }
            else
            {
                cols++;
            }

            _gridModel.Rows = rows;
            _gridModel.Columns = cols;
            _gridModel.RowPercents = new List<int>(rows);
            _gridModel.ColumnPercents = new List<int>(cols);
            _gridModel.CellChildMap = new int[rows, cols];

            // Note: The following are NOT equal to _multiplier divided by rows or columns and is
            // done like this to make the sum of all RowPercents exactly (_multiplier).
            for (int row = 0; row < rows; row++)
            {
                _gridModel.RowPercents.Add(((_multiplier * (row + 1)) / rows) - ((_multiplier * row) / rows));
            }

            for (int col = 0; col < cols; col++)
            {
                _gridModel.ColumnPercents.Add(((_multiplier * (col + 1)) / cols) - ((_multiplier * col) / cols));
            }

            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    _gridModel.CellChildMap[row, col] = index++;
                    if (index == ZoneCount)
                    {
                        index--;
                    }
                }
            }

            // Update the "Priority Grid" Default Layout
            if (ZoneCount <= _priorityData.Length)
            {
                _priorityGridModel.Reload(_priorityData[ZoneCount - 1]);
            }
            else
            {
                // same as grid;
                _priorityGridModel.Rows = _gridModel.Rows;
                _priorityGridModel.Columns = _gridModel.Columns;
                _priorityGridModel.RowPercents = _gridModel.RowPercents;
                _priorityGridModel.ColumnPercents = _gridModel.ColumnPercents;
                _priorityGridModel.CellChildMap = _gridModel.CellChildMap;
            }
        }

        public IList<LayoutModel> DefaultModels { get; }

        public static ObservableCollection<LayoutModel> CustomModels
        {
            get
            {
                if (_customModels == null)
                {
                    _customModels = LayoutModel.LoadCustomModels();
                    _customModels.Insert(0, _blankCustomModel);
                }

                return _customModels;
            }
        }

        private static ObservableCollection<LayoutModel> _customModels;

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        public void UpdateSelectedLayoutModel()
        {
            LayoutModel foundModel = null;
            LayoutSettings currentApplied = AppliedLayouts[CurrentDesktop];

            // reset previous selected layout
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

            // set new layout
            if (currentApplied.Type == LayoutType.Custom)
            {
                foreach (LayoutModel model in Settings.CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpper() + "}" == currentApplied.ZonesetUuid.ToUpper())
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
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = DefaultModels[0];
            }

            foundModel.IsSelected = true;
            App.Overlay.CurrentDataContext = foundModel;
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
