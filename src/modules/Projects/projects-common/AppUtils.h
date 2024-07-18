#pragma once

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

            constexpr const wchar_t* FileExplorerName = L"File Explorer";
            constexpr const wchar_t* FileExplorerPath = L"C:\\WINDOWS\\EXPLORER.EXE";
        }

		struct AppData
		{
            std::wstring name;
            std::wstring installPath;
            std::wstring packageFullName;
            bool canLaunchElevated = false;
		};

		using AppList = std::vector<AppData>;

        inline AppList IterateAppsFolder()
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
                        prop == NonLocalizable::HostEnvironmentProp)
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

        inline AppList GetAppsList()
        {
            return IterateAppsFolder();
        }

        inline std::optional<AppData> GetApp(const std::wstring& appPath, const AppList& apps)
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

            return AppData{
                .installPath = appPath
            };
        }

        inline std::optional<AppData> GetApp(HWND window, const AppList& apps)
        {
            std::wstring processPath = get_process_path_waiting_uwp(window);
            return Utils::Apps::GetApp(processPath, apps);
        }
	}
}