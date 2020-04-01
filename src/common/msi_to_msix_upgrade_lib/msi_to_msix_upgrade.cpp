#include "pch.h"

#include "version.h"

#include "msi_to_msix_upgrade.h"

#include <msi.h>
#include <common/common.h>
#include <common/json.h>

#include <common/winstore.h>
#include <common/notifications.h>
#include <MsiQuery.h>

#include <winrt/Windows.Web.Http.h>
#include <winrt/Windows.Web.Http.Headers.h>

#include "VersionHelper.h"

namespace
{
    const wchar_t* POWER_TOYS_UPGRADE_CODE = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const wchar_t* DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH = L"delete_previous_powertoys_confirm";
    const wchar_t* USER_AGENT = L"Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
    const wchar_t* LATEST_RELEASE_ENDPOINT = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
}

namespace localized_strings
{
    const wchar_t* OFFER_UNINSTALL_MSI = L"We've detected a previous installation of PowerToys. Would you like to remove it?";
    const wchar_t* OFFER_UNINSTALL_MSI_TITLE = L"PowerToys: uninstall previous version?";
    const wchar_t* UNINSTALLATION_SUCCESS = L"Previous version of PowerToys was uninstalled successfully.";
    const wchar_t* UNINSTALLATION_UNKNOWN_ERROR = L"Error: please uninstall the previous version of PowerToys manually.";
}

std::wstring get_msi_package_path()
{
    std::wstring package_path;
    wchar_t GUID_product_string[39];
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, GUID_product_string); !found)
    {
        return package_path;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(GUID_product_string); !installed)
    {
        return package_path;
    }

    DWORD package_path_size = 0;

    if (const bool has_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size); !has_package_path)
    {
        return package_path;
    }

    package_path = std::wstring(++package_path_size, L'\0');
    if (const bool got_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size); !got_package_path)
    {
        package_path = {};
        return package_path;
    }

    package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    return package_path;
}

bool offer_msi_uninstallation()
{
    const auto selection = SHMessageBoxCheckW(nullptr, localized_strings::OFFER_UNINSTALL_MSI, localized_strings::OFFER_UNINSTALL_MSI_TITLE, MB_ICONQUESTION | MB_YESNO, IDNO, DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH);
    return selection == IDYES;
}

bool uninstall_msi_version(const std::wstring& package_path)
{
    const auto uninstall_result = MsiInstallProductW(package_path.c_str(), L"REMOVE=ALL");
    if (ERROR_SUCCESS == uninstall_result)
    {
        notifications::show_toast(localized_strings::UNINSTALLATION_SUCCESS);
        return true;
    }
    else if (auto system_message = get_last_error_message(uninstall_result); system_message.has_value())
    {
        try
        {
            notifications::show_toast(*system_message);
        }
        catch (...)
        {
            notifications::show_toast(localized_strings::UNINSTALLATION_UNKNOWN_ERROR);
        }
    }
    return false;
}

std::future<std::optional<new_version_download_info>> check_for_new_github_release_async()
{
    try
    {
        winrt::Windows::Web::Http::HttpClient client;
        auto headers = client.DefaultRequestHeaders();
        headers.UserAgent().TryParseAdd(USER_AGENT);

        auto response = co_await client.GetAsync(winrt::Windows::Foundation::Uri{ LATEST_RELEASE_ENDPOINT });
        (void)response.EnsureSuccessStatusCode();
        const auto body = co_await response.Content().ReadAsStringAsync();
        auto json_body = json::JsonValue::Parse(body).GetObjectW();
        auto new_version = json_body.GetNamedString(L"tag_name");
        winrt::Windows::Foundation::Uri release_page_uri{ json_body.GetNamedString(L"html_url") };

        VersionHelper github_version(winrt::to_string(new_version));
        VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);

        if (github_version > current_version)
        {
            co_return new_version_download_info{ std::move(release_page_uri), new_version.c_str() };
        }
        else
        {
            co_return std::nullopt;
        }
    }
    catch (...)
    {
        co_return std::nullopt;
    }
}