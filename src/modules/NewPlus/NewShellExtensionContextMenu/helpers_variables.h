#pragma once

#include <regex>
#include "..\..\powerrename\lib\Helpers.h"
#include "helpers_filesystem.h"

#pragma comment(lib, "Pathcch.lib")

namespace newplus::helpers::variables
{
    inline std::wstring resolve_an_environment_variable(const std::wstring& string)
    {
        std::wstring return_string = string;
        wchar_t* env_variable = nullptr;

        _wdupenv_s(&env_variable, nullptr, return_string.c_str());

        if (env_variable != nullptr)
        {
            return_string = env_variable;
            free(env_variable);
        }

        return return_string;
    }

    inline std::wstring resolve_date_time_variables(const std::wstring& string)
    {
        SYSTEMTIME local_now = { 0 };
        GetLocalTime(&local_now);
        wchar_t resolved_filename[MAX_PATH] = { 0 };
        GetDatedFileName(resolved_filename, ARRAYSIZE(resolved_filename), string.c_str(), local_now);

        return resolved_filename;
    }

    inline std::wstring replace_all_occurrences(const std::wstring& string, const std::wstring& search_for, const std::wstring& replacement)
    {
        std::wstring return_string = string;
        size_t pos = 0;

        while ((pos = return_string.find(search_for, pos)) != std::wstring::npos)
        {
            return_string.replace(pos, search_for.length(), replacement);
            pos += replacement.length();
        }

        return return_string;
    }

    inline std::wstring resolve_environment_variables(const std::wstring& string)
    {
        // Do case-insensitive string replacement of environment variables being consistent with normal %eNV_VaR% behavior
        std::wstring return_string = string;
        const std::wregex reg_expression(L"%([^%]+)%");
        std::wsmatch match;

        size_t start = 0;
        while (std::regex_search(return_string.cbegin() + start, return_string.cend(), match, reg_expression))
        {
            std::wstring env_var_name = match[1].str();
            std::wstring env_var_value = resolve_an_environment_variable(env_var_name);
            if (!env_var_value.empty())
            {
                size_t match_position = match.position(0) + start;
                return_string.replace(match_position, match.length(0), env_var_value);
                start = match_position + env_var_value.length();
            }
            else
            {
                start += match.position(0) + match.length(0);
            }
        }

        return return_string;
    }

    inline std::wstring resolve_parent_folder(const std::wstring& string, const std::wstring& parent_folder_name)
    {
        // Do case-sensitive string replacement, for consistency on variables designated with $
        std::wstring result = replace_all_occurrences(string, constants::non_localizable::parent_folder_name_variable, parent_folder_name);

        return result;
    }

    inline std::filesystem::path resolve_variables_in_filename(const std::wstring& filename, const std::wstring& parent_folder_name)
    {
        std::wstring result;

        result = resolve_date_time_variables(filename);
        result = resolve_environment_variables(result);
        if (!parent_folder_name.empty())
        {
            result = resolve_parent_folder(result, parent_folder_name);
        }
        result = newplus::helpers::filesystem::make_valid_filename(result);

        return result;
    }

    inline std::filesystem::path resolve_variables_in_path(const std::filesystem::path& path)
    {
        // Need to resolve the whole path top-down (root to leaf), because of the support for $PARENT_FOLDER_NAME
        std::filesystem::path result;
        std::wstring previous_section;
        std::wstring current_section;
        auto path_section = path.begin();
        int level = 0;

        while (path_section != path.end())
        {
            previous_section = current_section;
            current_section = path_section->wstring();

            if (level <= 1)
            {
                // Up to and including L"x:\\"
                result /= current_section;
            }
            else
            {
                // Past L"x:\\", e.g. L"x:\\level1" and beyond
                result /= resolve_variables_in_filename(current_section, previous_section);
            }
            path_section++;
            level++;
        }

        return result;
    }

    inline void resolve_variables_in_filename_and_rename_files(const std::filesystem::path& path, const bool do_rename = true)
    {
        // Depth first recursion, so that we start renaming the leaves, and avoid having to rescan
        for (const auto& entry : std::filesystem::directory_iterator(path))
        {
            if (std::filesystem::is_directory(entry.status()))
            {
                resolve_variables_in_filename_and_rename_files(entry.path(), do_rename);
            }
        }

        // Perform the actual rename
        for (const auto& current : std::filesystem::directory_iterator(path))
        {
            if (!newplus::helpers::filesystem::is_hidden(current))
            {
                const std::filesystem::path resolved_path = resolve_variables_in_path(current.path());

                // Only rename if the filename is actually different
                const std::wstring non_resolved_leaf = current.path().filename();
                const std::wstring resolved_leaf = resolved_path.filename();

                if (StrCmpIW(non_resolved_leaf.c_str(), resolved_leaf.c_str()) != 0)
                {
                    const std::wstring org_name = current.path();
                    const std::wstring new_name = current.path().parent_path() / resolved_leaf;
                    const std::wstring really_new_name = helpers::filesystem::make_unique_path_name(new_name);

                    // To aid with testing, only conditionally rename
                    if (do_rename)
                    {
                        std::filesystem::rename(org_name, really_new_name);
                    }
                }
            }
        }
    }
}