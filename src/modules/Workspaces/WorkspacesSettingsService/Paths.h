// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <string>

namespace PTSettingsSvc
{
    // %ProgramData%\Microsoft\PowerToys\Settings
    std::wstring GetSettingsRoot();

    // %ProgramData%\Microsoft\PowerToys\SettingsSvcBin
    // Root for the service's own runnable copy of the exe.  The service binary
    // is STAGED (signed) in WindowsApps, but a classic virtual-account service
    // cannot read an exe there (WindowsApps grants BUILTIN\Users, not our
    // dedicated NT SERVICE\PTSettingsSvc_<SID> account, and its ACL cannot be
    // modified even elevated).  So the register path copies the exe into a
    // per-version subfolder here, owner=SYSTEM with a protected DACL that grants
    // the virtual account RX, and points the service at that copy.
    std::wstring GetServiceBinRoot();

    // %ProgramData%\Microsoft\PowerToys\Settings\<sid>
    // Per-user node: this is where the protected, user-isolating DACL is
    // applied; everything below inherits it.
    std::wstring GetUserFolder(const std::wstring& userSidString);

    // %ProgramData%\Microsoft\PowerToys\Settings\<sid>\<namespaceId>
    std::wstring GetUserNamespaceFolder(const std::wstring& userSidString,
                                        const std::wstring& namespaceId);

    // %ProgramData%\Microsoft\PowerToys\Settings\<sid>\<namespaceId>\<fileName>
    std::wstring GetUserFilePath(const std::wstring& userSidString,
                                 const std::wstring& namespaceId,
                                 const std::wstring& fileName);

    // Convert a binary SID to its string form (S-1-5-21-...).  Empty on failure.
    std::wstring SidToString(void* psid);
}
