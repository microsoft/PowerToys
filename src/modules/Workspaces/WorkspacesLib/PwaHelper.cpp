#include "pch.h"
#include "PwaHelper.h"

#include "AppUtils.h"

#include <filesystem>
#include <appmodel.h>
#include <shobjidl.h>
#include <propkey.h>
#include <wrl.h>
#include <ShlObj.h>
#include <tlhelp32.h>
#include <winternl.h>
#include <shellapi.h>

#include <wil/result_macros.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#pragma comment(lib, "ntdll.lib")

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
    }

    std::wstring GetProcCommandLine(DWORD pid)
    {
        std::wstring commandLine;

        // Open a handle to the process
        const HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
        if (process == NULL)
        {
            Logger::error(L"Failed to open the process, error: {}", get_last_error_or_default(GetLastError()));
        }
        else
        {
            // Get the address of the ProcessEnvironmentBlock
            PROCESS_BASIC_INFORMATION pbi = {};
            NTSTATUS status = NtQueryInformationProcess(process, ProcessBasicInformation, &pbi, sizeof(pbi), NULL);
            if (status != STATUS_SUCCESS)
            {
                Logger::error(L"Failed to query the process, error: {}", status);
            }
            else
            {
                // Get the address of the process parameters in the ProcessEnvironmentBlock
                PEB processEnvironmentBlock = {};
                if (!ReadProcessMemory(process, pbi.PebBaseAddress, &processEnvironmentBlock, sizeof(processEnvironmentBlock), NULL))
                {
                    Logger::error(L"Failed to read the process ProcessEnvironmentBlock, error: {}", get_last_error_or_default(GetLastError()));
                }
                else
                {
                    // Get the command line arguments from the process parameters
                    RTL_USER_PROCESS_PARAMETERS params = {};
                    if (!ReadProcessMemory(process, processEnvironmentBlock.ProcessParameters, &params, sizeof(params), NULL))
                    {
                        Logger::error(L"Failed to read the process params, error: {}", get_last_error_or_default(GetLastError()));
                    }
                    else
                    {
                        UNICODE_STRING& commandLineArgs = params.CommandLine;
                        std::vector<WCHAR> buffer(commandLineArgs.Length / sizeof(WCHAR));
                        if (!ReadProcessMemory(process, commandLineArgs.Buffer, buffer.data(), commandLineArgs.Length, NULL))
                        {
                            Logger::error(L"Failed to read the process command line, error: {}", get_last_error_or_default(GetLastError()));
                        }
                        else
                        {
                            commandLine.assign(buffer.data(), buffer.size());
                        }
                    }
                }
            }

            CloseHandle(process);
        }

        return commandLine;
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

    void PwaHelper::InitAumidToAppId()
    {
        if (m_pwaAumidToAppId.size() > 0)
        {
            return;
        }

        const auto pwaHelperProcessIds = FindPwaHelperProcessIds();
        Logger::info(L"Found {} edge Pwa helper processes", pwaHelperProcessIds.size());
        for (const auto subProcessID : pwaHelperProcessIds)
        {
            std::wstring aumidID = GetAUMIDFromProcessId(subProcessID);
            std::wstring commandLineArg = GetProcCommandLine(subProcessID);
            auto appIdIndexStart = commandLineArg.find(NonLocalizable::EdgeAppIdIdentifier);
            if (appIdIndexStart != std::wstring::npos)
            {
                commandLineArg = commandLineArg.substr(appIdIndexStart + NonLocalizable::EdgeAppIdIdentifier.size());
                auto appIdIndexEnd = commandLineArg.find(L" ");
                if (appIdIndexEnd != std::wstring::npos)
                {
                    commandLineArg = commandLineArg.substr(0, appIdIndexEnd);
                }
            }
            std::wstring appId{ commandLineArg };
            m_pwaAumidToAppId.insert(std::map<std::wstring, std::wstring>::value_type(aumidID, appId));
            Logger::info(L"Found an edge Pwa helper process with AumidID {} and PwaAppId {}", aumidID, appId);

            PWSTR path = NULL;
            HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &path);
            if (SUCCEEDED(hres))
            {
                std::filesystem::path fsPath(path);
                fsPath /= NonLocalizable::EdgeBase;
                for (const auto& directory : std::filesystem::directory_iterator(fsPath))
                {
                    if (directory.is_directory())
                    {
                        const std::filesystem::path directoryName = directory.path().filename();
                        if (directoryName.wstring().find(NonLocalizable::EdgeDirPrefix) == 0)
                        {
                            const std::wstring appIdDir = directoryName.wstring().substr(NonLocalizable::EdgeDirPrefix.size());
                            if (appIdDir == appId)
                            {
                                for (const auto& filename : std::filesystem::directory_iterator(directory))
                                {
                                    if (!filename.is_directory())
                                    {
                                        const std::filesystem::path filenameString = filename.path().filename();
                                        if (filenameString.extension().wstring() == L".ico")
                                        {
                                            m_pwaAppIdsToAppNames.insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
                                            Logger::info(L"Storing an edge Pwa app name {} for PwaAppId {}", filenameString.stem().wstring(), appId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                CoTaskMemFree(path);
            }
        }
    }

    std::optional<std::wstring> PwaHelper::GetPwaAppId(const std::wstring& windowAumid) const
    {
        const auto pwaIndex = m_pwaAumidToAppId.find(windowAumid);
        if (pwaIndex != m_pwaAumidToAppId.end())
        {
            return pwaIndex->second;
        }

        return std::nullopt;
        ;
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

    void PwaHelper::InitChromeAppIds()
    {
        if (m_chromeAppIds.size() > 0)
        {
            return;
        }

        PWSTR path = NULL;
        HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &path);
        if (SUCCEEDED(hres))
        {
            std::filesystem::path fsPath(path);
            fsPath /= NonLocalizable::ChromeBase;
            for (const auto& directory : std::filesystem::directory_iterator(fsPath))
            {
                if (directory.is_directory())
                {
                    const std::filesystem::path directoryName = directory.path().filename();
                    if (directoryName.wstring().find(NonLocalizable::ChromeDirPrefix) == 0)
                    {
                        const std::wstring appId = directoryName.wstring().substr(NonLocalizable::ChromeDirPrefix.size());
                        m_chromeAppIds.push_back(appId);
                        for (const auto& filename : std::filesystem::directory_iterator(directory))
                        {
                            if (!filename.is_directory())
                            {
                                const std::filesystem::path filenameString = filename.path().filename();
                                if (filenameString.extension().wstring() == L".ico")
                                {
                                    m_pwaAppIdsToAppNames.insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
                                    Logger::info(L"Found an installed chrome Pwa app {} with PwaAppId {}", filenameString.stem().wstring(), appId);
                                }
                            }
                        }
                    }
                }
            }
            CoTaskMemFree(path);
        }
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

        PROPVARIANT propvar;
        PropVariantInit(&propvar);

        hr = propertyStore->GetValue(PKEY_AppUserModel_ID, &propvar);
        if (SUCCEEDED(hr) && propvar.vt == VT_LPWSTR && propvar.pwszVal != nullptr)
        {
            result = propvar.pwszVal;
        }

        PropVariantClear(&propvar);
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

    std::optional<std::wstring> PwaHelper::SearchPwaAppId(const std::wstring& windowAumid) const
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

    void PwaHelper::UpdatePwaApp(Utils::Apps::AppData* appData, HWND window)
    {
        std::optional<std::wstring> pwaAppId = std::nullopt;
        std::wstring finalName = appData->name;
        std::wstring pwaName = L"";
        if (appData->IsEdge())
        {
            InitAumidToAppId();

            std::wstring windowAumid = GetAUMIDFromWindow(window);
            Logger::info(L"Found an edge window with aumid {}", windowAumid);

            pwaAppId = GetPwaAppId(windowAumid);
            if (pwaAppId.has_value())
            {
                Logger::info(L"The found edge window is a PWA app with appId {}", pwaAppId.value());
                pwaName = SearchPwaName(pwaAppId.value(), windowAumid);
                Logger::info(L"The found edge window is a PWA app with name {}", pwaName);
                finalName = pwaName + L" (" + finalName + L")";
            }
            else
            {
                Logger::info(L"The found edge window does not contain a PWA app");
            }
        }
        else if (appData->IsChrome())
        {
            InitChromeAppIds();

            std::wstring windowAumid = GetAUMIDFromWindow(window);
            Logger::info(L"Found a chrome window with aumid {}", windowAumid);

            pwaAppId = SearchPwaAppId(windowAumid);
            if (pwaAppId.has_value())
            {
                pwaName = SearchPwaName(pwaAppId.value(), windowAumid);
                finalName = pwaName + L" (" + finalName + L")";
            }
        }

        appData->name = finalName;
        appData->pwaAppId = pwaAppId.has_value() ? pwaAppId.value() : L"";
    }
}