// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Auth;

/// <summary>
/// A minimal single-request loopback HTTP server built on <see cref="TcpListener"/>
/// (so it needs no HTTP.SYS URL ACL / elevation). It accepts exactly one
/// connection, captures the raw request, and replies with a caller-supplied
/// status code and JSON body.
/// </summary>
internal sealed class MockHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _statusCode;
    private readonly string _responseBody;
    private readonly Task _serveTask;

    public MockHttpServer(int statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serveTask = Task.Run(ServeOneAsync);
    }

    public int Port { get; }

    public string Url => $"http://127.0.0.1:{Port}/token";

    /// <summary>The full raw HTTP request text captured from the client.</summary>
    public string? CapturedRequest { get; private set; }

    private async Task ServeOneAsync()
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            var buffer = new byte[8192];
            var sb = new StringBuilder();
            int headerEnd = -1;

            // Read until we have the full header block.
            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0)
                {
                    break;
                }

                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                headerEnd = sb.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal);
            }

            if (headerEnd < 0)
            {
                return;
            }

            var raw = sb.ToString();
            var contentLength = ParseContentLength(raw);
            var bodyStart = headerEnd + 4;
            var bodyReceived = raw.Length - bodyStart;

            // Drain the remaining request body so the client's write completes.
            while (bodyReceived < contentLength)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0)
                {
                    break;
                }

                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                bodyReceived += read;
            }

            CapturedRequest = sb.ToString();

            var bodyBytes = Encoding.UTF8.GetBytes(_responseBody);
            var header =
                $"HTTP/1.1 {_statusCode} {ReasonPhrase(_statusCode)}\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(bodyBytes);
            await stream.FlushAsync();
        }
        catch (Exception)
        {
            // Best-effort test server.
        }
    }

    private static int ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line["Content-Length:".Length..].Trim(), out var len))
                {
                    return len;
                }
            }
        }

        return 0;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        200 => "OK",
        400 => "Bad Request",
        401 => "Unauthorized",
        _ => "Status",
    };

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
        }
    }
}
