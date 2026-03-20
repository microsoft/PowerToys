// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace ImageResizer
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                Logger.InitializeLogger("\\Image Resizer\\Logs");
            }
            catch
            {
            }

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError("CultureNotFoundException: " + ex.Message);
            }

            Console.InputEncoding = Encoding.Unicode;

            ComWrappersSupport.InitializeComWrappers();
            App.SetArgs(args);

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });

            return 0;
        }
    }
}
