// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

using FancyZoneEditor.Telemetry;
using FancyZonesEditor.Helpers;
using FancyZonesEditor.Utils;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

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

        private static bool _debugMode;

        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

        private bool _isDisposed;
        private bool _settingsPersisted;

        public App()
        {
            InitializeComponent();

            // WinUI 3 has no Application.Current.Dispatcher, so the UI DispatcherQueue is
            // captured explicitly while we are still on the UI thread.
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            PowerToysTelemetry.Log.WriteEvent(new FancyZonesEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            var languageTag = LanguageHelper.LoadLanguage();

            if (!string.IsNullOrEmpty(languageTag))
            {
                // The .resw strings are resolved by MRT, which follows the app's primary language
                // override rather than the thread's UI culture - the culture alone (all the WPF
                // editor needed) would leave the whole editor in the system language.
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageTag;

                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageTag);
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

        public static FancyZonesEditorIO FancyZonesEditorIO { get; private set; }

        public static Overlay Overlay { get; private set; }

        public static int PowerToysPID { get; set; }

        public static bool DebugMode
        {
            get
            {
                return _debugMode;
            }
        }

        public MainWindowSettingsModel MainWindowSettings { get; }

        private CancellationTokenSource NativeThreadCTS { get; set; }

        public static void ShowExceptionMessageBox(string message, Exception exception = null)
        {
            string fullMessage = ResourceLoaderInstance.GetString("Error_Report") + PowerToysIssuesURL + " \n" + message;
            if (exception != null)
            {
                fullMessage += ": " + exception.Message;
            }

            ShowMessageDialog(fullMessage, ResourceLoaderInstance.GetString("Error_Exception_Message_Box_Title"));
        }

        public void App_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift || e.Key == VirtualKey.LeftShift || e.Key == VirtualKey.RightShift)
            {
                MainWindowSettings.IsShiftKeyPressed = false;
            }
        }

        public void App_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift || e.Key == VirtualKey.LeftShift || e.Key == VirtualKey.RightShift)
            {
                MainWindowSettings.IsShiftKeyPressed = true;
            }
            else if (e.Key == VirtualKey.Tab &&
                     InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                e.Handled = true;
                Overlay.FocusEditor();
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredFancyZonesEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
            {
                Logger.LogInfo("Runner exited");
                _dispatcherQueue.TryEnqueue(Shutdown);
            });

            var parsingErrors = ParseSettings();

            MainWindowSettings.UpdateSelectedLayoutModel();

            Overlay.Show();

            // The dialogs can only be hosted once an overlay window exists, so parsing errors are
            // collected during startup and reported here. WPF could show a MessageBox with no
            // window at all.
            foreach (string error in parsingErrors)
            {
                ShowMessageDialog(error, ResourceLoaderInstance.GetString("Error_Parsing_Data_Title"));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Logger.LogInfo("FancyZones Editor disposed");
            }
        }

        /// <summary>
        /// WinUI 3 has no MessageBox; errors surface through a ContentDialog hosted by whichever
        /// overlay window is currently up. Falls back to the log when no XamlRoot exists yet.
        /// </summary>
        /// <param name="message">Body of the message.</param>
        /// <param name="title">Title of the dialog.</param>
        private static async void ShowMessageDialog(string message, string title)
        {
            var xamlRoot = Overlay?.CurrentLayoutWindow?.Content?.XamlRoot;
            if (xamlRoot == null)
            {
                Logger.LogError(title + ": " + message);
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = ResourceLoaderInstance.GetString("Close"),
            };

            await dialog.ShowAsync();
        }

        [Conditional("DEBUG")]
        private static void DebugModeCheck()
        {
            _debugMode = true;
        }

        private static void ReportParsingError(ParsingResult parseResult, List<string> errors)
        {
            if (parseResult.Result)
            {
                return;
            }

            Logger.LogError(ParsingErrorReportTag + ": " + parseResult.Message + "; " + ParsingErrorDataTag + ": " + parseResult.MalformedData);
            errors.Add(parseResult.Message);
        }

        private static void ShowReportMessageBox()
        {
            ShowMessageDialog(
                ResourceLoaderInstance.GetString("Crash_Report_Message_Box_Text") + PowerToysIssuesURL,
                ResourceLoaderInstance.GetString("Fancy_Zones_Editor_App_Title"));
        }

        private static List<string> ParseSettings()
        {
            var errors = new List<string>();

            ReportParsingError(FancyZonesEditorIO.ParseParams(), errors);
            ReportParsingError(FancyZonesEditorIO.ParseLayoutTemplates(), errors);
            ReportParsingError(FancyZonesEditorIO.ParseCustomLayouts(), errors);
            ReportParsingError(FancyZonesEditorIO.ParseDefaultLayouts(), errors);
            ReportParsingError(FancyZonesEditorIO.ParseLayoutHotkeys(), errors);
            ReportParsingError(FancyZonesEditorIO.ParseAppliedLayouts(), errors);

            return errors;
        }

        private void App_WaitExit()
        {
            NativeEventWaiter.WaitForEventLoop(
                PowerToys.Interop.Constants.FZEExitEvent(),
                () =>
                {
                    Logger.LogInfo("Exit event triggered");
                    Shutdown();
                },
                _dispatcherQueue,
                NativeThreadCTS.Token);
        }

        /// <summary>
        /// Writes every FancyZones settings file back to disk and tears the overlays down.
        /// WPF ran this from <c>MainWindow.Closing</c>, which <c>Application.Shutdown()</c>
        /// reached on its way out; WinUI's <c>Application.Exit()</c> does not close windows the
        /// same way, so every exit path calls this explicitly. It is idempotent because both the
        /// window-close path and the shutdown path can reach it.
        /// </summary>
        public void PersistSettings()
        {
            if (_settingsPersisted)
            {
                return;
            }

            _settingsPersisted = true;

            FancyZonesEditorIO.SerializeAppliedLayouts();
            FancyZonesEditorIO.SerializeCustomLayouts();
            FancyZonesEditorIO.SerializeLayoutHotkeys();
            FancyZonesEditorIO.SerializeLayoutTemplates();
            FancyZonesEditorIO.SerializeDefaultLayouts();
            Overlay.CloseLayoutWindow();
        }

        /// <summary>
        /// Persists pending edits, cancels the native waiter thread, disposes the app and exits.
        /// Replaces <c>Application.Shutdown()</c> plus the WPF <c>Exit</c> event handler.
        /// </summary>
        public void Shutdown()
        {
            PersistSettings();

            NativeThreadCTS.Cancel();
            Dispose();

            Logger.LogInfo("FancyZones Editor exited");
            Exit();
        }

        private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
        {
            Logger.LogError("Unhandled exception", (Exception)args.ExceptionObject);

            // The exception can surface on any thread, and ContentDialog must be built on the UI
            // thread, so the report is marshalled back.
            _dispatcherQueue.TryEnqueue(ShowReportMessageBox);
        }
    }
}
