// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Common.UI;
using FancyZoneEditor.Telemetry;
using FancyZonesEditor.Utils;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Win32;

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

        private ResourceDictionary _currentThemeDictionary;

        public static bool DebugMode
        {
            get
            {
                return _debugMode;
            }
        }

        private static bool _debugMode;
        private bool _isDisposed;

        private CancellationTokenSource NativeThreadCTS { get; set; }

        [Conditional("DEBUG")]
        private void DebugModeCheck()
        {
            _debugMode = true;
        }

        public App()
        {
            PowerToysTelemetry.Log.WriteEvent(new FancyZonesEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            var languageTag = LanguageHelper.LoadLanguage();

            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageTag);
                }
                catch (CultureNotFoundException ex)
                {
                    Logger.LogError("CultureNotFoundException: " + ex.Message);
                }
            }

            Logger.InitializeLogger("\\FancyZones\\Editor\\Logs");

            // DebugModeCheck();
            NativeThreadCTS = new CancellationTokenSource();
            FancyZonesEditorIO = new FancyZonesEditorIO();
            Overlay = new Overlay();
            MainWindowSettings = new MainWindowSettingsModel();

            App_WaitExit();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredFancyZonesEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Shutdown(0);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            _currentThemeDictionary = null;
            ApplyTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
            {
                Logger.LogInfo("Runner exited");
                NativeThreadCTS.Cancel();
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            });

            var parseResult = FancyZonesEditorIO.ParseParams();

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

            parseResult = FancyZonesEditorIO.ParseCustomLayouts();
            if (!parseResult.Result)
            {
                Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
                MessageBox.Show(parseResult.Message, FancyZonesEditor.Properties.Resources.Error_Parsing_Data_Title, MessageBoxButton.OK);
            }

            parseResult = FancyZonesEditorIO.ParseDefaultLayouts();
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

            parseResult = FancyZonesEditorIO.ParseAppliedLayouts();
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
            NativeThreadCTS.Cancel();
            Dispose();

            Logger.LogInfo("FancyZones Editor exited");
        }

        private void App_WaitExit()
        {
            NativeEventWaiter.WaitForEventLoop(
            PowerToys.Interop.Constants.FZEExitEvent(),
            () =>
            {
                Logger.LogInfo("Exit event triggered");
                Application.Current.Shutdown();
            },
            Current.Dispatcher,
            NativeThreadCTS.Token);
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

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
            {
                Current.Dispatcher.Invoke(ApplyTheme);
            }
        }

        private void ApplyTheme()
        {
            string themeSource;

            if (SystemParameters.HighContrast)
            {
                // Determine which high contrast theme based on the window background
                var bgColor = SystemColors.WindowColor;
                if (bgColor.R < 128 && bgColor.G < 128 && bgColor.B < 128)
                {
                    // Dark high-contrast backgrounds
                    themeSource = "pack://application:,,,/Themes/HighContrast1.xaml";
                }
                else
                {
                    // Light high-contrast backgrounds
                    themeSource = "pack://application:,,,/Themes/HighContrastWhite.xaml";
                }
            }
            else
            {
                bool isLight = IsLightTheme();
                themeSource = isLight
                    ? "pack://application:,,,/Themes/Light.xaml"
                    : "pack://application:,,,/Themes/Dark.xaml";
            }

            var newDict = new ResourceDictionary { Source = new Uri(themeSource) };

            if (_currentThemeDictionary != null)
            {
                Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            }

            Resources.MergedDictionaries.Add(newDict);
            _currentThemeDictionary = newDict;
        }

        private static bool IsLightTheme()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue != 0;
                }
            }
            catch
            {
                // Default to dark if unable to read
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                }

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
