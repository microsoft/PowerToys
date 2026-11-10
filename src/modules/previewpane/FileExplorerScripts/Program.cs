// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace FileExplorerScripts;

internal static class Program
{
    private static readonly TaskCompletionSource<App> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [STAThread]
    private static int Main(string[] arguments)
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            using var identity = WindowsIdentity.GetCurrent();
            var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            var key = "PowerToys.FileExplorerScripts." +
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToUpperInvariant())))[..24] +
                "." + identity.User!.Value + (elevated ? ".elevated" : ".user");
            if (arguments.Length == 2 && arguments[0] == "--watch")
            {
                var instance = AppInstance.FindOrRegisterForKey(key);
                if (!instance.IsCurrent)
                {
                    return 0;
                }

                instance.Activated += OnActivated;
            }
            else if (arguments.Length == 2 && arguments[0] == "--request")
            {
                var instance = AppInstance.GetInstances().FirstOrDefault(item => !item.IsCurrent && item.Key == key);
                if (instance is not null)
                {
                    Redirect(AppInstance.GetCurrent().GetActivatedEventArgs(), instance);
                    return 0;
                }
            }

            App? app = null;
            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                app = new App();
                Ready.SetResult(app);
            });
            app?.Dispose();
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or COMException or System.ComponentModel.Win32Exception)
        {
            Logger.InitializeLogger("\\File Explorer\\PowerScripts\\Logs");
            Logger.LogError("Unable to start or redirect Explorer PowerScripts.", exception);
            if (MessageBoxW(IntPtr.Zero, exception.Message, Strings.Get("Failed"), 0x10) == 0)
            {
                Logger.LogError("Unable to display the Explorer PowerScripts startup failure.");
            }

            return 1;
        }
    }

    private static void Redirect(AppActivationArguments arguments, AppInstance instance)
    {
        using var completed = new ManualResetEvent(false);
        Exception? failure = null;
        _ = Task.Run(() =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                instance.RedirectActivationToAsync(arguments).AsTask(timeout.Token).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        });
        Marshal.ThrowExceptionForHR(CoWaitForMultipleObjects(0, uint.MaxValue, 1, [completed.SafeWaitHandle.DangerousGetHandle()], out _));
        if (failure is not null)
        {
            // Delivery may have happened already. Never fall back to replaying the request.
            throw new InvalidOperationException("Unable to deliver the Explorer request to the running app.", failure);
        }
    }

    private static async void OnActivated(object? sender, AppActivationArguments arguments)
    {
        var app = await Ready.Task;
        try
        {
            if (arguments.Data is not ILaunchActivatedEventArgs launch)
            {
                throw new ArgumentException("Unsupported Explorer PowerScripts activation.");
            }

            var request = ReadRequestId(launch.Arguments);
            if (!app.UiDispatcher.TryEnqueue(async () => await app.ExecuteRequestAsync(request)))
            {
                throw new InvalidOperationException("The Explorer PowerScripts app is shutting down.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Logger.LogError("Unable to handle Explorer PowerScripts activation.", exception);
        }
    }

    private static string ReadRequestId(string commandLine)
    {
        if (commandLine.Length > 32768)
        {
            throw new ArgumentException("Explorer activation is too large.");
        }

        var buffer = CommandLineToArgvW(commandLine, out var count);
        if (buffer == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var flag = count is 2 or 3 ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, (count - 2) * IntPtr.Size)) : null;
            var id = count is 2 or 3 ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, (count - 1) * IntPtr.Size)) : null;
            if (flag != "--request" || !Guid.TryParseExact(id, "N", out _))
            {
                throw new ArgumentException("Invalid Explorer request activation.");
            }

            return id!;
        }
        finally
        {
            LocalFree(buffer);
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoWaitForMultipleObjects(uint flags, uint timeout, uint count, IntPtr[] handles, out uint index);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
