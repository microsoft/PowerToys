// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <string>

namespace WorkspacesSvc
{
    // %ProgramData%\Microsoft\PowerToys\Workspaces
    std::wstring GetWorkspacesRoot();

    // %ProgramData%\Microsoft\PowerToys\Workspaces\<sid>
    std::wstring GetUserWorkspacesFolder(const std::wstring& userSidString);

    // %ProgramData%\Microsoft\PowerToys\Workspaces\<sid>\workspaces.json
    std::wstring GetUserWorkspacesFile(const std::wstring& userSidString);

    // %ProgramData%\Microsoft\PowerToys\Workspaces\<sid>\workspaces.json.legacy.bak
    std::wstring GetUserLegacyBackupFile(const std::wstring& userSidString);

    // Path to PowerToys install folder (from HKLM\SOFTWARE\Classes\powertoys
    // or the registry key the bootstrapper writes).  Empty string on failure.
    std::wstring GetPowerToysInstallFolder();

    // Returns true iff `folder` exists AND its DACL grants write/create/delete
    // only to admin-class principals (BUILTIN\Administrators,
    // NT AUTHORITY\SYSTEM, NT SERVICE\TrustedInstaller).  Used by the auth
    // pipeline to reject install paths that landed in a user-writable
    // location (custom MSI directory under a Users-writable parent, per-user
    // MSI under %LocalAppData%, etc.) — in those cases same-user malware
    // could plant a fake `PowerToys.WorkspacesEditor.exe` and pass the
    // path+name check.
    bool IsFolderAdminOnlyWritable(const std::wstring& folder);

    // Convert a binary SID to its string form (S-1-5-21-...).  Empty on failure.
    std::wstring SidToString(void* psid);
}
