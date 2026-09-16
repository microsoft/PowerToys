// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel.AppExtensions;
using WinRT;

namespace PowerToys.TryRun.CmdPal;

internal static partial class RegistrationProbe
{
    public static int Run(string correlation, bool launch = false)
    {
        if (!Guid.TryParseExact(correlation, "N", out _))
        {
            return 2;
        }

        var pointer = IntPtr.Zero;
        var initialized = false;
        string? error = null;
        string[] commands = [];
        try
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 0));
            initialized = true;
            var installed = AppExtensionCatalog.Open("com.microsoft.commandpalette").FindAllAsync().AsTask().GetAwaiter().GetResult();
            if (!installed.Any(item => item.Package.Id.Name == "PowerToys.TryRun.CmdPal.Dev" && item.Id == "TryRun"))
            {
                throw new InvalidOperationException("Try Run was not found in the Command Palette extension catalog.");
            }

            var classId = typeof(TryRunExtension).GUID;
            var interfaceId = typeof(IExtension).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref classId, IntPtr.Zero, 4, ref interfaceId, out pointer));
            var extension = MarshalInterface<IExtension>.FromAbi(pointer);
            var provider = extension.GetProvider(ProviderType.Commands) as ICommandProvider ?? throw new InvalidOperationException("The extension did not provide CmdPal commands.");
            commands = provider.TopLevelCommands().Select(command => command.Title).ToArray();
            if (!commands.Contains("Try Run", StringComparer.Ordinal) || !commands.Contains("Try Run a file", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("The registered extension returned an unexpected command set.");
            }

            if (launch)
            {
                var command = provider.TopLevelCommands()[0].Command as IInvokableCommand ?? throw new InvalidOperationException("The Try Run command cannot be invoked.");
                var result = command.Invoke(null!);
                if (result.Kind != CommandResultKind.Dismiss)
                {
                    throw new InvalidOperationException("The Try Run command could not open its configuration window.");
                }
            }

            GC.KeepAlive(extension);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }

            if (initialized)
            {
                CoUninitialize();
            }
        }

        using var receipt = new FileStream(Path.Combine(AppContext.BaseDirectory, $"CmdPal-probe-{correlation}.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(receipt, new { CorrelationId = correlation, Error = error, Commands = commands, Launched = launch && error is null });
        return error is null ? 0 : 1;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr reserved, uint mode);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(ref Guid classId, IntPtr outer, uint context, ref Guid interfaceId, out IntPtr instance);
}
