// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "Storage.h"

#include "../common/Protocol.h"

#include <shlobj.h>

#include <atomic>
#include <memory>
#include <mutex>
#include <unordered_map>

#pragma comment(lib, "Shell32.lib")

namespace SettingsBrokerPrototype
{
    namespace
    {
        std::mutex g_lockMapMutex;
        std::unordered_map<std::wstring, std::weak_ptr<std::mutex>> g_writeLocks;
        std::atomic<unsigned long long> g_tempSequence{ 0 };

        HRESULT EnsureDirectory(const std::wstring& path)
        {
            if (CreateDirectoryW(path.c_str(), nullptr) || GetLastError() == ERROR_ALREADY_EXISTS)
            {
                return S_OK;
            }
            return HRESULT_FROM_WIN32(GetLastError());
        }

        std::shared_ptr<std::mutex> GetWriteLock(const std::wstring& key)
        {
            std::lock_guard guard(g_lockMapMutex);
            auto& weak = g_writeLocks[key];
            auto value = weak.lock();
            if (!value)
            {
                value = std::make_shared<std::mutex>();
                weak = value;
            }
            return value;
        }

        std::wstring UserFolder(const std::wstring& sid)
        {
            return GetStoreRoot() + L"\\" + sid;
        }

        std::wstring NamespaceFolder(const std::wstring& sid, const TrustedTarget& target)
        {
            return UserFolder(sid) + L"\\" + target.nameSpace;
        }

        std::wstring TargetPath(const std::wstring& sid, const TrustedTarget& target)
        {
            return NamespaceFolder(sid, target) + L"\\" + target.fileName;
        }
    }

    std::wstring GetStoreRoot()
    {
        PWSTR programData = nullptr;
        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_ProgramData, 0, nullptr, &programData)))
        {
            std::wstring result(programData);
            CoTaskMemFree(programData);
            return result + L"\\Microsoft\\PowerToys\\SettingsBrokerPrototype\\Store";
        }
        return L"C:\\ProgramData\\Microsoft\\PowerToys\\SettingsBrokerPrototype\\Store";
    }

    HRESULT ReadValue(const std::wstring& callerSid,
                      const TrustedTarget& target,
                      std::vector<BYTE>& bytes)
    {
        bytes.clear();
        const std::wstring path = TargetPath(callerSid, target);
        HANDLE file = CreateFileW(path.c_str(),
                                  GENERIC_READ,
                                  FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                  nullptr,
                                  OPEN_EXISTING,
                                  FILE_ATTRIBUTE_NORMAL,
                                  nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        LARGE_INTEGER size{};
        if (!GetFileSizeEx(file, &size) ||
            size.QuadPart < 0 ||
            size.QuadPart > kMaxPayloadBytes)
        {
            const DWORD error = GetLastError() ? GetLastError() : ERROR_FILE_TOO_LARGE;
            CloseHandle(file);
            return HRESULT_FROM_WIN32(error);
        }

        bytes.resize(static_cast<size_t>(size.QuadPart));
        DWORD read = 0;
        const BOOL ok = bytes.empty() ||
                        ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr);
        const DWORD error = ok ? ERROR_SUCCESS : GetLastError();
        CloseHandle(file);
        if (!ok || read != bytes.size())
        {
            bytes.clear();
            return HRESULT_FROM_WIN32(error ? error : ERROR_READ_FAULT);
        }
        return S_OK;
    }

    HRESULT WriteValue(const std::wstring& callerSid,
                       const TrustedTarget& target,
                       const std::vector<BYTE>& bytes)
    {
        const std::wstring key = callerSid + L":" + std::to_wstring(target.id);
        const auto writeLock = GetWriteLock(key);
        std::lock_guard guard(*writeLock);

        HRESULT result = EnsureDirectory(GetStoreRoot());
        if (FAILED(result))
        {
            return result;
        }
        result = EnsureDirectory(UserFolder(callerSid));
        if (FAILED(result))
        {
            return result;
        }
        result = EnsureDirectory(NamespaceFolder(callerSid, target));
        if (FAILED(result))
        {
            return result;
        }

        const std::wstring targetPath = TargetPath(callerSid, target);
        const auto sequence = ++g_tempSequence;
        const std::wstring tempPath =
            targetPath + L".tmp." + std::to_wstring(GetCurrentProcessId()) + L"." +
            std::to_wstring(GetCurrentThreadId()) + L"." + std::to_wstring(sequence);

        HANDLE file = CreateFileW(tempPath.c_str(),
                                  GENERIC_WRITE,
                                  0,
                                  nullptr,
                                  CREATE_NEW,
                                  FILE_ATTRIBUTE_TEMPORARY | FILE_FLAG_WRITE_THROUGH,
                                  nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        DWORD written = 0;
        BOOL ok = bytes.empty() ||
                  WriteFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr);
        DWORD error = ok ? ERROR_SUCCESS : GetLastError();
        if (ok && written == bytes.size() && !FlushFileBuffers(file))
        {
            ok = FALSE;
            error = GetLastError();
        }
        CloseHandle(file);

        if (!ok || written != bytes.size())
        {
            DeleteFileW(tempPath.c_str());
            return HRESULT_FROM_WIN32(error ? error : ERROR_WRITE_FAULT);
        }

        if (!ReplaceFileW(targetPath.c_str(),
                          tempPath.c_str(),
                          nullptr,
                          REPLACEFILE_WRITE_THROUGH | REPLACEFILE_IGNORE_MERGE_ERRORS,
                          nullptr,
                          nullptr))
        {
            error = GetLastError();
            if (error != ERROR_FILE_NOT_FOUND ||
                !MoveFileExW(tempPath.c_str(), targetPath.c_str(), MOVEFILE_WRITE_THROUGH))
            {
                if (error == ERROR_FILE_NOT_FOUND)
                {
                    error = GetLastError();
                }
                DeleteFileW(tempPath.c_str());
                return HRESULT_FROM_WIN32(error);
            }
        }
        return S_OK;
    }
}
