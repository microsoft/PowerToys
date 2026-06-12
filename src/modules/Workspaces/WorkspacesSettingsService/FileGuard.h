// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <string>
#include <vector>

namespace PTSettingsSvc
{
    // Creates `folder` if it doesn't exist and applies the DACL that locks
    // the directory to:
    //   * the service account             — Full Control
    //   * BUILTIN\Administrators          — Read & Execute (audit/backup)
    //   * the user whose SID is passed in — Read & Execute (Launcher needs to read)
    //   * Everyone else                   — denied (DACL is protected, no inherit)
    HRESULT EnsureUserFolder(const std::wstring& folder,
                             const std::wstring& userSidString);

    // Atomically replaces `targetFile` with `bytes`.  Internally writes to
    // a sibling .tmp and uses ReplaceFileW so a crash during write never
    // leaves the file in a half-written state.  Re-asserts the directory's
    // protective DACL after the write in case something has tampered with it.
    HRESULT WriteFileAtomically(const std::wstring& targetFile,
                                const std::vector<BYTE>& bytes);

    // Reads an entire file into memory.  Caps at maxBytes; returns
    // HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE) if exceeded.
    HRESULT ReadFileFully(const std::wstring& path,
                          uint32_t maxBytes,
                          std::vector<BYTE>& outBytes);
}
