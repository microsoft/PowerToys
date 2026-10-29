#include "pch.h"
#include "AppUtils.h"
#include "SteamHelper.h"

#include <atlbase.h>
#include <propvarutil.h>
#include <ShlObj.h>
#include <TlHelp32.h>

#include <filesystem>
#include <set>
#include <appmodel.h>
#include <propkey.h>
#include <wil/resource.h>
#include <winrt/Windows.ApplicationModel.h>

#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>

namespace Utils
{
    namespace Apps
    {
        namespace NonLocalizable
        {
            constexpr const wchar_t* PackageFullNameProp = L"System.AppUserModel.PackageFullName";
            constexpr const wchar_t* PackageInstallPathProp = L"System.AppUserModel.PackageInstallPath";
            constexpr const wchar_t* InstallPathProp = L"System.Link.TargetParsingPath";
            constexpr const wchar_t* HostEnvironmentProp = L"System.AppUserModel.HostEnvironment";
            constexpr const wchar_t* AppUserModelIdProp = L"System.AppUserModel.ID";

            constexpr const wchar_t* FileExplorerName = L"File Explorer";
            constexpr const wchar_t* FileExplorerPath = L"C:\\WINDOWS\\EXPLORER.EXE";
            constexpr const wchar_t* PowerToys = L"PowerToys.exe";
            constexpr const wchar_t* PowerToysSettingsUpper = L"POWERTOYS.SETTINGS.EXE";
            constexpr const wchar_t* PowerToysSettings = L"PowerToys.Settings.exe";
            constexpr const wchar_t* ApplicationFrameHost = L"APPLICATIONFRAMEHOST.EXE";
            constexpr const wchar_t* Exe = L".EXE";

            constexpr const wchar_t* EdgeFilename = L"msedge.exe";
            constexpr const wchar_t* ChromeFilename = L"chrome.exe";

            constexpr const wchar_t* SteamUrlProtocol = L"steam:";
        }

        AppList IterateAppsFolder()
        {
            AppList result{};

            // get apps folder
            CComPtr<IShellItem> folder;
            HRESULT hr = SHGetKnownFolderItem(FOLDERID_AppsFolder, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(&folder));
            if (FAILED(hr))
            {
                Logger::error(L"Failed to get known apps folder: {}", get_last_error_or_default(hr));
                return result;
            }

            CComPtr<IEnumShellItems> enumItems;
            hr = folder->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&enumItems));
            if (FAILED(hr))
            {
                Logger::error(L"Failed to bind to enum items handler: {}", get_last_error_or_default(hr));
                return result;
            }

