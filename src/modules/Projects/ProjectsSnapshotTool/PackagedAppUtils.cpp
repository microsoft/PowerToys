#include "pch.h"
#include "PackagedAppUtils.h"

#include <windows.h>
#include <winreg.h>

#include <iostream>
#include <filesystem>

#include <atlbase.h>
#include <wil/registry.h>
#include <ShlObj.h>
#include <propvarutil.h>

namespace Utils
{
    namespace Apps
    {
        namespace NonLocalizable
        {
            const wchar_t* PackageFullNameProp = L"System.AppUserModel.PackageFullName";
            const wchar_t* PackageInstallPathProp = L"System.AppUserModel.PackageInstallPath";
            const wchar_t* InstallPathProp = L"System.Link.TargetParsingPath";

            const wchar_t* FileExplorerName = L"File Explorer";
            const wchar_t* FileExplorerPath = L"C:\\WINDOWS\\EXPLORER.EXE";
        }

        AppList IterateAppsFolder()
        {
            AppList result{};

            // get apps folder
            CComPtr<IShellItem> folder;
            HRESULT hr = SHGetKnownFolderItem(FOLDERID_AppsFolder, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(&folder));
            if (FAILED(hr))
            {
                return result;
            }

            CComPtr<IEnumShellItems> enumItems;
            hr = folder->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&enumItems));
            if (FAILED(hr))
            {
                return result;
            }

            IShellItem* items;
            while (enumItems->Next(1, &items, nullptr) == S_OK)
            {
                CComPtr<IShellItem> item = items;
                CComHeapPtr<wchar_t> name;
                
                if (FAILED(item->GetDisplayName(SIGDN_NORMALDISPLAY, &name)))
                {
                    continue;
                }

                std::wcout << name.m_pData << std::endl;
                AppData data
                {
                    .name = std::wstring(name.m_pData),
                };
                
                // properties
                CComPtr<IPropertyStore> store;
                if (FAILED(item->BindToHandler(NULL, BHID_PropertyStore, IID_PPV_ARGS(&store))))
                {
                    continue;
                }

                DWORD count = 0;
                store->GetCount(&count);
                for (DWORD i = 0; i < count; i++)
                {
                    PROPERTYKEY pk;
                    if (FAILED(store->GetAt(i, &pk)))
                    {
                        continue;
                    }

                    CComHeapPtr<wchar_t> pkName;
                    PSGetNameFromPropertyKey(pk, &pkName);

                    std::wstring prop(pkName.m_pData);
                    if (prop == NonLocalizable::PackageFullNameProp || 
                        prop == NonLocalizable::PackageInstallPathProp ||
                        prop == NonLocalizable::InstallPathProp)
                    {
                        PROPVARIANT pv;
                        PropVariantInit(&pv);
                        if (SUCCEEDED(store->GetValue(pk, &pv)))
                        {
                            CComHeapPtr<wchar_t> propVariantString;
                            propVariantString.Allocate(512);
                            PropVariantToString(pv, propVariantString, 512);
                            PropVariantClear(&pv);

                            if (prop == NonLocalizable::PackageFullNameProp)
                            {
                                data.packageFullName = propVariantString.m_pData;
                            }
                            else if (prop == NonLocalizable::PackageInstallPathProp || prop == NonLocalizable::InstallPathProp)
                            {
                                data.installPath = propVariantString.m_pData;
                            }
                        }

                        if (!data.packageFullName.empty() && !data.installPath.empty())
                        {
                            break;
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

        AppList Utils::Apps::GetAppsList()
        {
            return IterateAppsFolder();
        }

        std::optional<AppData> GetApp(const std::wstring& appPath, const AppList& apps)
        {
            for (const auto& appData : apps)
            {
                std::wstring appPathUpper(appPath);
                std::transform(appPathUpper.begin(), appPathUpper.end(), appPathUpper.begin(), towupper);

                // edge case, "Windows Software Development Kit" has the same app path as "File Explorer"
                if (appPathUpper == NonLocalizable::FileExplorerPath)
                {
                    return AppData
                    {
                        .name = NonLocalizable::FileExplorerName,
                        .installPath = appPath,
                    };
                }

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

            // TODO: not all installed apps found
            return AppData {
                .installPath = appPath
            };
        }
    }
}
