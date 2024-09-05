#include "pch.h"
#include "AppUtils.h"

#include <atlbase.h>
#include <ShlObj.h>
#include <propvarutil.h>

#include <filesystem>

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

        std::optional<AppData> GetApp(const std::wstring& appPath, const AppList& apps)
        {
            std::wstring appPathUpper(appPath);
            std::transform(appPathUpper.begin(), appPathUpper.end(), appPathUpper.begin(), towupper);

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

            for (const auto& appData : apps)
            {
                if (!appData.installPath.empty())
                {
                    std::wstring installPathUpper(appData.installPath);
                    std::transform(installPathUpper.begin(), installPathUpper.end(), installPathUpper.begin(), towupper);

                    if (appPathUpper.contains(installPathUpper))
                    {
                        return appData;
                    }

                    // edge case, some apps (e.g., Gitkraken) have different .exe files in the subfolders.
                    // apps list contains only one path, so in this case app is not found
                    if (std::filesystem::path(appPath).filename() == std::filesystem::path(appData.installPath).filename())
                    {
                        return appData;
                    }
                }
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
                    return appData;
                }
            }

            return AppData{
                .installPath = appPath
            };
        }

        std::optional<AppData> GetApp(HWND window, const AppList& apps)
        {
            std::wstring processPath = get_process_path(window);
            return Utils::Apps::GetApp(processPath, apps);
        }
    }
}