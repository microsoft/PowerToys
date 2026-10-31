// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PowerToys.Interop;

namespace AdvancedPaste.Helpers;

public static class NamedPipeProcessor
{
    public static async Task ProcessNamedPipeAsync(string pipeName, TimeSpan connectTimeout, Action<string> messageHandler, CancellationToken cancellationToken)
    {
        using NamedPipeClientStream pipeClient = new(".", pipeName, PipeDirection.In);

        await pipeClient.ConnectAsync(connectTimeout, cancellationToken);

        var installationDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        if (!CommonManaged.AuthenticateNamedPipeServer(
                unchecked((ulong)pipeClient.SafePipeHandle.DangerousGetHandle().ToInt64()),
                "PowerToys.exe",
                installationDirectory,
                Environment.ProcessPath ?? string.Empty,
                NamedPipePeerValidation.PowerToysPeer))
        {
            throw new UnauthorizedAccessException("The named pipe server is not a trusted PowerToys process.");
        }

        using StreamReader streamReader = new(pipeClient, Encoding.Unicode);

        while (true)
        {
            var message = await streamReader.ReadLineAsync(cancellationToken);

            if (message != null)
            {
                messageHandler(message);
            }

            var intraMessageDelay = TimeSpan.FromMilliseconds(10);
            await Task.Delay(intraMessageDelay, cancellationToken);
        }
    }
}
