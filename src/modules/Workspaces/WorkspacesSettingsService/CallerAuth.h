// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <string>

namespace WorkspacesSvc
{
    struct CallerIdentity
    {
        std::wstring userSidString;   // S-1-5-21-...  (per-user data partition key)
        std::wstring imagePath;       // Canonicalised, reparse-points resolved
        DWORD        processId{};
    };

    // Authenticates the client connected to the named-pipe handle.
    //
    // Successful authentication means ALL of the following hold:
    //   * Caller token is a real interactive user (not SYSTEM / SERVICE /
    //     ANONYMOUS), so we have a SID to scope the per-user data folder.
    //   * Caller image path resolves under %ProgramFiles%\PowerToys (the
    //     install folder recorded by the MSI in HKLM).
    //   * Caller image file name matches the allow-list of PowerToys binaries
    //     that legitimately need to mutate workspace settings (Editor,
    //     SnapshotTool, runner — i.e. *not* the launcher: the launcher only
    //     reads).
    //
    // We intentionally do NOT verify Authenticode here.  The previous v5
    // design relied on signature/publisher gating; v6 deliberately drops it
    // and relies on:
    //   (a) the OS-enforced ACL on %ProgramFiles%\PowerToys — same-user
    //       malware can't drop a binary there, so "image path is under PT
    //       install folder" already implies "binary was put there by an
    //       admin-mediated MSI",
    //   (b) the OS-enforced ACL on the data file — even if a non-PT process
    //       impersonated a PT binary by some other means, the data file
    //       itself rejects every write that doesn't come from this service.
    //
    // The function ImpersonateNamedPipeClient()s internally and reverts
    // before returning, regardless of success.
    HRESULT AuthenticateCaller(HANDLE pipeHandle, CallerIdentity& outIdentity);
}
