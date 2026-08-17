// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ColorPicker
{
    public static class Program
    {
        private static readonly Foundation.FatalExceptionHandler _fatalExceptionHandler =
            new(ex => Logger.LogError("Unhandled exception", ex), Mouse.CursorManager.RestoreOriginalCursors);

        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\ColorPicker\\Logs");
            Logger.LogInfo($"Color Picker started with pid={Environment.ProcessId}");

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredColorPickerEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App(args);
            });
        }

        internal static void HandleFatalException(Exception exception)
        {
            _fatalExceptionHandler.Handle(exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ??
                new InvalidOperationException("The process terminated with a non-Exception object.");
            HandleFatalException(exception);
        }
    }
}
