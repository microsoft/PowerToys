// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// Socket status enumeration for MouseWithoutBorders machine connections.
/// Must match the enum in MouseWithoutBorders\App\Class\Program.cs
/// </summary>
public enum SocketStatus : int
{
    NA = 0,
    Resolving = 1,
    Connecting = 2,
    Handshaking = 3,
    Error = 4,
    ForceClosed = 5,
    InvalidKey = 6,
    Timeout = 7,
    SendError = 8,
    Connected = 9,
}

/// <summary>
/// Represents the connection state of a machine in the MouseWithoutBorders network.
/// Used for IPC communication between Settings UI and MouseWithoutBorders service.
/// </summary>
public struct MachineSocketState
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Status")]
    public SocketStatus Status { get; set; }
}
