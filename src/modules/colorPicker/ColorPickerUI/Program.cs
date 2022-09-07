// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ColorPicker.Helpers;
using ColorPicker.Mouse;
using ColorPickerUI;
using Microsoft.PowerToys.Common.Utils;

namespace ColorPicker
{
    public static class Program
    {
        private static string[] _args;
        private static Logger _logger;

        [STAThread]
        public static void Main(string[] args)
        {
            _logger = new Logger("ColorPicker\\Logs");

            _args = args;
            _logger.LogInfo($"Color Picker started with pid={Environment.ProcessId}");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Unhandled exception", ex);
                CursorManager.RestoreOriginalCursors();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _logger.LogError("Unhandled exception", ex);
            }
            else
            {
                _logger.LogError("Unhandled exception");
            }

            CursorManager.RestoreOriginalCursors();
        }
    }
}
