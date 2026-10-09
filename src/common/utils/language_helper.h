#pragma once

#include <filesystem>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>

namespace LanguageHelpers
{
    inline std::wstring load_language()
    {
        std::filesystem::path languageJsonFilePath(PTSettingsHelper::get_root_save_folder_location() + L"\\language.json");

        auto langJson = json::from_file(languageJsonFilePath.c_str());
        if (!langJson.has_value())
        {
            return {};
        }

        std::wstring language = langJson->GetNamedString(L"language", L"").c_str();
        return language;
    }
}
