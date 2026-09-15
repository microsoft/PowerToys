// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Protocol;

namespace LightSwitch.Cli.Ipc;

internal sealed class CliPipeClient
{
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    private readonly string _pipeName;
    private readonly Func<PipeStream, bool> _verifyServer;
    private readonly TimeSpan _connectTimeout;

    internal CliPipeClient()
        : this(CliProtocol.PipeName, PipeServerIdentity.IsTrustedServer, ConnectTimeout)
    {
    }

    // Tests use a unique pipe and a stand-in verifier; production always verifies the actual exe.
    internal CliPipeClient(string pipeName, Func<PipeStream, bool> verifyServer, TimeSpan connectTimeout)
    {
        _pipeName = pipeName;
        _verifyServer = verifyServer;
        _connectTimeout = connectTimeout;
    }

    internal async Task<string> SendAsync(string requestJson, CancellationToken cancellationToken)
    {
        if (requestJson.Length > CliProtocol.MaxMessageChars || requestJson.Contains('\n') || requestJson.Contains('\r'))
        {
            throw new CliException("PROTOCOL_ERROR", "The Light Switch request exceeds the protocol limits.");
        }

        bool requestStarted = false;
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);

            await client.ConnectAsync((int)_connectTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
            if (!_verifyServer(client))
            {
                throw new CliException("SERVICE_UNAVAILABLE", "The pipe is not owned by this installation's Light Switch service.");
            }

            // A failed/cancelled write can still have delivered bytes. From this point onward,
            // connection loss must not be described as "the service was not running".
            byte[] requestBytes = CliProtocol.Encoding.GetBytes(requestJson + "\n");
            requestStarted = true;
            await client.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);

            using var reader = new StreamReader(client, CliProtocol.Encoding, false, CliProtocol.BufferSize, leaveOpen: true);
            return await ReadResponseLineAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new CliException("SERVICE_UNAVAILABLE", "Light Switch is not available. Enable it in PowerToys and try again.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw requestStarted
                ? new CliException("TIMEOUT", "The connection closed before a complete response arrived. The command may already have taken effect; run status before trying again.")
                : new CliException("SERVICE_UNAVAILABLE", "Light Switch is not available. Enable it in PowerToys and try again.");
        }
        catch (DecoderFallbackException)
        {
            throw new CliException("PROTOCOL_ERROR", "Light Switch returned invalid UTF-16 data.");
        }
    }

    internal static async Task<string> ReadResponseLineAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var buffer = new char[CliProtocol.BufferSize];
        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                // A truncated JSON object is not a completed response, even if it happens to parse.
                throw new EndOfStreamException("The response did not contain a line terminator.");
            }

            for (int i = 0; i < count; i++)
            {
                char character = buffer[i];
                if (character == '\n')
                {
                    if (line.Length > 0 && line[^1] == '\r')
                    {
                        line.Length--;
                    }

                    return line.ToString();
                }

                if (line.Length >= CliProtocol.MaxMessageChars && !(line.Length == CliProtocol.MaxMessageChars && character == '\r'))
                {
                    throw new CliException("PROTOCOL_ERROR", "The Light Switch response exceeds the protocol limit.");
                }

                line.Append(character);
            }
        }
    }
}
