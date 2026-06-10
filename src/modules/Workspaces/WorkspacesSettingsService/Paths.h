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

    // Convert a binary SID to its string form (S-1-5-21-...).  Empty on failure.
    std::wstring SidToString(void* psid);
}
