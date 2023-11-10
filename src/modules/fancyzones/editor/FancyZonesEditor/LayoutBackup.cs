// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor
{
    public class LayoutBackup
    {
        private LayoutModel _backup;
        private string _hotkeyBackup;
        private Dictionary<MonitorConfigurationType, LayoutModel> _defaultLayoutsBackup;

        public LayoutBackup()
        {
        }

        public void Backup(LayoutModel model)
        {
            if (model is GridLayoutModel grid)
            {
                _backup = new GridLayoutModel(grid);
            }
            else if (model is CanvasLayoutModel canvas)
            {
                _backup = new CanvasLayoutModel(canvas);
            }

            _hotkeyBackup = MainWindowSettingsModel.LayoutHotkeys.Key(model.Uuid);
            _defaultLayoutsBackup = new Dictionary<MonitorConfigurationType, LayoutModel>(MainWindowSettingsModel.DefaultLayouts.Layouts);
        }

        public void Restore(LayoutModel layoutToRestore)
        {
            if (_backup != null && layoutToRestore != null)
            {
                if (_backup is GridLayoutModel grid)
                {
                    grid.RestoreTo((GridLayoutModel)layoutToRestore);
                    grid.InitTemplateZones();
                }
                else if (_backup is CanvasLayoutModel canvas)
                {
                    canvas.RestoreTo((CanvasLayoutModel)layoutToRestore);
                    canvas.InitTemplateZones();
                }
            }

            if (_hotkeyBackup != null)
            {
                MainWindowSettingsModel.LayoutHotkeys.SelectKey(_hotkeyBackup, layoutToRestore.Uuid);
            }

            if (_defaultLayoutsBackup != null)
            {
                MainWindowSettingsModel.DefaultLayouts.Restore(_defaultLayoutsBackup);
            }
        }

        public void Clear()
        {
            _backup = null;
            _hotkeyBackup = null;
            _defaultLayoutsBackup = null;
        }
    }
}
