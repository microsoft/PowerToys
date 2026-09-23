// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "..\ProtectedStorage.Setup\MaintenanceContract.h"
#include <algorithm>

namespace PowerToysProtectedStorage::Maintenance
{
    template<typename Cleanup, typename Clock, typename Wait>
    void RetryPayloadCleanup(Cleanup&& cleanup, Clock&& clock, Wait&& wait, ULONGLONG budget)
    {
        const auto started = clock();
        for (;;)
        {
            try
            {
                cleanup();
                return;
            }
            catch (const failure& error)
            {
                const auto elapsed = clock() - started;
                if ((error.code != ERROR_SHARING_VIOLATION && error.code != ERROR_ACCESS_DENIED) ||
                    elapsed >= budget)
                    throw;
                wait(static_cast<DWORD>((std::min)(ULONGLONG{ 50 }, budget - elapsed)));
            }
        }
    }

    inline void CleanupPayloadOnce(const std::filesystem::path& stage)
    {
        handle directory(CreateFileW(stage.c_str(), DELETE | FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (directory.value == INVALID_HANDLE_VALUE)
        {
            const auto error = GetLastError();
            if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND) return;
            throw failure("open exact payload cleanup directory", error);
        }
        FILE_ATTRIBUTE_TAG_INFO attributes{};
        check(GetFileInformationByHandleEx(directory.value, FileAttributeTagInfo, &attributes, sizeof(attributes)) != FALSE,
            "payload cleanup directory identity");
        if (!(attributes.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) || (attributes.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
            throw failure("unsafe payload cleanup directory", ERROR_INVALID_DATA);
        for (const auto* leaf : { L"Bootstrap.exe", L"Runtime.exe", L"manifest.txt", L"manifest.p7s",
                                 L"ClientCatalog.json", L"ClientCatalog.p7s" })
        {
            const auto path = stage / leaf;
            const auto flags = GetFileAttributesW(path.c_str());
            if (flags == INVALID_FILE_ATTRIBUTES)
            {
                const auto error = GetLastError();
                if (error == ERROR_FILE_NOT_FOUND) continue;
                throw failure("inspect payload cleanup leaf", error);
            }
            if (flags & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT))
                throw failure("unsafe payload cleanup leaf", ERROR_INVALID_DATA);
            check(DeleteFileW(path.c_str()) != FALSE, "remove extracted MSI payload");
        }
        FILE_DISPOSITION_INFO disposition{ TRUE };
        check(SetFileInformationByHandle(directory.value, FileDispositionInfo, &disposition, sizeof(disposition)) != FALSE,
            "retire exact payload cleanup directory");
    }

    inline void CleanupPayloadStage(const std::filesystem::path& stage, ULONGLONG budget = 5000)
    {
        RetryPayloadCleanup([&] { CleanupPayloadOnce(stage); }, [] { return GetTickCount64(); },
            [](DWORD delay) { Sleep(delay); }, budget);
    }
}
