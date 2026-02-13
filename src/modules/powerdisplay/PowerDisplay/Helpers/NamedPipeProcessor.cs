// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Helpers;

/// <summary>
/// Processes messages from the Module DLL via Named Pipe.
/// Based on AdvancedPaste NamedPipeProcessor pattern.
/// </summary>
public static class NamedPipeProcessor
{
    /// <summary>
    /// Connects to a named pipe and processes incoming messages.
    /// This method runs continuously until cancelled or the pipe is disconnected.
    /// </summary>
    /// <param name="pipeName">The name of the pipe to connect to.</param>
    /// <param name="connectTimeout">Timeout for initial connection.</param>
    /// <param name="messageHandler">Handler for each received message.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public static async Task ProcessNamedPipeAsync(
        string pipeName,
        TimeSpan connectTimeout,
        Action<string> messageHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            using NamedPipeClientStream pipeClient = new(".", pipeName, PipeDirection.In);

            Logger.LogInfo($"[NamedPipe] Connecting to pipe: {pipeName}");
            await pipeClient.ConnectAsync(connectTimeout, cancellationToken);
            Logger.LogInfo($"[NamedPipe] Connected to pipe: {pipeName}");

            using StreamReader streamReader = new(pipeClient, Encoding.Unicode);

            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await streamReader.ReadLineAsync(cancellationToken);

                if (message != null)
                {
                    Logger.LogInfo($"[NamedPipe] Received message: {message}");
                    messageHandler(message);
                }

                // Small delay to prevent tight loop
                var intraMessageDelay = TimeSpan.FromMilliseconds(10);
                await Task.Delay(intraMessageDelay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("[NamedPipe] Processing cancelled");
        }
        catch (IOException ex)
        {
            // Pipe disconnected, this is expected when the module DLL terminates
            Logger.LogInfo($"[NamedPipe] Pipe disconnected: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[NamedPipe] Error processing pipe: {ex.Message}");
        }
    }
}
