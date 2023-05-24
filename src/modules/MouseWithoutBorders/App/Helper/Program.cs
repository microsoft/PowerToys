// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ManagedCommon;

namespace MouseWithoutBorders
{
    internal static class Program
    {
        internal static FormHelper FormHelper;

        private static FormDot dotForm;

        internal static FormDot DotForm
        {
            get
            {
                return dotForm != null && !dotForm.IsDisposed ? dotForm : (dotForm = new FormDot());
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // TODO: Add logging.
                // Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            RunnerHelper.WaitForPowerToysRunnerExitFallback(() =>
            {
                Application.Exit();
            });

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
            {
                string command = args[1];
                string arg = args.Length > 2 && !string.IsNullOrEmpty(args[2]) ? args[2] : string.Empty;

                if (command.Equals("SvcExec", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(Path.GetDirectoryName(Application.ExecutablePath) + "\\MouseWithoutBorders.exe", "\"" + arg + "\"");
                }
                else if (command.Equals("install", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(Path.GetDirectoryName(Application.ExecutablePath) + "\\MouseWithoutBorders.exe");
                }
                else if (command.Equals("help-ex", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(@"http://www.aka.ms/mm");
                }
                else if (command.Equals("InternalError", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(arg, Application.ProductName);
                }

                return;
            }

            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);

            dotForm = new FormDot();
            Application.Run(FormHelper = new FormHelper());
        }
    }
}
