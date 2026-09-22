// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Explorer;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length is 1 or 2 && args[0] is "--register" or "--unregister" or "--status")
        {
            var receipt = args.Length == 2 ? ExplorerRegistration.GetResultPath(AppContext.BaseDirectory, args[1]) : null;
            var result = ChangeRegistration(args[0], out var error);
            if (receipt is not null)
            {
                using var file = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                JsonSerializer.Serialize(file, new { Operation = args[0], CorrelationId = args[1], ExitCode = result, Error = error, Broker = Environment.ProcessPath });
            }

            return result;
        }

        if (args.Length != 1 || !args[0].Equals("-Embedding", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        var unavailable = RuntimeRequirements.GetUnavailableReason();
        if (unavailable is not null)
        {
            MessageBox.Show(unavailable, "Try Run", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }

        Marshal.ThrowExceptionForHR(ShellInterop.OleInitialize(IntPtr.Zero));
        uint cookie = 0;
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            timer.Tick += (_, _) => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            var command = new ExplorerCommand(Launch, () => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background));
            var factory = new CommandFactory(command);
            var pointer = Marshal.GetComInterfaceForObject(factory, typeof(ShellInterop.IClassFactory));
            try
            {
                var classId = new Guid(ShellInterop.CommandClassId);

                // REGCLS_SINGLEUSE gives concurrent invocations independent
                // selections. Idle or abandoned activation exits after 60 s.
                Marshal.ThrowExceptionForHR(ShellInterop.CoRegisterClassObject(ref classId, pointer, 4, 0, out cookie));
            }
            finally
            {
                Marshal.Release(pointer);
            }

            timer.Start();
            Dispatcher.Run();
            timer.Stop();
            GC.KeepAlive(factory);
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Try Run Explorer entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }
        finally
        {
            if (cookie != 0)
            {
                _ = ShellInterop.CoRevokeClassObject(cookie);
            }

            ShellInterop.OleUninitialize();
        }
    }

    private static void Launch(string[] paths)
    {
        var payload = SelectionPayload.Encode(paths);
        var application = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.TryRun.exe"));
        var start = new ProcessStartInfo(application)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = Path.GetDirectoryName(application),
        };
        start.ArgumentList.Add(SelectionPayload.InputSwitch);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the Try Run window.");

        // Only the fixed protocol switch appears on the command line. File
        // paths travel through an inherited private pipe, not shell syntax or
        // a shared temporary manifest. EOF ends the bounded message.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token).WaitAsync(timeout.Token).GetAwaiter().GetResult();
        process.StandardInput.Close();
    }

    private static int ChangeRegistration(string operation, out string? error)
    {
        error = null;
        try
        {
            // No HKLM, elevation, installer, shell restart, or in-process DLL.
            // Removing this build's HKCU binding must still work when its
            // workload backend or required Windows version is unavailable.
            var unavailable = operation == "--unregister" ? null : RuntimeRequirements.GetUnavailableReason();
            if (unavailable is not null)
            {
                throw new InvalidOperationException(unavailable);
            }

            using var classes = Registry.CurrentUser.CreateSubKey("Software\\Classes");
            var broker = Environment.ProcessPath!;
            var application = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.TryRun.exe"));
            switch (operation)
            {
                case "--register":
                    ExplorerRegistration.Register(classes, application, broker);
                    break;
                case "--unregister":
                    ExplorerRegistration.Unregister(classes, broker);
                    break;
                default:
                    if (!ExplorerRegistration.IsRegistered(classes, broker))
                    {
                        throw new InvalidOperationException("This Try Run build is not registered in the desktop user's registry view.");
                    }

                    break;
            }

            if (operation != "--unregister")
            {
                VerifyActivation();
            }

            SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED, SHCNF_FLUSH
            return 0;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifyActivation()
    {
        Marshal.ThrowExceptionForHR(ShellInterop.OleInitialize(IntPtr.Zero));
        try
        {
            var classId = new Guid(ShellInterop.CommandClassId);
            var interfaceId = typeof(ShellInterop.IExecuteCommand).GUID;
            var result = ShellInterop.CoCreateInstance(ref classId, IntPtr.Zero, 4, ref interfaceId, out var instance);
            if (result < 0)
            {
                throw new InvalidOperationException($"Windows could not activate the registered Explorer helper (0x{result:X8}). Register from the desktop Explorer context.");
            }

            Marshal.Release(instance);
        }
        finally
        {
            ShellInterop.OleUninitialize();
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr first, IntPtr second);
}
