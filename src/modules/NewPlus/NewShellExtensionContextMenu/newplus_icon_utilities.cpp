#include "pch.h"
// pch.h first
#include "newplus_icon_utilities.h"
#include <mutex>
#include <unordered_map>

#pragma comment(lib, "Shlwapi.lib")

namespace newplus::icon_utilities
{

namespace
{
    std::wstring query_default_icon(const wchar_t* association)
    {
        DWORD buffer_length = 0;
        const HRESULT size_result = AssocQueryString(
            ASSOCF_INIT_IGNOREUNKNOWN,
            ASSOCSTR_DEFAULTICON,
            association,
            nullptr,
            nullptr,
            &buffer_length);
        if (size_result != S_FALSE || buffer_length == 0)
        {
            return {};
        }

        std::wstring icon_resource(buffer_length, L'\0');
        const HRESULT query_result = AssocQueryString(
            ASSOCF_INIT_IGNOREUNKNOWN,
            ASSOCSTR_DEFAULTICON,
            association,
            nullptr,
            icon_resource.data(),
            &buffer_length);
        if (FAILED(query_result))
        {
            return {};
        }

        icon_resource.resize(wcsnlen_s(icon_resource.c_str(), icon_resource.size()));
        return icon_resource;
    }

    HICON extract_default_icon(const wchar_t* association)
    {
        const std::wstring icon_resource = query_default_icon(association);
        if (icon_resource.empty())
        {
            return nullptr;
        }

        const DWORD expanded_length = ExpandEnvironmentStrings(icon_resource.c_str(), nullptr, 0);
        if (expanded_length == 0)
        {
            return nullptr;
        }

        std::wstring icon_path(expanded_length, L'\0');
        const DWORD expand_result = ExpandEnvironmentStrings(icon_resource.c_str(), icon_path.data(), expanded_length);
        if (expand_result == 0 || expand_result > expanded_length)
        {
            return nullptr;
        }

        const int icon_index = PathParseIconLocation(icon_path.data());
        PathUnquoteSpaces(icon_path.data());
        icon_path.resize(wcsnlen_s(icon_path.c_str(), icon_path.size()));

        HICON icon = nullptr;
        const UINT icon_size = static_cast<UINT>(GetSystemMetrics(SM_CXSMICON));
        if (FAILED(SHDefExtractIcon(icon_path.c_str(), icon_index, 0, nullptr, &icon, MAKELONG(0, icon_size))))
        {
            return nullptr;
        }

        return icon;
    }
}

std::wstring get_explorer_icon(const std::filesystem::path& path, bool is_directory)
{
    // Cache by full path — directories are excluded because their icon can change via desktop.ini
    // without a DLL reload. Extension is intentionally NOT used as the key: icons for types like .exe
    // and .lnk are per-file (the icon comes from the binary/shortcut itself), so an extension key would
    // return the first-seen file's icon for every template of that type.
    if (!is_directory)
    {
        // Explorer can call into the shell extension on multiple threads concurrently, so the
        // process-wide cache must be synchronized to avoid a data race on the unordered_map.
        // The lock is only ever held around the map lookup/insert and never while calling into the
        // shell (SHGetFileInfo/AssocQueryString), because those calls can be reentrant and would
        // otherwise risk deadlocking this non-recursive mutex on the same thread.
        static std::mutex s_icon_cache_mutex;
        static std::unordered_map<std::wstring, std::wstring> s_icon_cache;
        const std::wstring key = path.wstring();

        {
            std::lock_guard<std::mutex> cache_lock(s_icon_cache_mutex);
            const auto it = s_icon_cache.find(key);
            if (it != s_icon_cache.end())
                return it->second;
        }

        std::wstring icon_resource;

        SHFILEINFO shell_file_info = { 0 };
        SHGetFileInfo(key.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICONLOCATION);
        const std::wstring icon_path = shell_file_info.szDisplayName;
        if (!icon_path.empty())
        {
            icon_resource = icon_path + L"," + std::to_wstring(shell_file_info.iIcon);
        }
        else
        {
            const std::wstring extension = path.extension().wstring();
            icon_resource = query_default_icon(extension.c_str());
        }

        {
            std::lock_guard<std::mutex> cache_lock(s_icon_cache_mutex);
            // Only cache successful (non-empty) lookups so a transient SHGetFileInfo/AssocQueryString
            // failure cannot permanently poison the cache with an empty icon for that path.
            if (!icon_resource.empty())
            {
                s_icon_cache[key] = icon_resource;
            }
        }

        return icon_resource;
    }

    // Directories: always read fresh from the shell
    SHFILEINFO shell_file_info = { 0 };
    const std::wstring filepath = path.wstring();
    SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICONLOCATION);
    const std::wstring icon_path = shell_file_info.szDisplayName;
    if (!icon_path.empty())
    {
        return icon_path + L"," + std::to_wstring(shell_file_info.iIcon);
    }

    return query_default_icon(L"");
}

HICON get_explorer_icon_handle(const std::filesystem::path& path)
{
    SHFILEINFO shell_file_info = { 0 };
    const std::wstring filepath = path.wstring();
    SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICON);
    if (shell_file_info.hIcon)
    {
        return shell_file_info.hIcon;
    }

    const std::wstring extension = path.extension().wstring();
    return extract_default_icon(extension.c_str());
}

}
