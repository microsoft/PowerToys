// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Thin C++ client for PTWorkspacesSvc.  Linked into PowerToys.WorkspacesEditor /
// WorkspacesSnapshotTool / runner.  WorkspacesLauncher does NOT use it: the
// launcher only ever reads the file directly (the per-user data folder
// already grants the user Read+Execute, no IPC required for the read path).

#pragma once

#include <string>
#include <vector>
#include <cstdint>

namespace WorkspacesSvcClient
{
    enum class Result : uint8_t
    {
        Ok = 0,
        ServiceUnavailable,        // Pipe couldn't be opened (service stopped
                                   // & failed to start, or wrong machine).
        AuthRejected,              // Service refused the caller — usually
                                   // means binary isn't where the MSI put it
                                   // (e.g. running from a build output dir).
        ProtocolError,             // Truncated / malformed wire frames.
        ServerError,               // Service returned an IoError / Internal.
        PayloadInvalid,            // JSON shape rejected by the service.
    };

    // Convenience helpers that perform a full connect → request → response
    // → disconnect against the service.

    Result Ping();

    // Reads the caller's workspaces.json.  Returns Ok with `outJsonUtf8`
    // empty when the file does not exist yet.
    Result GetSettings(std::string& outJsonUtf8);

    // Replaces the caller's workspaces.json with `jsonUtf8`.  Service does
    // the atomic write + DACL re-assertion.
    Result PutSettings(const std::string& jsonUtf8);

    // Migration entry point used by the runner.  Idempotent and cheap if
    // the user has already been migrated.
    Result MigrateFromLegacy(const std::string& legacyJsonUtf8);
}
