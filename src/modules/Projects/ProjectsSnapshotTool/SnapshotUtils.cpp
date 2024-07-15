#include "pch.h"
#include "SnapshotUtils.h"

#include <comdef.h>
#include <Wbemidl.h>

#include <projects-common/AppUtils.h>
#include <projects-common/WindowEnumerator.h>
#include <projects-common/WindowFilter.h>

#include <common/utils/process_path.h>

namespace SnapshotUtils
{
    namespace NonLocalizable
    {
        const std::wstring ApplicationFrameHost = L"ApplicationFrameHost.exe";
    }

    class WbemHelper
    {
    public:
        WbemHelper() = default;
        ~WbemHelper()
        {
            if (m_services)
            {
                m_services->Release();
            }

            if (m_locator)
            {
                m_locator->Release();
            }
        }

        bool Initialize()
        {
            // Obtain the initial locator to WMI.
            HRESULT hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, reinterpret_cast<LPVOID*>(&m_locator));
            if (FAILED(hres))
            {
                Logger::error(L"Failed to create IWbemLocator object. Error: {}", get_last_error_or_default(hres));
                return false;
            }

            // Connect to WMI through the IWbemLocator::ConnectServer method.
            hres = m_locator->ConnectServer(_bstr_t(L"ROOT\\CIMV2"), NULL, NULL, 0, NULL, 0, 0, &m_services);
            if (FAILED(hres))
            {
                Logger::error(L"Could not connect to WMI. Error: {}", get_last_error_or_default(hres));
                return false;
            }

            // Set security levels on the proxy.
            hres = CoSetProxyBlanket(m_services, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);
            if (FAILED(hres))
            {
                Logger::error(L"Could not set proxy blanket. Error: {}", get_last_error_or_default(hres));
                return false;
            }

            return true;
        }

        std::wstring GetCommandLineArgs(DWORD processID) const
        {
            static std::wstring property = L"CommandLine";
            std::wstring query = L"SELECT " + property + L" FROM Win32_Process WHERE ProcessId = " + std::to_wstring(processID);
            return Query(query, property);
        }

        std::wstring GetExecutablePath(DWORD processID) const
        {
            static std::wstring property = L"ExecutablePath";
            std::wstring query = L"SELECT " + property + L" FROM Win32_Process WHERE ProcessId = " + std::to_wstring(processID);
            return Query(query, property);
        }

    private:
        std::wstring Query(const std::wstring& query, const std::wstring& propertyName) const
        {
            if (!m_locator || !m_services)
            {
                return L"";
            }

            IEnumWbemClassObject* pEnumerator = NULL;

            HRESULT hres = m_services->ExecQuery(bstr_t("WQL"), bstr_t(query.c_str()), WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);
            if (FAILED(hres))
            {
                Logger::error(L"Query for process failed. Error: {}", get_last_error_or_default(hres));
                return L"";
            }

            IWbemClassObject* pClassObject = NULL;
            ULONG uReturn = 0;
            std::wstring result = L"";
            while (pEnumerator)
            {
                HRESULT hr = pEnumerator->Next(WBEM_INFINITE, 1, &pClassObject, &uReturn);
                if (uReturn == 0)
                {
                    break;
                }

                VARIANT vtProp;
                hr = pClassObject->Get(propertyName.c_str(), 0, &vtProp, 0, 0);
                if (SUCCEEDED(hr) && vtProp.vt == VT_BSTR)
                {
                    result = vtProp.bstrVal;
                }
                VariantClear(&vtProp);

                pClassObject->Release();
            }

            pEnumerator->Release();

            return result;
        }

        IWbemLocator* m_locator = NULL;
        IWbemServices* m_services = NULL;     
    };

    std::wstring GetCommandLineArgs(DWORD processID, const WbemHelper& wbemHelper)
    {
        std::wstring executablePath = wbemHelper.GetExecutablePath(processID);
        std::wstring commandLineArgs = wbemHelper.GetCommandLineArgs(processID);
        
        if (!commandLineArgs.empty())
        {
            auto pos = commandLineArgs.find(executablePath);
            if (pos != std::wstring::npos)
            {
                commandLineArgs = commandLineArgs.substr(pos + executablePath.size());
                auto spacePos = commandLineArgs.find_first_of(' ');
                if (spacePos != std::wstring::npos)
                {
                    commandLineArgs = commandLineArgs.substr(spacePos + 1);
                }
			}
        }

        return commandLineArgs;
    }

    std::vector<Project::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle)
    {
        std::vector<Project::Application> apps{};

        auto installedApps = Utils::Apps::GetAppsList();
        auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);
        
        WbemHelper wbemHelper;
        wbemHelper.Initialize();

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

            // filter by app path
            std::wstring processPath = get_process_path(window);
            if (processPath.empty() || WindowUtils::IsExcludedByDefault(window, processPath, title))
            {
                Logger::debug(L"Excluded by default: {}, {}", title, processPath);
                continue;
            }

            DWORD pid{};
            GetWindowThreadProcessId(window, &pid);

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

            auto data = Utils::Apps::GetApp(processPath, installedApps);
            if (!data.has_value() || data->name.empty())
            {
                Logger::debug(L"Installed app not found: {}, {}", title, processPath);
                continue;
            }

            Project::Application app{
                .name = data.value().name,
                .title = title,
                .path = processPath,
                .packageFullName = data.value().packageFullName,
                .commandLineArgs = L"", // GetCommandLineArgs(pid, wbemHelper),
                .isMinimized = WindowUtils::IsMinimized(window),
                .isMaximized = WindowUtils::IsMaximized(window),
                .position = Project::Application::Position{
                    .x = rect.left,
                    .y = rect.top,
                    .width = rect.right - rect.left,
                    .height = rect.bottom - rect.top,
                },
                .monitor = getMonitorNumberFromWindowHandle(window),
            };

            apps.push_back(app);
        }

        return apps;
    }
}