            IShellItem* items;
            while (enumItems->Next(1, &items, nullptr) == S_OK)
            {
                CComPtr<IShellItem> item = items;
                CComHeapPtr<wchar_t> name;

                hr = item->GetDisplayName(SIGDN_NORMALDISPLAY, &name);
                if (FAILED(hr))
                {
                    Logger::error(L"Failed to get display name for app: {}", get_last_error_or_default(hr));
                    continue;
                }

                AppData data{
                    .name = std::wstring(name.m_pData),
                };

                // properties
                CComPtr<IPropertyStore> store;
                hr = item->BindToHandler(NULL, BHID_PropertyStore, IID_PPV_ARGS(&store));
                if (FAILED(hr))
                {
                    Logger::error(L"Failed to bind to property store handler: {}", get_last_error_or_default(hr));
                    continue;
                }

                DWORD count = 0;
                store->GetCount(&count);
                for (DWORD i = 0; i < count; i++)
                {
                    PROPERTYKEY pk;
                    hr = store->GetAt(i, &pk);
                    if (FAILED(hr))
                    {
                        Logger::error(L"Failed to get property key: {}", get_last_error_or_default(hr));
                        continue;
                    }

                    CComHeapPtr<wchar_t> pkName;
                    PSGetNameFromPropertyKey(pk, &pkName);

                    std::wstring prop(pkName.m_pData);
                    if (prop == NonLocalizable::PackageFullNameProp ||
                        prop == NonLocalizable::PackageInstallPathProp ||
                        prop == NonLocalizable::InstallPathProp ||
                        prop == NonLocalizable::HostEnvironmentProp ||
                        prop == NonLocalizable::AppUserModelIdProp)
                    {
                        PROPVARIANT pv;
                        PropVariantInit(&pv);
                        hr = store->GetValue(pk, &pv);
                        if (SUCCEEDED(hr))
                        {
                            if (prop == NonLocalizable::HostEnvironmentProp)
                            {
                                CComHeapPtr<int> propVariantInt;
                                propVariantInt.Allocate(1);
                                PropVariantToInt32(pv, propVariantInt);

                                if (prop == NonLocalizable::HostEnvironmentProp)
                                {
                                    data.canLaunchElevated = *propVariantInt.m_pData != 1;
                                }
                            }
                            else
                            {
                                CComHeapPtr<wchar_t> propVariantString;
                                propVariantString.Allocate(512);
                                PropVariantToString(pv, propVariantString, 512);

                                if (prop == NonLocalizable::PackageFullNameProp)
                                {
                                    data.packageFullName = propVariantString.m_pData;
                                }
                                else if (prop == NonLocalizable::AppUserModelIdProp)
                                {
                                    data.appUserModelId = propVariantString.m_pData;
                                }
                                else if (prop == NonLocalizable::PackageInstallPathProp || prop == NonLocalizable::InstallPathProp)
                                {
                                    data.installPath = propVariantString.m_pData;

                                    if (!data.installPath.empty())
                                    {
                                        const bool isSteamProtocol = data.installPath.rfind(NonLocalizable::SteamUrlProtocol, 0) == 0;

                                        if (isSteamProtocol)
                                        {
                                            Logger::info(L"Found steam game: protocol path: {}", data.installPath);
                                            data.protocolPath = data.installPath;

                                            try
                                            {
                                                auto gameId = Steam::GetGameIdFromUrlProtocolPath(data.installPath);
                                                auto gameFolder = Steam::GetSteamGameInfoFromAcfFile(gameId);

                                                if (gameFolder)
                                                {
                                                    data.installPath = gameFolder->gameInstallationPath;
                                                    Logger::info(L"Found steam game: physical path: {}", data.installPath);
                                                }
                                            }
                                            catch (std::exception ex)
                                            {
                                                Logger::error(L"Failed to get installPath for game {}", data.installPath);
                                                Logger::error("Error: {}", ex.what());
                                            }
                                        }
                                    }
                                }
                            }

                            PropVariantClear(&pv);
                        }
                        else
                        {
                            Logger::error(L"Failed to get property value: {}", get_last_error_or_default(hr));
                        }
                    }
                }

                if (!data.name.empty())
                {
                    result.push_back(data);
                }
            }

