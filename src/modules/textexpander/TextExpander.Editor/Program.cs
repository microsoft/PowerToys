// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.TextExpander.Editor;

public static partial class Program
{
    [LibraryImport("ole32.dll")]
    private static partial int CoWaitForMultipleObjects(uint flags, uint timeout, int count, nint[] handles, out uint index);

    [STAThread]
    public static void Main()
    {
        Logger.InitializeLogger("\\TextExpander\\Editor\\Logs");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (GPOWrapper.GetConfiguredTextExpanderEnabledValue() == GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Text Expander editor launch blocked by group policy.");
            return;
        }

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey("PowerToys_TextExpander_Editor");
        if (!instance.IsCurrent)
        {
            RedirectActivation(instance, activation);
            return;
        }

        App app = null;
        instance.Activated += (_, _) => app?.ActivateEditor();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            app = new App();
        });
    }

    private static void RedirectActivation(AppInstance instance, AppActivationArguments activation)
    {
        using var completed = new ManualResetEvent(false);
        Task redirect = Task.Run(async () =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await instance.RedirectActivationToAsync(activation).AsTask(timeout.Token);
            }
            finally
            {
                completed.Set();
            }
        });

        // Pump COM while the original instance completes the activation handshake.
        nint[] handles = { completed.SafeWaitHandle.DangerousGetHandle() };
        Marshal.ThrowExceptionForHR(CoWaitForMultipleObjects(0, uint.MaxValue, 1, handles, out _));
        try
        {
            redirect.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is OperationCanceledException or COMException)
        {
            Logger.LogError("Could not activate the existing Text Expander editor.", ex);
        }
    }
}
