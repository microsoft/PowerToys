// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#include "pch.h"
#include "SilentFilesInUseBAFunctions.h"
#include "BalBaseBAFunctionsProc.h"
#include <common/updating/protectedStorageUpdate.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace
{
    void RemoveOwnerCarrier(HMODULE module)
    {
        try
        {
            HANDLE token = nullptr;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
            {
                BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "Cannot verify the original uninstall owner token (%lu).", GetLastError());
                return;
            }
            TOKEN_ELEVATION elevation{};
            DWORD size = 0;
            const BOOL tokenRead = GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size);
            CloseHandle(token);
            if (!tokenRead || elevation.TokenIsElevated)
            {
                BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "Owner carrier cleanup requires the original ordinary bundle context; no administrator profile will be used.");
                return;
            }
            wchar_t path[32768]{};
            const auto length = GetModuleFileNameW(module, path, ARRAYSIZE(path));
            if (length == 0 || length >= ARRAYSIZE(path))
            {
                BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "Cannot locate the verified owner-cleanup payload (%lu).", GetLastError());
                return;
            }
            const auto logRoot = std::filesystem::path(PTSettingsHelper::get_root_save_folder_location()) / L"Logs" / L"ProtectedStorage";
            std::filesystem::create_directories(logRoot);
            Logger::init("ProtectedStorageBundle", (logRoot / L"bundle-removal.log").wstring(), PTSettingsHelper::get_log_settings_file_location());
            const auto result = updating::RemoveProtectedStorageForOwner(std::filesystem::path(path).parent_path());
            BalLog(result.state == updating::ProtectedStorageSyncState::Completed ? BOOTSTRAPPER_LOG_LEVEL_STANDARD : BOOTSTRAPPER_LOG_LEVEL_ERROR,
                   "Owner carrier removal after main MSI: state=%ls, nativeCode=%lu. Main removal is not rolled back.",
                   updating::ProtectedStorageSyncStateName(result.state), result.nativeCode);
        }
        catch (const winrt::hresult_error& error)
        {
            BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "Owner carrier cleanup deferred: 0x%08x.", static_cast<unsigned>(error.code().value));
        }
        catch (const std::exception& error)
        {
            BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "Owner carrier cleanup deferred: %hs.", error.what());
        }
    }
}


HRESULT WINAPI CreateBAFunctions(
    __in HMODULE hModule,
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    )
{
    HRESULT hr = S_OK;
    CSilentFilesInUseBAFunctions* pBAFunctions = NULL;

    // First thing - log that we're being called
    BalInitialize(pArgs->pEngine);
    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS CALLED *** BA Function DLL is being loaded!");

    pBAFunctions = new CSilentFilesInUseBAFunctions(hModule, [hModule] { RemoveOwnerCarrier(hModule); });
    ExitOnNull(pBAFunctions, hr, E_OUTOFMEMORY, "Failed to create new CSilentFilesInUseBAFunctions object.");

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS *** Created CSilentFilesInUseBAFunctions object");

    hr = pBAFunctions->OnCreate(pArgs->pEngine, pArgs->pCommand);
    ExitOnFailure(hr, "Failed to call OnCreate CPrereqBaf.");

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS *** OnCreate completed successfully");

    pResults->pfnBAFunctionsProc = BalBaseBAFunctionsProc;
    pResults->pvBAFunctionsProcContext = pBAFunctions;
    pBAFunctions = NULL;

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS SUCCESS *** BA Function system initialized");

LExit:
    if (FAILED(hr))
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "*** CREATEBAFUNCTIONS FAILED *** hr=0x%x", hr);
    }
    ReleaseObject(pBAFunctions);

    return hr;
}
