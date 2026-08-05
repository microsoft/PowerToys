// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor
{
    public class LayoutBackup : IDisposable
    {
        private LayoutModel _backup;
        private string _hotkeyBackup;
        private Dictionary<MonitorConfigurationType, LayoutModel> _defaultLayoutsBackup;

        public LayoutBackup()
        {
        }

        public bool Matches(LayoutModel model)
        {
            return _backup != null &&
                   model != null &&
                   _backup.GetType() == model.GetType() &&
                   _backup.Uuid == model.Uuid;
        }

        public void Backup(LayoutModel model)
        {
            _backup?.Dispose();

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
                if (_backup is GridLayoutModel grid && layoutToRestore is GridLayoutModel targetGrid)
                {
                    grid.RestoreTo(targetGrid);
                    grid.InitTemplateZones();
                }
                else if (_backup is CanvasLayoutModel canvas && layoutToRestore is CanvasLayoutModel targetCanvas)
                {
                    canvas.RestoreTo(targetCanvas);
                    canvas.InitTemplateZones();
                }

                layoutToRestore.Name = _backup.Name;
                layoutToRestore.QuickKey = _backup.QuickKey;
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
            _backup?.Dispose();
            _backup = null;
            _hotkeyBackup = null;
            _defaultLayoutsBackup = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Clear();
            GC.SuppressFinalize(this);
        }
    }
}
