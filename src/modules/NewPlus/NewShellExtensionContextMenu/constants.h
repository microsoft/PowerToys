#pragma once

#include "pch.h"

namespace newplus::constants::non_localizable
{
    constexpr WCHAR powertoy_key[] = L"NewPlus";

    constexpr WCHAR powertoy_name[] = L"NewPlus";

    constexpr WCHAR settings_json_data_file_path[] = L"\\settings.json";

    constexpr WCHAR settings_json_key_hide_file_extension[] = L"HideFileExtension";

    constexpr WCHAR settings_json_key_hide_starting_digits[] = L"HideStartingDigits";

    constexpr WCHAR settings_json_key_replace_variables[] = L"ReplaceVariables";
    
    constexpr WCHAR settings_json_key_template_location[] = L"TemplateLocation";

    constexpr WCHAR context_menu_package_name[] = L"NewPlusContextMenu";

    constexpr WCHAR msix_package_name[] = L"NewPlusPackage.msix";
    
    constexpr WCHAR module_name[] = L"NewPlus.ShellExtension";

    constexpr WCHAR new_icon_light_resource_relative_path[] = L"\\Assets\\NewPlus\\New_light.ico";

    constexpr WCHAR new_icon_dark_resource_relative_path[] = L"\\Assets\\NewPlus\\New_dark.ico";

    constexpr WCHAR open_templates_icon_light_resource_relative_path[] = L"\\Assets\\NewPlus\\Open_templates_light.ico";

    constexpr WCHAR open_templates_icon_dark_resource_relative_path[] = L"\\Assets\\NewPlus\\Open_templates_dark.ico";

    constexpr WCHAR parent_folder_name_variable[] = L"$PARENT_FOLDER_NAME";
}
