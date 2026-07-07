// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Shared wire protocol between PTSettingsSvc and its clients.
//
// Wire format (little-endian, no padding):
//
//   REQUEST  := opcode(uint8) | length(uint32) | payload[length]
//   RESPONSE := status(uint8) | length(uint32) | payload[length]
//
// One request per connection.  After the response is written the server
// disconnects.  Keep this surface as small as possible — every additional
// opcode is a new attack surface on a privileged endpoint.
//
// The service treats `payload` as opaque bytes.  It does not parse them,
// does not validate their shape, does not interpret a "schema version"
// inside them.  Module-specific concerns (JSON shape, schema versioning,
// migration from legacy on-disk layouts, sensitive-field stripping) all
// live in the caller — see Design-v6-Final.md §4 and §10.

#pragma once

#include <cstdint>

namespace PTSettingsSvc
{
    // Wire constants ---------------------------------------------------------

    // Per-user pipe naming lives in PipeName.h (\\.\pipe\PTSettingsSvc_<SID>);
    // there is no single fixed pipe name under Approach 4 (§12.8).  kServiceName
    // is the BASE service key; the registrar appends _<SID> per user.
    constexpr const wchar_t* kServiceName = L"PTSettingsSvc";

    // Payload size guard rails.  A typical settings blob sits in the low
    // tens of KB.  1 MiB is generous and bounds memory the service has to
    // allocate per request.
    constexpr uint32_t kMaxPayloadBytes = 1u * 1024u * 1024u;

    enum class Opcode : uint8_t
    {
        Ping     = 0x00,   // No payload.  Authn still runs.  Used by liveness checks.
        GetBlob  = 0x01,   // No payload.  Returns the caller's namespace blob bytes.
        PutBlob  = 0x02,   // payload = full blob bytes.  Atomic replace.
    };

    enum class Status : uint8_t
    {
        Ok = 0x00,

        // Framing / dispatch errors.
        BadRequest       = 0x01,
        UnknownOpcode    = 0x02,
        PayloadTooLarge  = 0x03,

        // Authentication outcomes.
        AuthFailToken    = 0x10,   // Caller token is synthetic (SYSTEM / SERVICE / etc.)
                                   // or the SID couldn't be read.
        AuthFailCaller   = 0x11,   // Caller exe failed path / DACL-hardness /
                                   // basename allow-list.
        NamespaceUnknown = 0x12,   // Caller authenticated but is not in the
                                   // binding table (should never happen for
                                   // well-formed clients).

        // Storage outcomes.
        NotFound         = 0x20,   // GetBlob: blob does not exist yet.
        IoError          = 0x21,   // Underlying file IO failed.
    };
}
