// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TopToolbar.Logging;

namespace TopToolbar
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                AppLogger.Initialize(AppPaths.Logs);
                EnsureAppDirectories();
                AppLogger.LogInfo($"Logger initialized. Logs directory: {AppPaths.Logs}");
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    try
                    {
                        var message = $"AppDomain unhandled exception (IsTerminating={e.IsTerminating})";
                        if (e.ExceptionObject is Exception exception)
                        {
                            AppLogger.LogError(message, exception);
                        }
                        else
                        {
                            AppLogger.LogError($"{message} - {e.ExceptionObject}");
                        }
                    }
                    catch
                    {
                    }
                };
                };
                TaskScheduler.UnobservedTaskException += (_, e) =>
                {
                    try
                    {
                        AppLogger.LogError("Unobserved task exception", e.Exception);
                        e.SetObserved();
                    }
                    catch
                    {
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppLogger init failed: {ex.Message}");
            }

            Application.Start(args =>
            {
                _ = new App();
            });
        }

        private static void EnsureAppDirectories()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Root);
                Directory.CreateDirectory(AppPaths.IconsDirectory);
                Directory.CreateDirectory(AppPaths.ProfilesDirectory);
                Directory.CreateDirectory(AppPaths.ProvidersDirectory);
                Directory.CreateDirectory(AppPaths.ConfigDirectory);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to ensure data directories", ex);
            }
        }
    }
}
