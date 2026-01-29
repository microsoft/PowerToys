// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

/// <summary>
/// AOT-compatible IPC client for MouseWithoutBorders service communication.
/// Replaces StreamJsonRpc with manual NamedPipe protocol using length-prefixed messages.
/// </summary>
public sealed class MouseWithoutBordersIpcClient : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryWriter _writer;
    private readonly BinaryReader _reader;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Command types for IPC protocol.
    /// Must match server-side enum in MouseWithoutBorders Program.cs
    /// </summary>
    private enum CommandType : byte
    {
        Shutdown = 1,
        Reconnect = 2,
        GenerateNewKey = 3,
        ConnectToMachine = 4,
        RequestMachineSocketState = 5,
    }

    public MouseWithoutBordersIpcClient(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
        _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>
    /// Sends shutdown command to MouseWithoutBorders service
    /// </summary>
    public async Task ShutdownAsync()
    {
        await SendCommandAsync(CommandType.Shutdown);
        await FlushAsync();
    }

    /// <summary>
    /// Sends reconnect command to MouseWithoutBorders service
    /// </summary>
    public async Task ReconnectAsync()
    {
        await SendCommandAsync(CommandType.Reconnect);
        await FlushAsync();
    }

    /// <summary>
    /// Requests generation of a new security key
    /// </summary>
    public async Task GenerateNewKeyAsync()
    {
        await SendCommandAsync(CommandType.GenerateNewKey);
        await FlushAsync();
    }

    /// <summary>
    /// Requests connection to a specific machine
    /// </summary>
    public async Task ConnectToMachineAsync(string machineName, string securityKey)
    {
        lock (_lock)
        {
            _writer.Write((byte)CommandType.ConnectToMachine);

            // Write machine name (length-prefixed string)
            WriteString(machineName ?? string.Empty);

            // Write security key (length-prefixed string)
            WriteString(securityKey ?? string.Empty);
        }

        await FlushAsync();
    }

    /// <summary>
    /// Requests current state of all connected machines
    /// </summary>
    public async Task<MachineSocketState[]> RequestMachineSocketStateAsync()
    {
        // Send command
        await SendCommandAsync(CommandType.RequestMachineSocketState);
        await FlushAsync();

        // Read response
        var jsonResponse = await ReadStringAsync();

        if (string.IsNullOrEmpty(jsonResponse))
        {
            return Array.Empty<MachineSocketState>();
        }

        try
        {
            // Use source-generated JSON serialization
            return JsonSerializer.Deserialize(jsonResponse, SettingsSerializationContext.Default.MachineSocketStateArray)
                   ?? Array.Empty<MachineSocketState>();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to deserialize MachineSocketState: {ex.Message}");
            return Array.Empty<MachineSocketState>();
        }
    }

    /// <summary>
    /// Flushes the underlying stream asynchronously
    /// </summary>
    public async Task FlushAsync()
    {
        await _stream.FlushAsync();
    }

    /// <summary>
    /// Sends a simple command without parameters
    /// </summary>
    private Task SendCommandAsync(CommandType command)
    {
        lock (_lock)
        {
            _writer.Write((byte)command);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a length-prefixed UTF-8 string
    /// </summary>
    private void WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        _writer.Write(bytes.Length); // 4-byte length prefix
        _writer.Write(bytes);
    }

    /// <summary>
    /// Reads a length-prefixed UTF-8 string asynchronously
    /// </summary>
    private async Task<string> ReadStringAsync()
    {
        var lengthBytes = new byte[4];
        var bytesRead = await _stream.ReadAsync(lengthBytes.AsMemory(0, 4));

        if (bytesRead != 4)
        {
            return string.Empty;
        }

        var length = BitConverter.ToInt32(lengthBytes, 0);

        // Max 1MB to prevent memory exhaustion
        if (length <= 0 || length > 1024 * 1024)
        {
            return string.Empty;
        }

        var stringBytes = new byte[length];
        bytesRead = await _stream.ReadAsync(stringBytes.AsMemory(0, length));

        if (bytesRead != length)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(stringBytes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer?.Dispose();
        _reader?.Dispose();

        // Note: Do not dispose _stream as it's owned by the caller (NamedPipeClientStream)
    }
}
