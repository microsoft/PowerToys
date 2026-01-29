// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MouseWithoutBorders.Class;
using Logger = MouseWithoutBorders.Core.Logger;

#pragma warning disable SA1649 // File name should match first type name

namespace MouseWithoutBorders.Class;

/// <summary>
/// Command types for IPC protocol.
/// Must match client-side enum in Settings.UI\Helpers\MouseWithoutBordersIpcClient.cs
/// </summary>
internal enum IpcCommandType : byte
{
    Shutdown = 1,
    Reconnect = 2,
    GenerateNewKey = 3,
    ConnectToMachine = 4,
    RequestMachineSocketState = 5,
}

/// <summary>
/// AOT-compatible IPC server for MouseWithoutBorders Settings communication.
/// Replaces StreamJsonRpc with manual NamedPipe protocol.
/// </summary>
internal sealed class MouseWithoutBordersIpcServer
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = false };

    private readonly ISettingsSyncHandler _handler;

    public MouseWithoutBordersIpcServer(ISettingsSyncHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Handles a single client connection
    /// </summary>
    public async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        try
        {
            while (!cancellationToken.IsCancellationRequested && stream.CanRead)
            {
                // Read command type (1 byte)
                var commandByte = reader.ReadByte();
                var command = (IpcCommandType)commandByte;

                switch (command)
                {
                    case IpcCommandType.Shutdown:
                        _handler.Shutdown();
                        break;

                    case IpcCommandType.Reconnect:
                        _handler.Reconnect();
                        break;

                    case IpcCommandType.GenerateNewKey:
                        _handler.GenerateNewKey();
                        break;

                    case IpcCommandType.ConnectToMachine:
                        {
                            var machineName = ReadString(reader);
                            var securityKey = ReadString(reader);
                            _handler.ConnectToMachine(machineName, securityKey);
                        }

                        break;

                    case IpcCommandType.RequestMachineSocketState:
                        {
                            var states = await _handler.RequestMachineSocketStateAsync();
                            var json = JsonSerializer.Serialize(states, JsonOptions);
                            WriteString(writer, json);
                            await stream.FlushAsync(cancellationToken);
                        }

                        break;

                    default:
                        Logger.Log($"Unknown IPC command: {commandByte}");
                        return; // Invalid command, close connection
                }
            }
        }
        catch (EndOfStreamException)
        {
            // Client disconnected, normal termination
        }
        catch (IOException)
        {
            // Pipe broken, normal termination
        }
        catch (Exception ex)
        {
            Logger.Log($"IPC error: {ex}");
        }
    }

    /// <summary>
    /// Reads a length-prefixed UTF-8 string
    /// </summary>
    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length <= 0 || length > 1024 * 1024)
        {
            return string.Empty;
        }

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Writes a length-prefixed UTF-8 string
    /// </summary>
    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

/// <summary>
/// Interface for handling IPC commands.
/// Implemented by SettingsSyncHelper in Program.cs
/// </summary>
internal interface ISettingsSyncHandler
{
    void Shutdown();

    void Reconnect();

    void GenerateNewKey();

    void ConnectToMachine(string machineName, string securityKey);

    Task<MachineSocketState[]> RequestMachineSocketStateAsync();
}

/// <summary>
/// Machine socket state for serialization.
/// Uses SocketStatus from SocketStuff.cs in MouseWithoutBorders.Class namespace.
/// </summary>
public struct MachineSocketState
{
    public string Name { get; set; }

    public MouseWithoutBorders.Class.SocketStatus Status { get; set; }
}
