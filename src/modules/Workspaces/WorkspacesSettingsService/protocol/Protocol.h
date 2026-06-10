// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Shared wire protocol between the Workspaces settings service and its clients
// (Editor / Launcher / Runner migration step).
//
// Wire format (little-endian, no padding):
//
//   REQUEST  := opcode(uint8) | length(uint32) | payload[length]
//   RESPONSE := status(uint8) | length(uint32) | payload[length]
//
// One request per connection.  After the response is written the server
// disconnects.  Keep this surface as small as possible — every additional
// opcode is a new attack surface on a privileged endpoint.

#pragma once

#include <cstdint>

namespace WorkspacesSvc
{
    // Wire constants ---------------------------------------------------------

    constexpr const wchar_t* kPipeName = L"\\\\.\\pipe\\PTWorkspacesSvc";
    constexpr const wchar_t* kServiceName = L"PTWorkspacesSvc";

    // Bumped whenever the JSON envelope produced by the service changes in a
    // non-backwards-compatible way.  Clients reading a higher version should
    // refuse and ask the user to upgrade.
    constexpr uint32_t kCurrentSchemaVersion = 2;

    // Payload size guard rails.  A typical workspaces.json sits in the low
    // tens of KB; the largest real one observed in telemetry was ~2 MB.
    // 8 MB is generous and bounds memory the service has to allocate.
    constexpr uint32_t kMaxPayloadBytes = 8u * 1024u * 1024u;

    // Inactivity window after which the demand-started service self-stops.
    constexpr uint32_t kIdleShutdownSeconds = 60;

    enum class Opcode : uint8_t
    {
        Ping = 0x00,                // No payload.  Useful for liveness checks.
        GetSettings = 0x01,         // No payload.  Returns the caller's workspaces.json.
        PutSettings = 0x02,         // payload = full JSON bytes.  Replaces the file atomically.
        GetSchemaVersion = 0x03,    // No payload.  Returns 4-byte LE uint32.
        MigrateFromLegacy = 0x04,   // payload = legacy JSON bytes read by the client.
    };

    enum class Status : uint8_t
    {
        Ok = 0x00,

        AuthFailToken = 0x10,       // Caller token isn't a regular interactive user.
        AuthFailCallerPath = 0x11,  // Caller exe isn't in the PowerToys install dir
                                    // or doesn't match the allow-list.

        BadRequest = 0x20,          // Malformed framing.
        PayloadTooLarge = 0x21,     // length > kMaxPayloadBytes.
        JsonInvalid = 0x22,         // Payload is not valid JSON / fails schema check.
        SchemaUnsupported = 0x23,   // schemaVersion is newer than service knows.

        IoError = 0x30,             // Disk / DACL failure.
        Internal = 0x31,            // Anything we didn't categorise.

        UnknownOpcode = 0xFF,
    };
}
