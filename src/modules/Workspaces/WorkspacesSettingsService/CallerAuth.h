// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <string>

namespace PTSettingsSvc
{
    struct CallerBinding;  // Bindings.h

    struct CallerIdentity
    {
        std::wstring        userSidString;   // S-1-5-21-...  (per-user data partition key)
        std::wstring        imagePath;       // Canonicalised, reparse-points resolved
        DWORD               processId{};
        const CallerBinding* binding = nullptr;  // never freed (static table)
    };

    // Authenticates the client connected to the named-pipe handle.
    //
    // Successful authentication means ALL of the following hold:
    //   * Caller token is a real interactive user (not SYSTEM / SERVICE /
    //     ANONYMOUS), so we have a SID to scope the per-user data folder.
    //   * Caller image is trusted by EITHER anchor (Design-v6-Final.md §7):
    //       - PATH anchor: image resolves under %ProgramFiles%\PowerToys and
    //         that folder's DACL is admin-only writable (per-machine), OR
    //       - BINARY-IDENTITY anchor: image is Microsoft-signed AND its version
    //         equals the service's own version (per-user, user-writable folder).
    //   * Caller image basename is in the CallerBinding allow-list — and the
    //     matched binding is returned in outIdentity.binding so the dispatch
    //     layer knows which namespace this caller may operate on.
    //
    // The path anchor is preferred where available (smaller privileged surface,
    // immutability); the signature+version anchor is the fallback used only
    // when the path cannot be trusted.  See §7 and §15 #5.
    //
    // The function ImpersonateNamedPipeClient()s internally and reverts
    // before returning, regardless of success.
    //
    // Returns:
    //   S_OK                                     — all checks passed
    //   E_ACCESSDENIED                           — auth-rejected (path, DACL, or basename)
    //   HRESULT_FROM_WIN32(ERROR_NOT_FOUND)      — basename allow-listed but
    //                                              binding lookup returned nullptr
    //   any other HRESULT                        — Win32 failure (token read,
    //                                              OpenProcess, etc.)
    HRESULT AuthenticateCaller(HANDLE pipeHandle, CallerIdentity& outIdentity);
}
