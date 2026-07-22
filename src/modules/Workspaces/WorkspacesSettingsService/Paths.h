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

    // Path to the PowerToys install folder (from HKLM\SOFTWARE\Classes\PowerToys
    // or the registry key the bootstrapper writes).  Empty string on failure.
    std::wstring GetPowerToysInstallFolder();

    // Returns true iff `folder` exists AND its DACL grants write/create/delete
    // only to admin-class principals (BUILTIN\Administrators,
    // NT AUTHORITY\SYSTEM, NT SERVICE\TrustedInstaller).  Used by the auth
    // pipeline to reject install paths that landed in a user-writable
    // location (custom MSI directory under a Users-writable parent, per-user
    // MSI under %LocalAppData%, etc.) — in those cases same-user malware
    // could plant a fake allow-listed exe there and pass the path+name check.
    bool IsFolderAdminOnlyWritable(const std::wstring& folder);

    // Convert a binary SID to its string form (S-1-5-21-...).  Empty on failure.
    std::wstring SidToString(void* psid);
}
