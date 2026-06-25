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
}
