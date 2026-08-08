// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// A minimal single-request loopback HTTP server for capturing an OAuth
/// redirect. It binds to <see cref="IPAddress.Loopback"/> (127.0.0.1) only,
/// never to any external interface, on an ephemeral high port. It accepts
/// connections until the browser hits the callback path, captures the query
/// parameters, serves a small "you can return to Command Palette" page, and
/// stops. Requests to any other path get a 404 so the listener keeps waiting.
/// </summary>
public sealed partial class TcpLoopbackRedirectListener : ILoopbackRedirectListener
{
    private const string LandingPageHtml =
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        "<title>Command Palette</title></head>" +
        "<body style=\"font-family:Segoe UI,system-ui,sans-serif;text-align:center;padding-top:64px\">" +
        "<h2>Sign-in complete</h2>" +
        "<p>You can close this tab and return to Command Palette.</p></body></html>";

    private readonly TcpListener _listener;
    private bool _disposed;

    public TcpLoopbackRedirectListener()
    {
        // Bind to the loopback interface only. Never 0.0.0.0 / ::.
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        RedirectUri = $"http://127.0.0.1:{port}/";
    }

    public string RedirectUri { get; }

    public async Task<IReadOnlyDictionary<string, string>> WaitForRedirectAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            using var stream = client.GetStream();

            var requestLine = await ReadRequestLineAsync(stream, cancellationToken).ConfigureAwait(false);
            var (path, query) = ParseRequestTarget(requestLine);

            // The advertised redirect_uri path is "/". Anything else (favicon,
            // etc.) is not the callback, so answer 404 and keep listening.
            if (!string.Equals(path, "/", StringComparison.Ordinal) || query.Count == 0)
            {
                await WriteResponseAsync(stream, 404, "text/plain", "Not found", cancellationToken).ConfigureAwait(false);
                continue;
            }

            await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", LandingPageHtml, cancellationToken).ConfigureAwait(false);
            return query;
        }
    }

    private static async Task<string> ReadRequestLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (sb.Length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
            var newline = sb.ToString().IndexOf('\n');
            if (newline >= 0)
            {
                return sb.ToString(0, newline).TrimEnd('\r');
            }
        }

        return sb.ToString();
    }

    private static (string Path, IReadOnlyDictionary<string, string> Query) ParseRequestTarget(string requestLine)
    {
        // "GET /?code=...&state=... HTTP/1.1"
        var parts = requestLine.Split(' ');
        var target = parts.Length >= 2 ? parts[1] : "/";

        var queryIndex = target.IndexOf('?');
        if (queryIndex < 0)
        {
            return (target, new Dictionary<string, string>());
        }

        var path = target[..queryIndex];
        var query = QueryStringParser.Parse(target[(queryIndex + 1)..]);
        return (path, query);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, string contentType, string body, CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var reason = statusCode == 200 ? "OK" : "Not Found";
        var header =
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
        }
    }
}
