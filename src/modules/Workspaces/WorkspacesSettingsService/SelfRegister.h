// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Elevated self-management of the per-user service instance.  Because the MSIX windows.service extension cannot
// express a virtual account, the service is self-registered: the SIGNED service
// binary (in immutable WindowsApps) is launched elevated with one of these verbs
// by the installer custom action (per-machine, as SYSTEM) or the per-user
// provisioner (one-time UAC).  Resolving the binary path from GetModuleFileName
// (never a caller argument) is what mitigates the "point the service at a
// malicious exe" risk.
//
// Service key  : PTSettingsSvc_<SID>
// Account      : NT SERVICE\PTSettingsSvc_<SID>  (dedicated low-priv virtual acct)
// binPath      : "<this exe>" "<SID>"            (SID flows back in as argv[1])

#pragma once

#include <string>

namespace PTSettingsSvc
{
    // Creates (or, if it already exists, re-points the binPath of) the per-user
    // service for `userSidString`, provisions the protected %ProgramData% store
    // (owner=SYSTEM, DACL granting the virtual account Full + the user RX), sets
    // failure/restart actions, and starts it.  Idempotent — safe to run on fresh
    // install, re-install, and upgrade.  Must run elevated.  Returns 0 on success.
    int RunRegister(const std::wstring& userSidString);

    // Re-points the existing service's binPath to THIS binary and restarts it
    // (upgrade path: the versioned WindowsApps path changed).  Must run elevated.
    int RunRepoint(const std::wstring& userSidString);

    // Stops and deletes the per-user service (uninstall).  Must run elevated.
    // Returns 0 on success (or if the service was already absent).
    int RunUnregister(const std::wstring& userSidString);
}
