#include "pch.h"
#include "SnapshotUtils.h"

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

#pragma comment(lib, "ntdll.lib")

namespace SnapshotUtils
{
    namespace NonLocalizable
    {
        const std::wstring ApplicationFrameHost = L"ApplicationFrameHost.exe";
        const std::wstring EdgeFilename = L"msedge.exe";
        const std::wstring ChromeFilename = L"chrome.exe";
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

    BOOL GetAppId(HWND hWnd, std::wstring* result)
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
    std::vector<DWORD> FindPwaHelperProcessIds(DWORD parentProcessId)
    {
        std::vector<DWORD> pwaHelperProcessIds;

        HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot == INVALID_HANDLE_VALUE)
        {
            return pwaHelperProcessIds;
        }

        PROCESSENTRY32 pe;
        pe.dwSize = sizeof(PROCESSENTRY32);

        if (Process32First(hSnapshot, &pe))
        {
            do
            {
                if (_wcsicmp(pe.szExeFile, L"PwaHelper.exe") == 0 && pe.th32ParentProcessID == parentProcessId)
                {
                    pwaHelperProcessIds.push_back(pe.th32ProcessID);
                    //PrintProcessHandles(pe.th32ProcessID);
                }
            } while (Process32Next(hSnapshot, &pe));
        }

        CloseHandle(hSnapshot);
        return pwaHelperProcessIds;
    }

    bool IsProcessElevated(DWORD processID)
    {
        wil::unique_handle hProcess{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processID) };
        wil::unique_handle token;

        if (OpenProcessToken(hProcess.get(), TOKEN_QUERY, &token))
        {
            TOKEN_ELEVATION elevation;
            DWORD size;
            if (GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            {
                return elevation.TokenIsElevated != 0;
            }
        }

        return false;
    }

    bool IsEdge(Utils::Apps::AppData appData)
    {
        return appData.installPath.ends_with(NonLocalizable::EdgeFilename);
    }

    bool IsChrome(Utils::Apps::AppData appData)
    {
        return appData.installPath.ends_with(NonLocalizable::ChromeFilename);
    }