            return result;
        }

        const std::wstring& GetCurrentFolder()
        {
            static std::wstring currentFolder;
            if (currentFolder.empty())
            {
                TCHAR buffer[MAX_PATH] = { 0 };
                GetModuleFileName(NULL, buffer, MAX_PATH);
                std::wstring::size_type pos = std::wstring(buffer).find_last_of(L"\\/");
                currentFolder = std::wstring(buffer).substr(0, pos);
            }

            return currentFolder;
        }

        const std::wstring& GetCurrentFolderUpper()
        {
            static std::wstring currentFolderUpper;
            if (currentFolderUpper.empty())
            {
                currentFolderUpper = GetCurrentFolder();
                std::transform(currentFolderUpper.begin(), currentFolderUpper.end(), currentFolderUpper.begin(), towupper);
            }

            return currentFolderUpper;
        }

        AppList GetAppsList()
        {
            return IterateAppsFolder();
        }

        std::optional<AppData> GetShortcutApp(const std::wstring& path)
        {
            std::wstring extension = std::filesystem::path(path).extension();
            std::transform(extension.begin(), extension.end(), extension.begin(), towlower);
            AppData data{};
            wchar_t target[32768]{};
            if (extension == L".lnk")
            {
                CComPtr<IShellLinkW> link;
                if (FAILED(link.CoCreateInstance(CLSID_ShellLink)))
                    return std::nullopt;
                CComQIPtr<IPersistFile> file(link);
                if (!file || FAILED(file->Load(path.c_str(), STGM_READ)) ||
                    FAILED(link->GetPath(target, ARRAYSIZE(target), nullptr, SLGP_RAWPATH)))
                    return std::nullopt;
                CComQIPtr<IPropertyStore> store(link);
                PROPVARIANT value{};
                if (store && SUCCEEDED(store->GetValue(PKEY_AppUserModel_ID, &value)))
                {
                    CComHeapPtr<wchar_t> id;
                    if (SUCCEEDED(PropVariantToStringAlloc(value, &id)))
                        data.appUserModelId = id.m_pData;
                }
                PropVariantClear(&value);
            }
            else if (extension == L".url")
            {
                GetPrivateProfileStringW(L"InternetShortcut", L"URL", L"", target, ARRAYSIZE(target), path.c_str());
            }
            else
            {
                return std::nullopt;
            }

            if (!target[0])
                return std::nullopt;
            const auto required = ExpandEnvironmentStringsW(target, nullptr, 0);
            if (!required || required > 32768)
                return std::nullopt;
            std::wstring expanded(required, L'\0');
            if (ExpandEnvironmentStringsW(target, expanded.data(), required) != required)
                return std::nullopt;
            expanded.resize(required - 1);
            data.installPath = std::move(expanded);
            data.canLaunchElevated = true;

            CComPtr<IShellItem> item;
            CComHeapPtr<wchar_t> name;
            if (SUCCEEDED(SHCreateItemFromParsingName(path.c_str(), nullptr, IID_PPV_ARGS(&item))) &&
                SUCCEEDED(item->GetDisplayName(SIGDN_NORMALDISPLAY, &name)))
                data.name = name.m_pData;
            else
                data.name = std::filesystem::path(path).stem();

            if (data.installPath.starts_with(NonLocalizable::SteamUrlProtocol))
            {
                data.protocolPath = data.installPath;
                try
                {
                    const auto game = Steam::GetSteamGameInfoFromAcfFile(Steam::GetGameIdFromUrlProtocolPath(data.protocolPath));
                    if (game)
                        data.installPath = game->gameInstallationPath;
                }
                catch (const std::exception& error)
                {
                    Logger::warn("Could not resolve captured Steam shortcut: {}", error.what());
                }
            }
            return data;
        }

        AppList GetCaptureAppsList(const std::vector<HWND>& windows)
        {
            AppList result;
            std::set<DWORD> seen;
            for (const auto window : windows)
            {
                DWORD pid{};
                GetWindowThreadProcessId(window, &pid);
                if (!pid || !seen.insert(pid).second)
                    continue;
                wil::unique_handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid));
                if (!process)
                    continue;
                wchar_t id[APPLICATION_USER_MODEL_ID_MAX_LENGTH + 1]{};
                UINT32 idLength = ARRAYSIZE(id);
                wchar_t package[PACKAGE_FULL_NAME_MAX_LENGTH + 1]{};
                UINT32 packageLength = ARRAYSIZE(package);
                if (GetApplicationUserModelId(process.get(), &idLength, id) != ERROR_SUCCESS ||
                    GetPackageFullName(process.get(), &packageLength, package) != ERROR_SUCCESS)
                    continue;
                try
                {
                    const auto info = winrt::Windows::ApplicationModel::AppInfo::GetFromAppUserModelId(id);
                    AppData data{
                        .name = std::wstring(info.DisplayInfo().DisplayName()),
                        .installPath = get_process_path(pid),
                        .packageFullName = package,
                        .appUserModelId = id,
                    };
                    wil::unique_handle token;
                    AppPolicyWindowingModel model{};
                    data.canLaunchElevated = OpenProcessToken(process.get(), TOKEN_QUERY, token.put()) &&
                        AppPolicyGetWindowingModel(token.get(), &model) == ERROR_SUCCESS &&
                        model == AppPolicyWindowingModel_ClassicDesktop;
                    if (!data.name.empty() && !data.installPath.empty())
                        result.push_back(std::move(data));
                }
                catch (const winrt::hresult_error& error)
                {
                    Logger::warn(L"Could not resolve captured package metadata: 0x{:08X}", static_cast<uint32_t>(error.code()));
                }
            }

            // AppsFolder synchronously rebuilds the entire app catalog. Read desktop
            // shortcuts without resolving/launching them; query packages only for live windows.
            for (const auto& folderId : { FOLDERID_Programs, FOLDERID_CommonPrograms })
            {
                CComHeapPtr<wchar_t> path;
                if (FAILED(SHGetKnownFolderPath(folderId, KF_FLAG_DEFAULT, nullptr, &path)))
                    continue;
                std::error_code error;
                std::filesystem::recursive_directory_iterator entry(path.m_pData, std::filesystem::directory_options::skip_permission_denied, error);
                const std::filesystem::recursive_directory_iterator end;
                while (entry != end)
                {
                    const auto attributes = GetFileAttributesW(entry->path().c_str());
                    if (attributes != INVALID_FILE_ATTRIBUTES &&
                        (attributes & FILE_ATTRIBUTE_DIRECTORY) && (attributes & FILE_ATTRIBUTE_REPARSE_POINT))
                        entry.disable_recursion_pending();
                    if (auto data = GetShortcutApp(entry->path().wstring()))
                        result.push_back(std::move(*data));
                    entry.increment(error);
                    if (error)
                    {
                        Logger::warn("Could not finish capture shortcut discovery: {}", error.message());
                        break;
                    }
                }
            }
            return result;
        }

        AppList GetWorkspaceApps(const WorkspacesData::WorkspacesProject& project, bool refreshPackages)
        {
            AppList result;
            std::optional<AppList> legacyInventory;
            for (const auto& app : project.apps)
            {
                if (std::any_of(result.begin(), result.end(), [&](const AppData& entry) {
                    return entry.name == app.name && entry.appUserModelId == app.appUserModelId &&
                        entry.installPath == app.path;
                }))
                    continue;
                AppData data{
                    .name = app.name,
                    .installPath = app.path,
                    .packageFullName = app.packageFullName,
                    .appUserModelId = app.appUserModelId,
                    .canLaunchElevated = app.canLaunchElevated,
                };
                if (app.appUserModelId.starts_with(NonLocalizable::SteamUrlProtocol))
                    data.protocolPath = app.appUserModelId;
                if (refreshPackages && !app.packageFullName.empty())
                {
                    CComPtr<IShellItem> item;
                    auto hr = app.appUserModelId.empty() ? E_INVALIDARG :
                        SHCreateItemFromParsingName((L"shell:AppsFolder\\" + app.appUserModelId).c_str(), nullptr, IID_PPV_ARGS(&item));
                    CComPtr<IPropertyStore> store;
                    if (SUCCEEDED(hr))
                        hr = item->BindToHandler(nullptr, BHID_PropertyStore, IID_PPV_ARGS(&store));
                    std::wstring package, path;
                    if (SUCCEEDED(hr))
                    {
                        for (auto& entry : { std::pair{ NonLocalizable::PackageFullNameProp, &package },
                                             std::pair{ NonLocalizable::PackageInstallPathProp, &path } })
                        {
                            PROPERTYKEY key{};
                            hr = PSGetPropertyKeyFromName(entry.first, &key);
                            if (FAILED(hr)) break;
                            PROPVARIANT value{};
                            hr = store->GetValue(key, &value);
                            if (SUCCEEDED(hr))
                            {
                                CComHeapPtr<wchar_t> text;
                                hr = PropVariantToStringAlloc(value, &text);
                                if (SUCCEEDED(hr)) *entry.second = text.m_pData;
                            }
                            PropVariantClear(&value);
                            if (FAILED(hr)) break;
                        }
                    }
                    if (SUCCEEDED(hr) && !package.empty() && !path.empty())
                    {
                        data.packageFullName = std::move(package);
                        data.installPath = std::move(path);
                    }
                    else
                    {
                        if (SUCCEEDED(hr)) hr = HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
                        Logger::warn(L"Targeted workspace package lookup failed (HRESULT=0x{:08X}); using the legacy application inventory.",
                            static_cast<uint32_t>(hr));
                        if (!legacyInventory) legacyInventory = GetAppsList();
                        const auto found = std::find_if(legacyInventory->begin(), legacyInventory->end(),
                            [&](const AppData& entry) { return entry.name == app.name; });
                        if (found != legacyInventory->end()) data = *found;
                    }
                }
                result.push_back(std::move(data));
            }
            return result;
        }

        DWORD GetParentPid(DWORD pid)
        {
            DWORD res = 0;
            HANDLE h = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            PROCESSENTRY32 pe = { 0 };
            pe.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(h, &pe))
            {
                do
                {
                    if (pe.th32ProcessID == pid)
                    {
                        res = pe.th32ParentProcessID;
                    }
                } while (Process32Next(h, &pe));
            }

            CloseHandle(h);
            return res;
        }

        std::optional<AppData> GetApp(const std::wstring& appPath, DWORD pid, const AppList& apps)
        {
            std::wstring appPathUpper(appPath);
            std::transform(appPathUpper.begin(), appPathUpper.end(), appPathUpper.begin(), towupper);

            // filter out ApplicationFrameHost.exe
            if (appPathUpper.ends_with(NonLocalizable::ApplicationFrameHost))
            {
                return std::nullopt;
            }

            // edge case, "Windows Software Development Kit" has the same app path as "File Explorer"
            if (appPathUpper == NonLocalizable::FileExplorerPath)
            {
                return AppData{
                    .name = NonLocalizable::FileExplorerName,
                    .installPath = appPath,
                };
            }

            // PowerToys
            if (appPathUpper.contains(GetCurrentFolderUpper()))
            {
                if (appPathUpper.contains(NonLocalizable::PowerToysSettingsUpper))
                {
                    return AppData{
                        .name = NonLocalizable::PowerToysSettings,
                        .installPath = GetCurrentFolder() + L"\\" + NonLocalizable::PowerToys
                    };
                }
                else
                {
                    return AppData{
                        .name = std::filesystem::path(appPath).stem(),
                        .installPath = appPath,
                    };
                }
            }

            // search in apps list
            std::optional<AppData> appDataPlanB{ std::nullopt };
            for (const auto& appData : apps)
            {
                if (!appData.installPath.empty())
                {
                    std::wstring installPathUpper(appData.installPath);
                    std::transform(installPathUpper.begin(), installPathUpper.end(), installPathUpper.begin(), towupper);

                    if (appPathUpper.contains(installPathUpper))
                    {
                        // Update the install path to keep .exe in the path
                        if (!installPathUpper.ends_with(NonLocalizable::Exe))
                        {
                            auto settingsAppData = appData;
                            settingsAppData.installPath = appPath;
                            return settingsAppData;
                        }

                        return appData;
                    }

                    // edge case, some apps (e.g., Gitkraken) have different .exe files in the subfolders.
                    // apps list contains only one path, so in this case app is not found
                    // remember the match and return it in case the loop is over and there are no direct matches
                    if (std::filesystem::path(appPath).filename() == std::filesystem::path(appData.installPath).filename())
                    {
                        appDataPlanB = appData;
                    }
                }
            }

            if (appDataPlanB.has_value())
            {
                return appDataPlanB.value();
            }

            // try by name if path not found
            // apps list could contain a different path from that one we get from the process (for electron)
            std::wstring exeName = std::filesystem::path(appPath).stem();
            std::wstring exeNameUpper(exeName);
            std::transform(exeNameUpper.begin(), exeNameUpper.end(), exeNameUpper.begin(), towupper);

            for (const auto& appData : apps)
            {
                std::wstring appNameUpper(appData.name);
                std::transform(appNameUpper.begin(), appNameUpper.end(), appNameUpper.begin(), towupper);

                if (appNameUpper == exeNameUpper)
                {
                    auto result = appData;
                    result.installPath = appPath;
                    return result;
                }
            }

            // try with parent process (fix for Steam)
            auto parentPid = GetParentPid(pid);
            auto parentProcessPath = get_process_path(parentPid);

            if (!parentProcessPath.empty())
            {
                std::wstring parentDirUpper = std::filesystem::path(parentProcessPath).parent_path().c_str();
                std::transform(parentDirUpper.begin(), parentDirUpper.end(), parentDirUpper.begin(), towupper);

                if (appPathUpper.starts_with(parentDirUpper))
                {
                    Logger::info(L"original process is in the subfolder of the parent process");

                    for (const auto& appData : apps)
                    {
                        if (!appData.installPath.empty())
                        {
                            std::wstring installDirUpper = std::filesystem::path(appData.installPath).parent_path().c_str();
                            std::transform(installDirUpper.begin(), installDirUpper.end(), installDirUpper.begin(), towupper);

                            if (installDirUpper == parentDirUpper)
                            {
                                return appData;
                            }
                        }
                    }
                }
            }

            return AppData{
                .name = std::filesystem::path(appPath).stem(),
                .installPath = appPath
            };
        }

        std::optional<AppData> GetApp(HWND window, const AppList& apps)
        {
            std::wstring processPath = get_process_path(window);

            DWORD pid{};
            GetWindowThreadProcessId(window, &pid);

            return Utils::Apps::GetApp(processPath, pid, apps);
        }

        bool UpdateAppVersion(WorkspacesData::WorkspacesProject::Application& app, const AppList& installedApps)
        {
            auto installedApp = std::find_if(installedApps.begin(), installedApps.end(), [&](const AppData& val) { return val.name == app.name; });
            if (installedApp == installedApps.end())
            {
                return false;
            }

            // Packaged apps have version in the path, it will be outdated after update.
            // We need make sure the current package is up to date.
            if (!app.packageFullName.empty())
            {
                if (app.packageFullName != installedApp->packageFullName)
                {
                    std::wstring exeFileName = app.path.substr(app.path.find_last_of(L"\\") + 1);
                    app.packageFullName = installedApp->packageFullName;
                    app.path = installedApp->installPath + L"\\" + exeFileName;
                    Logger::trace(L"Updated package full name for {}: {}", app.name, app.packageFullName);
                    return true;
                }
            }

            return false;
        }

        bool UpdateWorkspacesApps(WorkspacesData::WorkspacesProject& workspace, const AppList& installedApps)
        {
            bool updated = false;
            for (auto& app : workspace.apps)
            {
                updated |= UpdateAppVersion(app, installedApps);
            }

            return updated;
        }

        bool AppData::IsEdge() const
        {
            return installPath.ends_with(NonLocalizable::EdgeFilename);
        }

        bool AppData::IsChrome() const
        {
            return installPath.ends_with(NonLocalizable::ChromeFilename);
        }

        bool AppData::IsSteamGame() const
        {
            return protocolPath.rfind(NonLocalizable::SteamUrlProtocol, 0) == 0;
        }
    }
}