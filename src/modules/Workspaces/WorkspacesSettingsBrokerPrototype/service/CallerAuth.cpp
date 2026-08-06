// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "CallerAuth.h"

#include <sddl.h>

#include <memory>
#include <vector>

namespace SettingsBrokerPrototype
{
    namespace
    {
        struct HandleCloser
        {
            void operator()(void* value) const noexcept
            {
                if (value && value != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(static_cast<HANDLE>(value));
                }
            }
        };

        using unique_handle = std::unique_ptr<void, HandleCloser>;

        std::wstring Basename(const std::wstring& path)
        {
            const auto slash = path.find_last_of(L"\\/");
            return slash == std::wstring::npos ? path : path.substr(slash + 1);
        }

        std::wstring ParentPath(const std::wstring& path)
        {
            const auto slash = path.find_last_of(L"\\/");
            return slash == std::wstring::npos ? std::wstring{} : path.substr(0, slash);
        }

        HRESULT NormalizeLocalDosPath(const std::wstring& input,
                                      std::wstring& path)
        {
            path = input;
            if (path.rfind(L"\\\\?\\", 0) == 0)
            {
                path.erase(0, 4);
            }
            while (path.size() > 3 && path.back() == L'\\')
            {
                path.pop_back();
            }

            const bool hasDrive =
                path.size() >= 3 &&
                ((path[0] >= L'A' && path[0] <= L'Z') ||
                 (path[0] >= L'a' && path[0] <= L'z')) &&
                path[1] == L':' &&
                path[2] == L'\\';
            if (!hasDrive || path.find(L'/') != std::wstring::npos)
            {
                return E_ACCESSDENIED;
            }

            size_t componentStart = 3;
            while (componentStart < path.size())
            {
                const size_t separator = path.find(L'\\', componentStart);
                const size_t componentLength =
                    (separator == std::wstring::npos ? path.size() : separator) -
                    componentStart;
                if (componentLength == 0 ||
                    (componentLength == 1 && path[componentStart] == L'.') ||
                    (componentLength == 2 &&
                     path[componentStart] == L'.' &&
                     path[componentStart + 1] == L'.') ||
                    path.find(L':', componentStart) <
                        (separator == std::wstring::npos ? path.size() : separator))
                {
                    return E_ACCESSDENIED;
                }
                if (separator == std::wstring::npos)
                {
                    break;
                }
                componentStart = separator + 1;
            }
            return S_OK;
        }

        HRESULT FinalPath(HANDLE handle, std::wstring& path)
        {
            DWORD chars = GetFinalPathNameByHandleW(handle,
                                                    nullptr,
                                                    0,
                                                    FILE_NAME_NORMALIZED |
                                                        VOLUME_NAME_DOS);
            if (chars == 0)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::vector<wchar_t> buffer(static_cast<size_t>(chars) + 1);
            chars = GetFinalPathNameByHandleW(handle,
                                              buffer.data(),
                                              static_cast<DWORD>(buffer.size()),
                                              FILE_NAME_NORMALIZED |
                                                  VOLUME_NAME_DOS);
            if (chars == 0 || chars >= buffer.size())
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            path.assign(buffer.data(), chars);
            if (path.rfind(L"\\\\?\\", 0) == 0)
            {
                path.erase(0, 4);
            }
            while (path.size() > 3 && path.back() == L'\\')
            {
                path.pop_back();
            }
            return S_OK;
        }

        HRESULT TrustedBinPath(std::wstring& path)
        {
            DWORD chars = GetEnvironmentVariableW(L"ProgramData", nullptr, 0);
            if (chars == 0)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::vector<wchar_t> programData(chars);
            chars = GetEnvironmentVariableW(L"ProgramData",
                                             programData.data(),
                                             static_cast<DWORD>(programData.size()));
            if (chars == 0 || chars >= programData.size())
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::wstring bin(programData.data(), chars);
            bin += L"\\Microsoft\\PowerToys\\SettingsBrokerPrototype\\Bin";
            HANDLE rawDirectory = CreateFileW(bin.c_str(),
                                              FILE_READ_ATTRIBUTES,
                                              FILE_SHARE_READ | FILE_SHARE_WRITE |
                                                  FILE_SHARE_DELETE,
                                              nullptr,
                                              OPEN_EXISTING,
                                              FILE_FLAG_BACKUP_SEMANTICS,
                                              nullptr);
            if (rawDirectory == INVALID_HANDLE_VALUE)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            unique_handle directory(rawDirectory);
            return FinalPath(directory.get(), path);
        }

