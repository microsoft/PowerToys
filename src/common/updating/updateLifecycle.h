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

    // Returns true when the value read from UpdateState.json is a plain installer
    // filename that can be combined with the pending-updates directory. UpdateState.json
    // is persisted state that may be stale, corrupted, or otherwise unexpected, so the
    // cached filename could contain path separators or an absolute/drive-relative path.
    // Only a single bare filename (the form produced by the download step) is accepted;
    // anything else is rejected so the updater never looks outside the Updates folder.
    inline bool IsSafeDownloadedInstallerFilename(const std::wstring& filename)
    {
        if (filename.empty())
        {
            return false;
        }

        // Reject any path separators or parent-directory tokens outright. Installer
        // asset filenames never contain these.
        if (filename.find(L'/') != std::wstring::npos ||
            filename.find(L'\\') != std::wstring::npos ||
            filename.find(L"..") != std::wstring::npos)
        {
            return false;
        }

        const fs::path candidate{ filename };

        // Must be a single path component: no drive/root and no directory portion.
        if (candidate.has_root_name() || candidate.has_root_directory() || candidate.has_parent_path())
        {
            return false;
        }

        const auto name = candidate.filename().wstring();
        if (name != filename || name == L"." || name == L"..")
        {
            return false;
        }

        return true;
    }
}
