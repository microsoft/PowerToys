// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <string>
#include <vector>

namespace PTSettingsSvc
{
    // --- Provisioning (elevated register path, Approach 4 §12.8) ---------------
    // These run from the service exe's `--register` mode under an elevated actor
    // (the installer CA as SYSTEM, or the per-user provisioner elevated), NOT at
    // service runtime — the low-privilege virtual account cannot set owner=SYSTEM
    // or a protected DACL itself (Design §12.8, Blocker-1 resolution: store
    // provisioning moved out of the runtime service into the elevated registrar).

    // Creates the store root (<ProgramData>\Microsoft\PowerToys\Settings) if it
    // doesn't exist and applies the root DACL: SYSTEM/Admins Full, Authenticated
    // Users RX (traverse so each user reaches their own <sid> node).  Idempotent.
    HRESULT EnsureStoreRoot(const std::wstring& root);

    // Creates `folder` (the per-user <sid> node) if needed and applies the
    // PROTECTED DACL that locks it to:
    //   * owner                                = SYSTEM (recovery; low-priv svc
    //                                            cannot rewrite the DACL)
    //   * the service virtual account          = Full Control (sole writer)
    //   * BUILTIN\Administrators               = Full Control (audit/backup)
    //   * the owning user (SID passed in)      = Read & Execute (callers read)
    //   * everyone else                        = denied (protected, no inherit)
    // `serviceAccountName` is the virtual account, e.g.
    // L"NT SERVICE\\PTSettingsSvc_<SID>".  Requires SeRestore/SeTakeOwnership to
    // set the SYSTEM owner; the register path enables them.
    HRESULT EnsureUserFolder(const std::wstring& folder,
                             const std::wstring& userSidString,
                             const std::wstring& serviceAccountName);

    // Convenience: provision root + per-user node in one call (register path).
    HRESULT ProvisionStore(const std::wstring& root,
                           const std::wstring& userFolder,
                           const std::wstring& userSidString,
                           const std::wstring& serviceAccountName);

    // --- Runtime (the low-privilege service) -----------------------------------

    // Creates `dir` if it doesn't exist WITHOUT touching owner/DACL — the
    // register path already provisioned the protected parent, and the running
    // virtual account only has (and only needs) Full Control to create children
    // and write files that inherit the parent's protected DACL.
    HRESULT EnsureDirectory(const std::wstring& dir);

    // Atomically replaces `targetFile` with `bytes`.  Internally writes to
    // a sibling .tmp and uses ReplaceFileW so a crash during write never
    // leaves the file in a half-written state.
    HRESULT WriteFileAtomically(const std::wstring& targetFile,
                                const std::vector<BYTE>& bytes);

    // Reads an entire file into memory.  Caps at maxBytes; returns
    // HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE) if exceeded.
    HRESULT ReadFileFully(const std::wstring& path,
                          uint32_t maxBytes,
                          std::vector<BYTE>& outBytes);
}
