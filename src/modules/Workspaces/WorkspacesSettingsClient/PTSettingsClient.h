// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Thin C++ client for PTSettingsSvc.  Linked into PowerToys.WorkspacesEditor /
// WorkspacesSnapshotTool / runner / etc.  The client is payload-agnostic —
// it shuttles opaque bytes to and from the service.  Whatever the bytes mean
// is the caller's responsibility (JSON shape, schema version, sensitive-
// field stripping, migration logic).
//
// Modules using settings (e.g. Workspaces) wrap this in their own
// type-safe layer (Workspaces serialises its `Workspaces` object → UTF-8
// JSON bytes → PutBlob; reverse on read).

#pragma once

#include <string>
#include <vector>
#include <cstdint>

namespace PTSettingsClient
{
    enum class Result : uint8_t
    {
        Ok = 0,
        ServiceUnavailable,        // Pipe couldn't be opened (service stopped
                                   // or wrong machine).
        AuthRejected,              // Service refused the caller — usually
                                   // means binary isn't where the MSI put it,
                                   // basename not allow-listed, or the
                                   // install folder DACL isn't hardened.
        NamespaceUnknown,          // Caller authenticated but isn't in the
                                   // binding table.  Build-time misconfig.
        NotFound,                  // GetBlob: blob does not exist yet.
        ProtocolError,             // Truncated / malformed wire frames.
        PayloadTooLarge,           // Local or remote rejected oversize payload.
        IoError,                   // Service-side disk failure.
        UnknownStatus,             // Server returned a status code we don't recognise.
    };

    Result Ping();

    // Reads the caller's namespace blob.  Returns NotFound (with `outBytes`
    // empty) when no blob has ever been written for this user+namespace.
    Result GetBlob(std::vector<uint8_t>& outBytes);

    // Replaces the caller's namespace blob with `bytes`.  Service does
    // the atomic write + DACL re-assertion.
    Result PutBlob(const std::vector<uint8_t>& bytes);
}
