// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include "Generated Files/resource.h"

#include <Windows.h>
#include <shellapi.h>

#include <filesystem>
#include <string_view>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

#include <common/SettingsAPI/settings_helpers.h>

#include <common/logger/logger.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>

#include "../runner/tray_icon.h"
#include "../runner/ActionRunnerUtils.h"

using namespace cmdArg;

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    int nArgs = 0;
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &nArgs);
    if (!args || nArgs < 2)
    {
        return 1;
    }

    std::wstring_view action{ args[1] };

    std::filesystem::path logFilePath(PTSettingsHelper::get_root_save_folder_location());
    logFilePath.append(LogSettings::actionRunnerLogPath);
    Logger::init(LogSettings::actionRunnerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    if (action == RUN_NONELEVATED)
    {
        int nextArg = 2;

        std::wstring_view target;
        std::wstring_view pidFile;
        std::wstring params;

        while (nextArg < nArgs)
        {
            if (std::wstring_view(args[nextArg]) == L"-target" && nextArg + 1 < nArgs)
            {
                target = args[nextArg + 1];
                nextArg += 2;
            }
            else if (std::wstring_view(args[nextArg]) == L"-pidFile" && nextArg + 1 < nArgs)
            {
                pidFile = args[nextArg + 1];
                nextArg += 2;
            }
            else
            {
                params += args[nextArg];
                params += L' ';
                nextArg++;
            }
        }

        HANDLE hMapFile = NULL;
        PDWORD pidBuffer = NULL;

        if (!pidFile.empty())
        {
            hMapFile = OpenFileMappingW(FILE_MAP_WRITE, FALSE, pidFile.data());
            if (hMapFile)
            {
                pidBuffer = static_cast<PDWORD>(MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(DWORD)));
                if (pidBuffer)
                {
                    *pidBuffer = 0;
                }
            }
        }

        run_same_elevation(target.data(), params, pidBuffer);

        if (!pidFile.empty())
        {
            if (pidBuffer)
            {
                FlushViewOfFile(pidBuffer, sizeof(DWORD));
                UnmapViewOfFile(pidBuffer);
            }

            if (hMapFile)
            {
                FlushFileBuffers(hMapFile);
                CloseHandle(hMapFile);
            }
        }
    }

    return 0;
}
