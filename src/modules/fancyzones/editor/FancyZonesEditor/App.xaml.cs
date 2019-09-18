using FancyZonesEditor;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Settings ZoneSettings { get { return _settings; } }
        private Settings _settings;
        private ushort _idInitial = 0;
        public App()
        {
            _settings = new Settings();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 1)
            {
                UInt16.TryParse(e.Args[1], out _idInitial);
            }

            LayoutModel foundModel = null;

            if (_idInitial != 0)
            {
                foreach (LayoutModel model in _settings.DefaultModels)
                {
                    if (model.Id == _idInitial)
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }

                if (foundModel == null)
                {
                    foreach (LayoutModel model in _settings.CustomModels)
                    {
                        if (model.Id == _idInitial)
                        {
                            // found match
                            foundModel = model;
                            break;
                        }
                    }
                }
            }
            if (foundModel == null)
            {
                foundModel = _settings.DefaultModels[0];
            }

            foundModel.IsSelected = true;

            EditorOverlay overlay = new EditorOverlay();
            overlay.Show();
            overlay.DataContext = foundModel;
        }
    }
}
