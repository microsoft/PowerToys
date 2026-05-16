// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <filesystem>
#include <string>

namespace updating
{
    namespace fs = std::filesystem;

    // Build the command-line arguments for Stage 2.
    // Stage 1 passes the installer path and the PT install directory
    // so Stage 2 can run the installer and relaunch PowerToys afterward.
    // Note: paths containing embedded double-quote characters are not supported.
    // This is safe because install paths come from get_module_folderpath().
    inline std::wstring BuildStage2Arguments(
        const std::wstring& stage2Flag,
        const fs::path& installerPath,
        const fs::path& installDir)
    {
        std::wstring arguments{ stage2Flag };
        arguments += L" \"";
        arguments += installerPath.c_str();
        arguments += L"\" \"";
        arguments += installDir.c_str();
        arguments += L"\"";
        return arguments;
    }

    // Build the full path to PowerToys.exe from the install directory.
    // Used by Stage 2 to relaunch PT after a successful update.
    inline std::wstring BuildPowerToysExePath(const std::wstring& installDir)
    {
        return (std::filesystem::path(installDir) / L"PowerToys.exe").wstring();
    }

    // Determine whether Stage 2 has enough information to relaunch PT.
    // Returns true if the install directory argument was provided.
    inline bool CanRelaunchAfterUpdate(int argCount)
    {
        // args[0]=exe, args[1]=action, args[2]=installer, args[3]=installDir
        return argCount >= 4;
    }
}
