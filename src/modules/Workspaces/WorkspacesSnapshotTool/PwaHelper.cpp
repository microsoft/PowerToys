#include "pch.h"
#include "PwaHelper.h"
#include <comdef.h>
#include <Wbemidl.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/notifications/NotificationUtil.h>

#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>

#include <WorkspacesLib/AppUtils.h>
#include <tlhelp32.h>
#include <winternl.h>
#include <initguid.h>

namespace SnapshotUtils
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
    // {c8900b66-a973-584b-8cae-355b7f55341b}
    DEFINE_GUID(CLSID_StartMenuCacheAndAppResolver, 0x660b90c8, 0x73a9, 0x4b58, 0x8c, 0xae, 0x35, 0x5b, 0x7f, 0x55, 0x34, 0x1b);

    // {46a6eeff-908e-4dc6-92a6-64be9177b41c}
    DEFINE_GUID(IID_IAppResolver_7, 0x46a6eeff, 0x908e, 0x4dc6, 0x92, 0xa6, 0x64, 0xbe, 0x91, 0x77, 0xb4, 0x1c);

    // {de25675a-72de-44b4-9373-05170450c140}
    DEFINE_GUID(IID_IAppResolver_8, 0xde25675a, 0x72de, 0x44b4, 0x93, 0x73, 0x05, 0x17, 0x04, 0x50, 0xc1, 0x40);

    struct IAppResolver_7 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForShortcut() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForWindow(HWND hWnd, WCHAR** pszAppId, void* pUnknown1, void* pUnknown2, void* pUnknown3) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForProcess(DWORD dwProcessId, WCHAR** pszAppId, void* pUnknown1, void* pUnknown2, void* pUnknown3) = 0;
    };

    struct IAppResolver_8 : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForShortcut() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForShortcutObject() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForWindow(HWND hWnd, WCHAR** pszAppId, void* pUnknown1, void* pUnknown2, void* pUnknown3) = 0;
        virtual HRESULT STDMETHODCALLTYPE GetAppIDForProcess(DWORD dwProcessId, WCHAR** pszAppId, void* pUnknown1, void* pUnknown2, void* pUnknown3) = 0;
    };

    BOOL GetAppId_7(HWND hWnd, std::wstring* result)
    {
        HRESULT hr;

        IAppResolver_7* AppResolver;
        hr = CoCreateInstance(CLSID_StartMenuCacheAndAppResolver, NULL, CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER, IID_IAppResolver_7, reinterpret_cast<void**>(&AppResolver));
        if (SUCCEEDED(hr))
        {
            WCHAR* pszAppId;
            hr = AppResolver->GetAppIDForWindow(hWnd, &pszAppId, NULL, NULL, NULL);
            if (SUCCEEDED(hr))
            {
                *result = std::wstring(pszAppId);
                CoTaskMemFree(pszAppId);
            }

            AppResolver->Release();
        }

        return SUCCEEDED(hr);
    }

    BOOL GetAppId_8(HWND hWnd, std::wstring* result)
    {
        HRESULT hr;
        *result = L"";

        IAppResolver_8* AppResolver;
        hr = CoCreateInstance(CLSID_StartMenuCacheAndAppResolver, NULL, CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER, IID_IAppResolver_8, reinterpret_cast<void**>(&AppResolver));
        if (SUCCEEDED(hr))
        {
            WCHAR* pszAppId;
            hr = AppResolver->GetAppIDForWindow(hWnd, &pszAppId, NULL, NULL, NULL);
            if (SUCCEEDED(hr))
            {
                *result = std::wstring(pszAppId);
                CoTaskMemFree(pszAppId);
            }

            AppResolver->Release();
        }

        return SUCCEEDED(hr);
    }

    BOOL PwaHelper::GetAppId(HWND hWnd, std::wstring* result)
    {
        HRESULT hr = GetAppId_8(hWnd, result);
        if (!SUCCEEDED(hr))
        {
            hr = GetAppId_7(hWnd, result);
        }
        return SUCCEEDED(hr);
    }

    BOOL GetProcessId_7(DWORD dwProcessId, std::wstring* result)
    {
        HRESULT hr;
        *result = L"";

        IAppResolver_7* AppResolver;
        hr = CoCreateInstance(CLSID_StartMenuCacheAndAppResolver, NULL, CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER, IID_IAppResolver_7, reinterpret_cast<void**>(&AppResolver));
        if (SUCCEEDED(hr))
        {
            WCHAR* pszAppId;
            hr = AppResolver->GetAppIDForProcess(dwProcessId, &pszAppId, NULL, NULL, NULL);
            if (SUCCEEDED(hr))
            {
                *result = std::wstring(pszAppId);
                CoTaskMemFree(pszAppId);
            }

            AppResolver->Release();
        }

        return SUCCEEDED(hr);
    }

    BOOL GetProcessId_8(DWORD dwProcessId, std::wstring* result)
    {
        HRESULT hr;
        *result = L"";

        IAppResolver_8* AppResolver;
        hr = CoCreateInstance(CLSID_StartMenuCacheAndAppResolver, NULL, CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER, IID_IAppResolver_8, reinterpret_cast<void**>(&AppResolver));
        if (SUCCEEDED(hr))
        {
            WCHAR* pszAppId;
            hr = AppResolver->GetAppIDForProcess(dwProcessId, &pszAppId, NULL, NULL, NULL);
            if (SUCCEEDED(hr))
            {
                *result = std::wstring(pszAppId);
                CoTaskMemFree(pszAppId);
            }

            AppResolver->Release();
        }

        return SUCCEEDED(hr);
    }

    BOOL GetProcessId(DWORD dwProcessId, std::wstring* result)
    {
        HRESULT hr = GetProcessId_8(dwProcessId, result);
        if (!SUCCEEDED(hr))
        {
            hr = GetProcessId_7(dwProcessId, result);
        }
        return SUCCEEDED(hr);
    }

    std::map<std::wstring, std::wstring> pwaAumidToAppId;
    std::vector<std::wstring> chromeAppIds;
    std::map<std::wstring, std::wstring> pwaAppIdsToAppNames;

    std::wstring GetProcCommandLine(DWORD pid)
    {
        std::wstring commandLine;

        // Open a handle to the process
        HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
        if (process == NULL)
        {
            DWORD err = GetLastError();
            Logger::error(L"Failed to open the process, error: {}", err);
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
                    DWORD err = GetLastError();
                    Logger::error(L"Failed to read the process ProcessEnvironmentBlock, error: {}", err);
                }
                else
                {
                    // Get the command line arguments from the process parameters
                    RTL_USER_PROCESS_PARAMETERS params = {};
                    if (!ReadProcessMemory(process, processEnvironmentBlock.ProcessParameters, &params, sizeof(params), NULL))
                    {
                        DWORD err = GetLastError();
                        Logger::error(L"Failed to read the process params, error: {}", err);
                    }
                    else
                    {
                        UNICODE_STRING& commandLineArgs = params.CommandLine;
                        std::vector<WCHAR> buffer(commandLineArgs.Length / sizeof(WCHAR));
                        if (!ReadProcessMemory(process, commandLineArgs.Buffer, buffer.data(), commandLineArgs.Length, NULL))
                        {
                            DWORD err = GetLastError();
                            Logger::error(L"Failed to read the process command line, error: {}", err);
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
        HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
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

    void PwaHelper::InitAumidToAppId(DWORD pid)
    {
        if (pwaAumidToAppId.size() > 0)
        {
            return;
        }

        auto pwaHelperProcessIds = FindPwaHelperProcessIds();
        Logger::info(L"Found {} edge Pwa helper processes", pwaHelperProcessIds.size());
        for (auto subProcessID : pwaHelperProcessIds)
        {
            std::wstring aumidID;
            GetProcessId(subProcessID, &aumidID);
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
            pwaAumidToAppId.insert(std::map<std::wstring, std::wstring>::value_type(aumidID, appId));
            Logger::info(L"Found an edge Pwa helper process with AumidID {} and PwaAppId {}", aumidID, appId);

            PWSTR path = NULL;
            HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &path);
            if (SUCCEEDED(hres))
            {
                std::filesystem::path fsPath(path);
                fsPath /= NonLocalizable::EdgeBase;
                for (auto& directory : std::filesystem::directory_iterator(fsPath))
                {
                    if (directory.is_directory())
                    {
                        std::filesystem::path directoryName = directory.path().filename();
                        if (directoryName.wstring().find(NonLocalizable::EdgeDirPrefix) == 0)
                        {
                            std::wstring appIdDir = directoryName.wstring().substr(NonLocalizable::EdgeDirPrefix.size());
                            if (appIdDir == appId)
                            {
                                for (auto& filename : std::filesystem::directory_iterator(directory))
                                {
                                    if (!filename.is_directory())
                                    {
                                        std::filesystem::path filenameString = filename.path().filename();
                                        if (filenameString.extension().wstring() == L".ico")
                                        {
                                            pwaAppIdsToAppNames.insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
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

    BOOL PwaHelper::GetPwaAppId(std::wstring windowAumid, std::wstring* result)
    {
        auto pwaIndex = pwaAumidToAppId.find(windowAumid);
        if (pwaIndex != pwaAumidToAppId.end())
        {
            *result = pwaIndex->second;
            return true;
        }

        return false;
    }

    BOOL PwaHelper::SearchPwaName(std::wstring pwaAppId, std::wstring windowAumid, std::wstring* pwaName)
    {
        auto index = pwaAppIdsToAppNames.find(pwaAppId);
        if (index != pwaAppIdsToAppNames.end())
        {
            *pwaName = index->second;
            return true;
        }

        std::wstring nameFromAumid{ windowAumid };
        std::size_t delimiterPos = nameFromAumid.find(L"-");
        if (delimiterPos != std::string::npos)
        {
            nameFromAumid = nameFromAumid.substr(0, delimiterPos);
        }

        *pwaName = nameFromAumid;
        return false;
    }

    void PwaHelper::InitChromeAppIds()
    {
        if (chromeAppIds.size() > 0)
        {
            return;
        }

        PWSTR path = NULL;
        HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &path);
        if (SUCCEEDED(hres))
        {
            std::filesystem::path fsPath(path);
            fsPath /= NonLocalizable::ChromeBase;
            for (auto& directory : std::filesystem::directory_iterator(fsPath))
            {
                if (directory.is_directory())
                {
                    std::filesystem::path directoryName = directory.path().filename();
                    if (directoryName.wstring().find(NonLocalizable::ChromeDirPrefix) == 0)
                    {
                        std::wstring appId = directoryName.wstring().substr(NonLocalizable::ChromeDirPrefix.size());
                        chromeAppIds.push_back(appId);
                        for (auto& filename : std::filesystem::directory_iterator(directory))
                        {
                            if (!filename.is_directory())
                            {
                                std::filesystem::path filenameString = filename.path().filename();
                                if (filenameString.extension().wstring() == L".ico")
                                {
                                    pwaAppIdsToAppNames.insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
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

    BOOL PwaHelper::SearchPwaAppId(std::wstring windowAumid, std::wstring* pwaAppId)
    {
        auto appIdIndexStart = windowAumid.find(NonLocalizable::ChromeAppIdIdentifier);
        if (appIdIndexStart != std::wstring::npos)
        {
            windowAumid = windowAumid.substr(appIdIndexStart + NonLocalizable::ChromeAppIdIdentifier.size());
            auto appIdIndexEnd = windowAumid.find(L" ");
            if (appIdIndexEnd != std::wstring::npos)
            {
                windowAumid = windowAumid.substr(0, appIdIndexEnd);
            }

            std::wstring windowAumidBegin = windowAumid.substr(0, 10);
            for (auto chromeAppId : chromeAppIds)
            {
                if (chromeAppId.find(windowAumidBegin) == 0)
                {
                    *pwaAppId = chromeAppId;
                    return true;
                }
            }
        }

        *pwaAppId = L"";
        return false;
    }
}