    void InitAumidToAppId(DWORD pid, std::map<std::wstring, std::wstring>* pwaAumidToAppId, std::map<std::wstring, std::wstring>* pwaAppIdsToAppNames)
    {
        if (pwaAumidToAppId->size() > 0)
        {
            return;
        }

        auto pwaHelperProcessIds = FindPwaHelperProcessIds(pid);
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
            pwaAumidToAppId->insert(std::map<std::wstring, std::wstring>::value_type(aumidID, appId));

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
                                            pwaAppIdsToAppNames->insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
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

    void InitChromeAppIds(std::vector<std::wstring>* chromeAppIds, std::map<std::wstring, std::wstring>* pwaAppIdsToAppNames)
    {
        if (chromeAppIds->size() > 0)
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
                        chromeAppIds->push_back(appId);
                        for (auto& filename : std::filesystem::directory_iterator(directory))
                        {
                            if (!filename.is_directory())
                            {
                                std::filesystem::path filenameString = filename.path().filename();
                                if (filenameString.extension().wstring() == L".ico")
                                {
                                    pwaAppIdsToAppNames->insert(std::map<std::wstring, std::wstring>::value_type(appId, filenameString.stem().wstring()));
                                }
                            }
                        }
                    }
                }
            }
            CoTaskMemFree(path);
        }
    }

    std::wstring SearchPwaAppId(std::vector<std::wstring> chromeAppIds, std::wstring windowAumid)
    {
        std::wstring windowAumidBegin = windowAumid.substr(0, 10);
        for (auto chromeAppId : chromeAppIds)
        {
            if (chromeAppId.find(windowAumidBegin) == 0)
            {
                return chromeAppId;
            }
        }
        return L"";
    }

    std::wstring SearchPwaName(std::map<std::wstring, std::wstring> pwaAppIdsToAppNames, std::wstring pwaAppId)
    {
        auto index = pwaAppIdsToAppNames.find(pwaAppId);
        if (index != pwaAppIdsToAppNames.end())
        {
            return index->second;
        }

        return L"";
    }

    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle)
    {
        std::vector<WorkspacesData::WorkspacesProject::Application> apps{};
        std::map<std::wstring, std::wstring> pwaAumidToAppId;
        std::vector<std::wstring> chromeAppIds;
        std::map<std::wstring, std::wstring> pwaAppIdsToAppNames;

        auto installedApps = Utils::Apps::GetAppsList();
        auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);

        for (const auto window : windows)
        {
            // filter by window rect size
            RECT rect = WindowUtils::GetWindowRect(window);
            if (rect.right - rect.left <= 0 || rect.bottom - rect.top <= 0)
            {
                continue;
            }

            // filter by window title
            std::wstring title = WindowUtils::GetWindowTitle(window);
            if (title.empty())
            {
                continue;
            }

            DWORD pid{};
            GetWindowThreadProcessId(window, &pid);

            // filter by app path
            std::wstring processPath = get_process_path(window);
            if (processPath.empty())
            {
                // When PT runs not as admin, it can't get the process path of the window of the elevated process.
                // Notify the user that running as admin is required to process elevated windows.
                if (!is_process_elevated() && IsProcessElevated(pid))
                {
                    notifications::WarnIfElevationIsRequired(GET_RESOURCE_STRING(IDS_PROJECTS),
                                                             GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED),
                                                             GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_LEARN_MORE),
                                                             GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_DIALOG_DONT_SHOW_AGAIN));
                }

                continue;
            }

            if (WindowUtils::IsExcludedByDefault(window, processPath))
            {
                continue;
            }

            // fix for the packaged apps that are not caught when minimized, e.g., Settings.
            if (processPath.ends_with(NonLocalizable::ApplicationFrameHost))
            {
                for (auto otherWindow : windows)
                {
                    DWORD otherPid{};
                    GetWindowThreadProcessId(otherWindow, &otherPid);

                    // searching for the window with the same title but different PID
                    if (pid != otherPid && title == WindowUtils::GetWindowTitle(otherWindow))
                    {
                        processPath = get_process_path(otherPid);
						break;
                    }
                }
            }

            if (WindowFilter::FilterPopup(window))
            {
                continue;
            }

            auto data = Utils::Apps::GetApp(processPath, pid, installedApps);
            if (!data.has_value() || data->name.empty())
            {
                Logger::info(L"Installed app not found: {}", processPath);
                continue;
            }

            std::wstring pwaAppId = L"";
            std::wstring finalName = data.value().name;
            std::wstring pwaName = L"";
            if (IsEdge(data.value()))
            {
                CoInitialize(NULL);
                InitAumidToAppId(pid, &pwaAumidToAppId, &pwaAppIdsToAppNames);

                std::wstring windowAumid;
                GetAppId(window, &windowAumid);
                auto pwaIndex = pwaAumidToAppId.find(windowAumid);
                if (pwaIndex != pwaAumidToAppId.end())
                {
                    pwaAppId = pwaIndex->second;
                    pwaName = SearchPwaName(pwaAppIdsToAppNames, pwaAppId);
                    if (!pwaName.empty())
                    {
                        finalName = pwaName + L" (" + finalName + L")";
                    }
                }

                CoUninitialize();
            }
            else if (IsChrome(data.value()))
            {
                CoInitialize(NULL);
                InitChromeAppIds(&chromeAppIds, &pwaAppIdsToAppNames);

                std::wstring windowAumid;
                GetAppId(window, &windowAumid);
                auto appIdIndexStart = windowAumid.find(NonLocalizable::ChromeAppIdIdentifier);
                if (appIdIndexStart != std::wstring::npos)
                {
                    windowAumid = windowAumid.substr(appIdIndexStart + NonLocalizable::ChromeAppIdIdentifier.size());
                    auto appIdIndexEnd = windowAumid.find(L" ");
                    if (appIdIndexEnd != std::wstring::npos)
                    {
                        windowAumid = windowAumid.substr(0, appIdIndexEnd);
                    }
                    pwaAppId = SearchPwaAppId(chromeAppIds, windowAumid);
                    pwaName = SearchPwaName(pwaAppIdsToAppNames, pwaAppId);
                    if (!pwaName.empty())
                    {
                        finalName = pwaName + L" (" + finalName + L")";
                    }
                }
                CoUninitialize();
            }

            bool isMinimized = WindowUtils::IsMinimized(window);
            unsigned int monitorNumber = getMonitorNumberFromWindowHandle(window);

            if (isMinimized)
            {
                // set the screen area as position, the values we get for the minimized windows are out of the screens' area
                WorkspacesData::WorkspacesProject::Monitor::MonitorRect monitorRect = getMonitorRect(monitorNumber);
                rect.left = monitorRect.left;
                rect.top = monitorRect.top;
                rect.right = monitorRect.left + monitorRect.width;
                rect.bottom = monitorRect.top + monitorRect.height;
            }

            WorkspacesData::WorkspacesProject::Application app{
                .name = finalName,
                .title = title,
                .path = data.value().installPath,
                .packageFullName = data.value().packageFullName,
                .appUserModelId = data.value().appUserModelId,
                .pwaAppId = pwaAppId,
                .commandLineArgs = L"",
                .isElevated = IsProcessElevated(pid),
                .canLaunchElevated = data.value().canLaunchElevated,
                .isMinimized = isMinimized,
                .isMaximized = WindowUtils::IsMaximized(window),
                .position = WorkspacesData::WorkspacesProject::Application::Position{
                    .x = rect.left,
                    .y = rect.top,
                    .width = rect.right - rect.left,
                    .height = rect.bottom - rect.top,
                },
                .monitor = monitorNumber,
            };

            apps.push_back(app);
        }

        return apps;
    }
}