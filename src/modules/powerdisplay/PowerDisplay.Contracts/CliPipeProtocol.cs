// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerDisplay.Contracts;

/// <summary>
/// Wire-framing constants for the CLI&lt;-&gt;app named pipe, shared by the client and server so the
/// two ends cannot drift. The exchange is one '\n'-delimited request line and one '\n'-delimited
/// response line.
/// </summary>
public static class CliPipeProtocol
{
    /// <summary>
    /// BOM-less UTF-16 LE. <see cref="Encoding.Unicode"/> emits a BOM on the first write which
    /// corrupts line framing on a named pipe; this encoding is identical in every other respect
    /// (UTF-16 LE, 2 bytes per ASCII char). Both pipe ends MUST use this exact encoding.
    /// </summary>
    public static readonly Encoding PipeEncoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);

    /// <summary>Stream reader/writer buffer size used by both pipe ends.</summary>
    public const int BufferSize = 1024;

    /// <summary>
    /// Maximum length (in characters) the server will accept for a single request line. The
    /// protocol carries one short JSON object, so this is a generous sanity bound that prevents an
    /// unbounded read from buffering arbitrary amounts of memory in the app process.
    /// </summary>
    public const int MaxRequestChars = 64 * 1024;

    /// <summary>
    /// How long the server waits for a connected client to send its request line before abandoning
    /// the connection. Without this a client that connects but never sends a line would stall the
    /// single-threaded accept loop for every other CLI invocation.
    /// </summary>
    public const int ReadTimeoutMilliseconds = 10_000;

    /// <summary>
    /// How long the server waits for the response write and drain (<c>WaitForPipeDrain</c>) to
    /// complete before abandoning the connection. Bounds the write phase the same way
    /// <see cref="ReadTimeoutMilliseconds"/> bounds the read phase: the pipe uses a 0-byte output
    /// buffer, so both the write and the drain block until the client reads, and a connected client
    /// that never reads the response would otherwise wedge the single-threaded accept loop
    /// indefinitely (<c>WaitForPipeDrain</c> has no timeout/<c>CancellationToken</c> overload).
    /// </summary>
    public const int WriteTimeoutMilliseconds = 10_000;
}
