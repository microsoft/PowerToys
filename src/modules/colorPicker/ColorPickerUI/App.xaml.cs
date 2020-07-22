using System;
using System.Threading;
using System.Windows;
using ColorPicker.Helpers;
using ColorPicker.Mouse;
using ManagedCommon;

namespace ColorPickerUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex _instanceMutex = null;
        private static string[] _args;
        private int _powerToysPid;

        [STAThread]
        public static void Main(string[] args)
        {
            _args = args;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
            catch (Exception ex)
            {
                Logger.LogError("Unhandled exception", ex);
                CursorManager.RestoreOriginalCursors();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // allow only one instance of color picker
            bool createdNew;
            _instanceMutex = new Mutex(true, @"Global\ColorPicker", out createdNew);
            if (!createdNew)
            {
                _instanceMutex = null;
                Application.Current.Shutdown();
                return;
            }

            if(_args.Length > 0)
            {
                _ = int.TryParse(_args[0], out _powerToysPid);
            }

            RunnerHelper.WaitForPowerToysRunner(_powerToysPid, () => {
                Environment.Exit(0);
            });

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
                _instanceMutex.ReleaseMutex();

            CursorManager.RestoreOriginalCursors();
            base.OnExit(e);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", (e.ExceptionObject is Exception) ? (e.ExceptionObject as Exception) : new Exception());
            CursorManager.RestoreOriginalCursors();
        }
    }
}
