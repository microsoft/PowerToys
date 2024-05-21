// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using ProjectsEditor.Common;
using ProjectsEditor.Utils;
using ProjectsEditor.ViewModels;

namespace ProjectsEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        public static ProjectsEditorIO ProjectsEditorIO { get; private set; }

        private MainWindow _mainWindow;

        private MainViewModel _mainViewModel;

        private ThemeManager _themeManager;

        private bool _isDisposed;

        public App()
        {
            ProjectsEditorIO = new ProjectsEditorIO();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            _themeManager = new ThemeManager(this);

            if (_mainViewModel == null)
            {
                _mainViewModel = new MainViewModel(ProjectsEditorIO);
            }

            var parseResult = ProjectsEditorIO.ParseProjects(_mainViewModel);

            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                _mainViewModel.LaunchProject(args[1]);
                return;
            }

            // normal start of editor
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_mainViewModel);
            }

            // reset main window owner to keep it on the top
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();

            // we can reset topmost flag after it's opened
            _mainWindow.Topmost = false;
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Dispose();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            // TODO: log the error and show an error message
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _themeManager?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
