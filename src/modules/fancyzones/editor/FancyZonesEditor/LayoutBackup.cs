// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class LayoutBackup
    {
        private LayoutModel _backup;
        private List<LayoutModel> _defaultLayoutsBackup;

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

            _defaultLayoutsBackup = new List<LayoutModel>(MainWindowSettingsModel.DefaultLayouts.Layouts);
        }

        public void Restore()
        {
            if (_backup != null)
            {
                var settings = ((App)Application.Current).MainWindowSettings;
                var selectedModel = settings.SelectedModel;

                if (selectedModel == null)
                {
                    return;
                }

                if (_backup is GridLayoutModel grid)
                {
                    grid.RestoreTo((GridLayoutModel)selectedModel);
                    grid.InitTemplateZones();
                }
                else if (_backup is CanvasLayoutModel canvas)
                {
                    canvas.RestoreTo((CanvasLayoutModel)selectedModel);
                }
            }

            if (_defaultLayoutsBackup != null)
            {
                MainWindowSettingsModel.DefaultLayouts.Restore(_defaultLayoutsBackup);
            }
        }

        public void Clear()
        {
            _backup = null;
            _defaultLayoutsBackup = null;
        }
    }
}
