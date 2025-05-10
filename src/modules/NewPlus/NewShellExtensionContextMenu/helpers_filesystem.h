#pragma once

#include "helpers_variables.h"

namespace newplus::helpers::filesystem
{
    namespace constants::non_localizable
    {
        constexpr WCHAR desktop_ini_filename[] = L"desktop.ini";
    }

    inline bool is_hidden(const std::filesystem::path path)
    {
        const std::filesystem::path::string_type name = path.filename();
        if (name == constants::non_localizable::desktop_ini_filename)
        {
            return true;
        }

        return false;
    }

    inline bool is_directory(const std::filesystem::path path)
    {
        const auto entry = std::filesystem::directory_entry(path);
        return entry.is_directory();
    }

    inline std::wstring make_valid_filename(const std::wstring& string, const wchar_t replace_with = L' ')
    {
        // replace all non-filename-valid chars with replace_with wchar
        std::wstring valid_filename = string;

        std::replace_if(valid_filename.begin(), valid_filename.end(), [](wchar_t c) { return c == L'/' || c == L'\\' || c == L':' || c == L'*' || c == L'?' || c == L'"' || c == L'<' || c == L'>' || c == L'|'; }, replace_with);

        return valid_filename;
    }

    inline std::wstring make_unique_path_name(const std::wstring& initial_path)
    {
        std::filesystem::path folder_path(initial_path);
        std::filesystem::path path_based_on(initial_path);

        int counter = 1;

        while (std::filesystem::exists(folder_path))
        {
            std::wstring new_filename = path_based_on.stem().wstring() + L" (" + std::to_wstring(counter) + L")";
            if (path_based_on.has_extension())
            {
                new_filename += path_based_on.extension().wstring();
            }
            folder_path = path_based_on.parent_path() / new_filename;
            counter++;
        }

        return folder_path.wstring();
    }
}
