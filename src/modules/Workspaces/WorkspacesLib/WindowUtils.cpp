#include "pch.h"
#include "WindowUtils.h"
#include <filesystem>

#include <appmodel.h>

#include <shellapi.h>
#include <ShlObj.h>
#include <shobjidl.h>
#include <tlhelp32.h>
#include <wrl.h>
#include <propkey.h>

#include <wil/com.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/CommandLineArgsHelper.h>
#include <WorkspacesLib/StringUtils.h>

namespace Utils
{
    std::wstring GetAUMIDFromProcessId(DWORD processId)
    {
        HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (hProcess == NULL)
        {
            Logger::error(L"Failed to open process handle. Error: {}", get_last_error_or_default(GetLastError()));
            return {};
        }

        // Get the package full name for the process
        UINT32 packageFullNameLength = 0;
        LONG rc = GetPackageFullName(hProcess, &packageFullNameLength, nullptr);
        if (rc != ERROR_INSUFFICIENT_BUFFER)
        {
            Logger::error(L"Failed to get package full name length. Error code: {}", rc);
            CloseHandle(hProcess);
            return {};
        }

        std::vector<wchar_t> packageFullName(packageFullNameLength);
        rc = GetPackageFullName(hProcess, &packageFullNameLength, packageFullName.data());
        if (rc != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to get package full name. Error code: {}", rc);
            CloseHandle(hProcess);
            return {};
        }

        // Get the AUMID for the package
        UINT32 appModelIdLength = 0;
        rc = GetApplicationUserModelId(hProcess, &appModelIdLength, nullptr);
        if (rc != ERROR_INSUFFICIENT_BUFFER)
        {
            Logger::error(L"Failed to get AppUserModelId length. Error code: {}", rc);
            CloseHandle(hProcess);
            return {};
        }

        std::vector<wchar_t> appModelId(appModelIdLength);
        rc = GetApplicationUserModelId(hProcess, &appModelIdLength, appModelId.data());
        if (rc != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to get AppUserModelId. Error code: {}", rc);
            CloseHandle(hProcess);
            return {};
        }

        CloseHandle(hProcess);
        return std::wstring(appModelId.data());
    }

    std::wstring GetAUMIDFromWindow(HWND hwnd)
    {
        std::wstring result{};
        if (hwnd == NULL)
        {
            return result;
        }

        Microsoft::WRL::ComPtr<IPropertyStore> propertyStore;
        HRESULT hr = SHGetPropertyStoreForWindow(hwnd, IID_PPV_ARGS(&propertyStore));
        if (FAILED(hr))
        {
            return result;
        }

        PROPVARIANT propVariant;
        PropVariantInit(&propVariant);

        hr = propertyStore->GetValue(PKEY_AppUserModel_ID, &propVariant);
        if (SUCCEEDED(hr) && propVariant.vt == VT_LPWSTR && propVariant.pwszVal != nullptr)
        {
            result = propVariant.pwszVal;
        }

        PropVariantClear(&propVariant);

        Logger::info(L"Found a window with aumid {}", result);
        return result;
    }
}