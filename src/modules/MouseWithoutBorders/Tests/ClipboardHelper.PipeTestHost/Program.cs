// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Principal;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using StreamJsonRpc;

namespace MouseWithoutBorders.PipeTestHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            if (args.Length < 2 || !args[1].StartsWith("PowerToys.ClipboardFixture.", StringComparison.Ordinal))
            {
                throw new ArgumentException("An isolated fixture pipe name is required.");
            }

            if (args[0] == "server")
            {
                using var identity = WindowsIdentity.GetCurrent();
                using var process = Process.GetCurrentProcess();
                using var stream = RestrictedNamedPipeServer.Create(args[1], identity.User!);
                Console.WriteLine("ready");
                await stream.WaitForConnectionAsync(timeout.Token);
                var user = args.Length > 2 ? new SecurityIdentifier(args[2]) : identity.User!;
                int session = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : process.SessionId;
                string rejection = ClipboardHelperIpc.VerifyClient(stream, user, session);
                if (rejection.Length != 0)
                {
                    Console.WriteLine($"rejected:{rejection};calls:0");
                    return 3;
                }

                var target = new FixtureTarget();
                using var rpc = JsonRpc.Attach(stream, target);
                await Task.WhenAny(target.Completed.Task, rpc.Completion).WaitAsync(timeout.Token);
                await rpc.Completion.WaitAsync(timeout.Token);
                Console.WriteLine($"accepted;calls:{target.Calls}");
                return 0;
            }

            if (args[0] == "capture")
            {
                using var identity = WindowsIdentity.GetCurrent();
                using var stream = RestrictedNamedPipeServer.Create(args[1], identity.User!);
                Console.WriteLine("ready");
                await stream.WaitForConnectionAsync(timeout.Token);
                var buffer = new byte[1];
                int bytes = await stream.ReadAsync(buffer, timeout.Token);
                Console.WriteLine($"bytes:{bytes}");
                return bytes == 0 ? 0 : 2;
            }

            if (args[0] == "client")
            {
                Console.WriteLine("connecting");
                using var stream = ClipboardHelperIpc.Connect(args[1]);
                using var rpc = JsonRpc.Attach(stream);
                string result = await rpc.InvokeAsync<string>("Echo", "synthetic-handoff").WaitAsync(timeout.Token);
                Console.WriteLine(result);
                return result == "synthetic-handoff" ? 0 : 2;
            }

            throw new ArgumentException("Unknown fixture role.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or OperationCanceledException or ConnectionLostException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"failed:{exception.GetType().Name}:{exception.Message}");
            return 4;
        }
    }

    public sealed class FixtureTarget
    {
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Calls { get; private set; }

        public string Echo(string value)
        {
            Calls++;
            Completed.SetResult();
            return value;
        }
    }
}
