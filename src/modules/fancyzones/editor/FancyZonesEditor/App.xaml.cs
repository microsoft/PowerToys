// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Common.UI;
using FancyZonesEditor.Logs;
using FancyZonesEditor.Utils;
using ManagedCommon;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        // Non-localizable strings
        private const string PowerToysIssuesURL = "https://aka.ms/powerToysReportBug";
        private const string ParsingErrorReportTag = "Settings parsing error";
        private const string ParsingErrorDataTag = "Data: ";

        public MainWindowSettingsModel MainWindowSettings { get; }

        public static FancyZonesEditorIO FancyZonesEditorIO { get; private set; }

        public static Overlay Overlay { get; private set; }

        public static int PowerToysPID { get; set; }

        private ThemeManager _themeManager;

        private EventWaitHandle _eventHandle;

        private Thread _exitWaitThread;

        public static bool DebugMode
        {
            get
            {
                return _debugMode;
            }
        }

        private static bool _debugMode;
        private bool _isDisposed;

        [Conditional("DEBUG")]
        private void DebugModeCheck()
        {
            _debugMode = true;
        }

        public App()
        {
            // DebugModeCheck();
            FancyZonesEditorIO = new FancyZonesEditorIO();
            Overlay = new Overlay();
            MainWindowSettings = new MainWindowSettingsModel();

            _exitWaitThread = new Thread(App_WaitExit);
            _exitWaitThread.Start();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
            {
                Logger.LogInfo("Runner exited");
                Environment.Exit(0);
            });

            _themeManager = new ThemeManager(this);

            var parseResult = FancyZonesEditorIO.ParseParams();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            parseResult = FancyZonesEditorIO.ParseAppliedLayouts();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            parseResult = FancyZonesEditorIO.ParseCustomLayouts();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            parseResult = FancyZonesEditorIO.ParseLayoutHotkeys();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            parseResult = FancyZonesEditorIO.ParseLayoutTemplates();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            MainWindowSettingsModel settings = ((App)Current).MainWindowSettings;
            settings.UpdateSelectedLayoutModel();

            Overlay.Show();
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Dispose();

            if (_eventHandle != null)
            {
                _eventHandle.Set();
            }

            _exitWaitThread.Join();

            Logger.LogInfo("FancyZones Editor exited");
        }

        private void App_WaitExit()
        {
            _eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, interop.Constants.FZEExitEvent());
            if (_eventHandle.WaitOne())
            {
                Logger.LogInfo("Exit event triggered");
                Environment.Exit(0);
            }
        }

        public void App_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift)
            {
                MainWindowSettings.IsShiftKeyPressed = false;
            }
        }

        public void App_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift)
            {
                MainWindowSettings.IsShiftKeyPressed = true;
            }
            else if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                e.Handled = true;
                App.Overlay.FocusEditor();
            }
        }

        public static void ShowExceptionMessageBox(string message, Exception exception = null)
        {
            string fullMessage = FancyZonesEditor.Properties.Resources.Error_Report + PowerToysIssuesURL + " \n" + message;
            if (exception != null)
            {
                fullMessage += ": " + exception.Message;
            }

            MessageBox.Show(fullMessage, FancyZonesEditor.Properties.Resources.Error_Exception_Message_Box_Title);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Logger.LogError("Unhandled exception", (Exception)args.ExceptionObject);
            ShowReportMessageBox();
        }

        private static void ShowReportMessageBox()
        {
            MessageBox.Show(
                FancyZonesEditor.Properties.Resources.Crash_Report_Message_Box_Text +
                PowerToysIssuesURL,
                FancyZonesEditor.Properties.Resources.Fancy_Zones_Editor_App_Title);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _themeManager?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
                Logger.LogInfo("FancyZones Editor disposed");
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