        HRESULT ValidateImagePath(const std::wstring& queriedPath,
                                  CallerIdentity& identity)
        {
            std::wstring trustedBin;
            HRESULT result = TrustedBinPath(trustedBin);
            if (FAILED(result))
            {
                return result;
            }
            std::wstring normalizedTrustedBin;
            result = NormalizeLocalDosPath(trustedBin, normalizedTrustedBin);
            if (FAILED(result))
            {
                return result;
            }

            std::wstring normalizedQueriedPath;
            result = NormalizeLocalDosPath(queriedPath, normalizedQueriedPath);
            if (FAILED(result) ||
                _wcsicmp(ParentPath(normalizedQueriedPath).c_str(),
                         normalizedTrustedBin.c_str()) != 0)
            {
                return E_ACCESSDENIED;
            }
            const CallerBinding* binding =
                FindCallerBinding(Basename(normalizedQueriedPath));
            if (!binding)
            {
                return E_ACCESSDENIED;
            }

            HANDLE rawImage = CreateFileW(normalizedQueriedPath.c_str(),
                                          FILE_READ_ATTRIBUTES,
                                          FILE_SHARE_READ | FILE_SHARE_WRITE |
                                              FILE_SHARE_DELETE,
                                          nullptr,
                                          OPEN_EXISTING,
                                          FILE_ATTRIBUTE_NORMAL,
                                          nullptr);
            if (rawImage == INVALID_HANDLE_VALUE)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            unique_handle image(rawImage);

            std::wstring finalPath;
            result = FinalPath(image.get(), finalPath);
            if (FAILED(result))
            {
                return E_ACCESSDENIED;
            }
            std::wstring normalizedFinalPath;
            result = NormalizeLocalDosPath(finalPath, normalizedFinalPath);
            if (FAILED(result) ||
                _wcsicmp(normalizedFinalPath.c_str(),
                         normalizedQueriedPath.c_str()) != 0)
            {
                return E_ACCESSDENIED;
            }

            identity.imagePath = std::move(normalizedFinalPath);
            identity.binding = binding;
            return S_OK;
        }

        HRESULT ReadAndValidateSid(HANDLE token, std::wstring& sidString)
        {
            DWORD bytes = 0;
            GetTokenInformation(token, TokenUser, nullptr, 0, &bytes);
            if (bytes == 0)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }

            std::vector<BYTE> buffer(bytes);
            if (!GetTokenInformation(token, TokenUser, buffer.data(), bytes, &bytes))
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }

            PSID sid = reinterpret_cast<TOKEN_USER*>(buffer.data())->User.Sid;
            constexpr WELL_KNOWN_SID_TYPE rejected[] = {
                WinLocalSystemSid,
                WinLocalServiceSid,
                WinNetworkServiceSid,
                WinAnonymousSid,
                WinNullSid,
            };
            for (const auto type : rejected)
            {
                if (IsWellKnownSid(sid, type))
                {
                    return E_ACCESSDENIED;
                }
            }

            LPWSTR text = nullptr;
            if (!ConvertSidToStringSidW(sid, &text))
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            sidString.assign(text);
            LocalFree(text);
            return sidString.empty() ? E_ACCESSDENIED : S_OK;
        }
    }

    HRESULT AuthenticateCaller(HANDLE pipe, CallerIdentity& identity)
    {
        identity = {};

        ULONG processId = 0;
        if (!GetNamedPipeClientProcessId(pipe, &processId) || processId == 0)
        {
            return E_ACCESSDENIED;
        }

        if (!ImpersonateNamedPipeClient(pipe))
        {
            return E_ACCESSDENIED;
        }

        HRESULT result = E_ACCESSDENIED;
        HANDLE rawToken = nullptr;
        HANDLE rawProcess = nullptr;

        if (OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &rawToken))
        {
            unique_handle token(rawToken);
            result = ReadAndValidateSid(token.get(), identity.sid);
            if (SUCCEEDED(result))
            {
                rawProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
                if (rawProcess)
                {
                    unique_handle process(rawProcess);
                    std::vector<wchar_t> image(32768);
                    DWORD chars = static_cast<DWORD>(image.size());
                    if (QueryFullProcessImageNameW(process.get(), 0, image.data(), &chars))
                    {
                        identity.processId = processId;
                        result = ValidateImagePath(
                            std::wstring(image.data(), chars),
                            identity);
                    }
                }
            }
        }

        if (!RevertToSelf())
        {
            const DWORD error = GetLastError();
            TerminateProcess(GetCurrentProcess(), error);
            ExitProcess(error);
        }
        return result;
    }
}
