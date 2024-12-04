#include "pch.h"
#include "PwaHelper.h"

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
    namespace NonLocalizable
    {
        const std::wstring EdgeAppIdIdentifier = L"--app-id=";
        const std::wstring ChromeAppIdIdentifier = L"Chrome._crx_";
        const std::wstring ChromeBase = L"Google\\Chrome\\User Data\\Default\\Web Applications";
        const std::wstring EdgeBase = L"Microsoft\\Edge\\User Data\\Default\\Web Applications";
        const std::wstring ChromeDirPrefix = L"_crx_";
        const std::wstring EdgeDirPrefix = L"_crx__";
        const std::wstring IcoExtension = L".ico";
    }

    static const std::wstring& GetLocalAppDataFolder()
    {
        static std::wstring localFolder{};

        if (localFolder.empty())
        {
            wil::unique_cotaskmem_string folderPath;
            HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &folderPath);
            if (SUCCEEDED(hres))
            {
                localFolder = folderPath.get();
            }
            else
            {
                Logger::error(L"Failed to get the local app data folder path: {}", get_last_error_or_default(hres));
                localFolder = L""; // Ensure it is explicitly set to empty on failure
            }
        }
        
        return localFolder;
    }

    // Finds all PwaHelper.exe processes with the specified parent process ID
    std::vector<DWORD> FindPwaHelperProcessIds()
    {
        std::vector<DWORD> pwaHelperProcessIds;
        const HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot == INVALID_HANDLE_VALUE)
        {
            Logger::info(L"Invalid handle when creating snapshot for the search for PwaHelper processes");
            return pwaHelperProcessIds;
        }

        PROCESSENTRY32 pe;
        pe.dwSize = sizeof(PROCESSENTRY32);

        if (Process32First(hSnapshot, &pe))
        {
            do
            {
                if (_wcsicmp(pe.szExeFile, L"PwaHelper.exe") == 0)
                {
                    Logger::info(L"Found a PWA process with id {}", pe.th32ProcessID);
                    pwaHelperProcessIds.push_back(pe.th32ProcessID);
                }
            } while (Process32Next(hSnapshot, &pe));
        }

        CloseHandle(hSnapshot);
        return pwaHelperProcessIds;
    }

    PwaHelper::PwaHelper()
    {
        InitChromeAppIds();
        InitEdgeAppIds();
    }

    void PwaHelper::InitAppIds(const std::wstring& browserDataFolder, const std::wstring& browserDirPrefix, const std::function<void(const std::wstring&)>& addingAppIdCallback)
    {
        std::filesystem::path folderPath(GetLocalAppDataFolder());
        folderPath.append(browserDataFolder);
        if (!std::filesystem::exists(folderPath))
        {
            Logger::info(L"Edge base path does not exist: {}", folderPath.wstring());
            return;
        }

        try
        {
            for (const auto& directory : std::filesystem::directory_iterator(folderPath))
            {
                if (!directory.is_directory())
                {
                    continue;
                }

                const std::wstring directoryName = directory.path().filename();
                if (directoryName.find(browserDirPrefix) != 0)
                {
                    continue;
                }

                const std::wstring appId = directoryName.substr(browserDirPrefix.length());
                if (addingAppIdCallback)
                {
                    addingAppIdCallback(appId);
                }

                for (const auto& filename : std::filesystem::directory_iterator(directory))
                {
                    if (!filename.is_directory())
                    {
                        const std::filesystem::path filenameString = filename.path().filename();
                        if (StringUtils::CaseInsensitiveEquals(filenameString.extension(), NonLocalizable::IcoExtension))
                        {
                            const auto stem = filenameString.stem().wstring();
                            m_pwaAppIdsToAppNames.insert({ appId, stem });
                            Logger::info(L"Found an installed Pwa app {} with PwaAppId {}", stem, appId);
                            break;
                        }
                    }
                }
            }
        }
        catch (std::exception& ex)
        {
            Logger::error("Failed to iterate over the directory: {}", ex.what());
        }
    }

    void PwaHelper::InitEdgeAppIds()
    {
        if (!m_edgeAppIds.empty())
        {
            // already initialized
            return;
        }

        CommandLineArgsHelper commandLineArgsHelper{};

        const auto pwaHelperProcessIds = FindPwaHelperProcessIds();
        Logger::info(L"Found {} edge Pwa helper processes", pwaHelperProcessIds.size());

        for (const auto subProcessID : pwaHelperProcessIds)
        {
            std::wstring aumidID = GetAUMIDFromProcessId(subProcessID);
            std::wstring commandLineArg = commandLineArgsHelper.GetCommandLineArgs(subProcessID);
            std::wstring appId = GetAppIdFromCommandLineArgs(commandLineArg);

            m_edgeAppIds.insert({ aumidID, appId });
            Logger::info(L"Found an edge Pwa helper process with AumidID {} and PwaAppId {}", aumidID, appId);
        }

        InitAppIds(NonLocalizable::EdgeBase, NonLocalizable::EdgeDirPrefix, [&](const std::wstring&) {});
    }

    void PwaHelper::InitChromeAppIds()
    {
        if (!m_chromeAppIds.empty())
        {
            // already initialized
            return;
        }

        InitAppIds(NonLocalizable::ChromeBase, NonLocalizable::ChromeDirPrefix, [&](const std::wstring& appId) {
            m_chromeAppIds.push_back(appId);
        });
    }

    std::optional<std::wstring> PwaHelper::GetEdgeAppId(const std::wstring& windowAumid) const
    {
        const auto pwaIndex = m_edgeAppIds.find(windowAumid);
        if (pwaIndex != m_edgeAppIds.end())
        {
            return pwaIndex->second;
        }

        return std::nullopt;
    }
    
    std::optional<std::wstring> PwaHelper::GetChromeAppId(const std::wstring& windowAumid) const
    {
        const auto appIdIndexStart = windowAumid.find(NonLocalizable::ChromeAppIdIdentifier);
        if (appIdIndexStart != std::wstring::npos)
        {
            std::wstring windowAumidSub = windowAumid.substr(appIdIndexStart + NonLocalizable::ChromeAppIdIdentifier.size());
            const auto appIdIndexEnd = windowAumidSub.find(L" ");
            if (appIdIndexEnd != std::wstring::npos)
            {
                windowAumidSub = windowAumidSub.substr(0, appIdIndexEnd);
            }

            const std::wstring windowAumidBegin = windowAumidSub.substr(0, 10);
            for (const auto chromeAppId : m_chromeAppIds)
            {
                if (chromeAppId.find(windowAumidBegin) == 0)
                {
                    return chromeAppId;
                }
            }
        }

        return std::nullopt;
    }

    std::wstring PwaHelper::SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const
    {
        const auto index = m_pwaAppIdsToAppNames.find(pwaAppId);
        if (index != m_pwaAppIdsToAppNames.end())
        {
            return index->second;
        }

        std::wstring nameFromAumid{ windowAumid };
        const std::size_t delimiterPos = nameFromAumid.find(L"-");
        if (delimiterPos != std::string::npos)
        {
            return nameFromAumid.substr(0, delimiterPos);
        }

        return nameFromAumid;
    }

    std::wstring PwaHelper::GetAppIdFromCommandLineArgs(const std::wstring& commandLineArgs) const
    {
        auto result = commandLineArgs;

        // remove the prefix
        if (result.find(NonLocalizable::EdgeAppIdIdentifier) == 0)
        {
            result.erase(0, NonLocalizable::EdgeAppIdIdentifier.length());
        }

        // remove the suffix
        auto appIdIndexEnd = result.find(L" ");
        if (appIdIndexEnd != std::wstring::npos)
        {
            result = result.substr(0, appIdIndexEnd);
        }

        return result;
    }

    std::wstring PwaHelper::GetAUMIDFromWindow(HWND hwnd) const
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

    std::wstring PwaHelper::GetAUMIDFromProcessId(DWORD processId) const
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
}