// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace SoundManager
{
    public static class Program
    {
        // The copy that is already running has this long to take the activation over.
        private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(32);

        // The extra time this copy waits before it gives up on the other one.
        private static readonly TimeSpan RedirectGrace = TimeSpan.FromSeconds(2);

        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\Sound Manager\\Logs");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_SoundManager_Instance");
            if (!instanceKey.IsCurrent)
            {
                Logger.LogWarning("Another instance of Sound Manager is already running.");

                // The window that is already running takes over, so the user does get a window even when this copy was started to replace one that had none.
                RedirectActivation(instanceKey);
                return;
            }

            // The activation is caught before the window is made, so that a second copy that starts while this one is still coming up is not lost.
            instanceKey.Activated += OnActivated;

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }

        private static void OnActivated(object sender, AppActivationArguments args)
        {
            // The window is made when the app starts, so an activation that comes in before that does not have to be kept: the window comes up anyway.
            if (App.Current is App app)
            {
                app.ActivateWindow();
            }
        }

        // The activation is handed over on another thread with a time limit, so that a copy that does not answer does not leave this one hanging for good.
        private static void RedirectActivation(AppInstance keyInstance)
        {
            using var redirectSemaphore = new Semaphore(0, 1);

            _ = Task.Run(() =>
            {
                using var cts = new CancellationTokenSource(RedirectTimeout);
                try
                {
                    keyInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs())
                        .AsTask(cts.Token)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException)
                {
                    Logger.LogError($"Failed to activate the Sound Manager that is already running; timed out after {RedirectTimeout}.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to activate the Sound Manager that is already running", ex);
                }
                finally
                {
                    redirectSemaphore.Release();
                }
            });

            redirectSemaphore.WaitOne(RedirectTimeout + RedirectGrace);
        }
    }
}
