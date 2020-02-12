#include "pch.h"
#include "msi_to_msix_upgrade.h"

#include <msi.h>
#include <common/common.h>

#include <common/winstore.h>
#include <common/notifications.h>
#include <MsiQuery.h>

namespace
{
    const wchar_t* POWER_TOYS_UPGRADE_CODE = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const wchar_t* DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH = L"delete_previous_powertoys_confirm";
}

namespace localized_strings
{
    const wchar_t* OFFER_UNINSTALL_MSI = L"We've detected a previous installation of PowerToys. Would you like to remove it?";
    const wchar_t* OFFER_UNINSTALL_MSI_TITLE = L"PowerToys: uninstall previous version?";
    const wchar_t* UNINSTALLATION_SUCCESS = L"Previous version of PowerToys was uninstalled successfully.";
}

void uninstall_msi_with_confirmation()
{
    if (!winstore::running_as_packaged())
    {
        return;
    }

    wchar_t GUID_product_string[39];
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, GUID_product_string); !found)
    {
        return;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(GUID_product_string); !installed)
    {
        return;
    }

    DWORD package_path_size = 0;

    if (const bool has_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size); !has_package_path)
    {
        return;
    }

    const auto package_path = std::make_unique<wchar_t[]>(package_path_size++);

    if (const bool got_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, package_path.get(), &package_path_size); !got_package_path)
    {
        return;
    }

    const auto selection = SHMessageBoxCheckW(nullptr, localized_strings::OFFER_UNINSTALL_MSI, localized_strings::OFFER_UNINSTALL_MSI_TITLE, MB_ICONQUESTION | MB_YESNO, IDNO, DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH);
    if (selection != IDYES)
    {
        return;
    }

    const auto uninstall_result = MsiInstallProductW(package_path.get(), L"REMOVE=ALL");
    auto system_message = get_last_error_message(uninstall_result);
    if (ERROR_SUCCESS == uninstall_result)
    {
        notifications::show_toast(localized_strings::UNINSTALLATION_SUCCESS);
    }
    else if (auto system_message = get_last_error_message(uninstall_result); system_message.has_value())
    {
        notifications::show_toast(*system_message);
    }
}
