// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows.Threading;

using Common.UI;
using Microsoft.PowerToys.Telemetry;
using PowerToys.Interop;

namespace Microsoft.PowerToys.PreviewHandler.Bgcode
{
    internal static class Program
    {
        private static CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private static BgcodePreviewHandlerControl _previewHandlerControl;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            if (args != null)
            {
                if (args.Length == 6)
                {
                    ETWTrace etwTrace = new ETWTrace(Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "AppData", "LocalLow", "Microsoft", "PowerToys", "etw"));

                    string filePath = args[0];
                    IntPtr hwnd = IntPtr.Parse(args[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                    int left = Convert.ToInt32(args[2], 10);
                    int right = Convert.ToInt32(args[3], 10);
                    int top = Convert.ToInt32(args[4], 10);
                    int bottom = Convert.ToInt32(args[5], 10);
                    Rectangle s = new Rectangle(left, top, right - left, bottom - top);

                    _previewHandlerControl = new BgcodePreviewHandlerControl();

                    if (!_previewHandlerControl.SetWindow(hwnd, s))
                    {
                        return;
                    }

                    _previewHandlerControl.DoPreview(filePath);

                    NativeEventWaiter.WaitForEventLoop(
                        Constants.BgcodePreviewResizeEvent(),
                        () =>
                        {
                            Rectangle s = default;
                            if (!_previewHandlerControl.SetRect(s))
                            {
                                etwTrace?.Dispose();

                                // When the parent HWND became invalid, the application won't respond to Application.Exit().
                                Environment.Exit(0);
                            }
                        },
                        Dispatcher.CurrentDispatcher,
                        _tokenSource.Token);

                    etwTrace?.Dispose();
                }
                else
                {
                    MessageBox.Show("Wrong number of args: " + args.Length.ToString(CultureInfo.InvariantCulture));
                }
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.Run();
        }
    }
}
