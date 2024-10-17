

#include "pch.h"
#include "template_item.h"
#include <shellapi.h>
#include "new_utilities.h"
#include <cassert>
#include <thread>
#include <shellapi.h>
#include <shlobj_core.h>

using namespace Microsoft::WRL;
using namespace newplus;

template_item::template_item(const std::filesystem::path entry)
{
    path = entry;
}

std::wstring template_item::get_menu_title(const bool show_extension, const bool show_starting_digits) const
{
    std::wstring title = path.filename();

    if (!show_starting_digits)
    {
        // Hide starting digits, spaces, and .
        title = remove_starting_digits_from_filename(title);
    }

    if (show_extension || !path.has_extension())
    {
        return title;
    }

    std::wstring ext = path.extension();
    title = title.substr(0, title.length() - ext.length());

    return title;
}

std::wstring template_item::get_target_filename(const bool include_starting_digits) const
{
    std::wstring filename = path.filename();

    if (!include_starting_digits)
    {
        // Remove starting digits, spaces, and .
        filename = remove_starting_digits_from_filename(filename);
    }

    return filename;
}

std::wstring template_item::remove_starting_digits_from_filename(std::wstring filename) const
{
    filename.erase(0, min(filename.find_first_not_of(L"0123456789 ."), filename.size()));

    return filename;
}

std::wstring template_item::get_explorer_icon() const
{
    return utilities::get_explorer_icon(path);
}

std::filesystem::path template_item::copy_object_to(const HWND window_handle, const std::filesystem::path destination) const
{
    // SHFILEOPSTRUCT wants the from and to paths to be terminated with two NULLs,
    wchar_t double_terminated_path_from[MAX_PATH + 1] = { 0 };
    wcsncpy_s(double_terminated_path_from, this->path.c_str(), this->path.wstring().length());
    double_terminated_path_from[this->path.wstring().length() + 1] = 0;

    wchar_t double_terminated_path_to[MAX_PATH + 1] = { 0 };
    wcsncpy_s(double_terminated_path_to, destination.c_str(), destination.wstring().length());
    double_terminated_path_to[destination.wstring().length() + 1] = 0;

    SHFILEOPSTRUCT file_operation_params = { 0 };
    file_operation_params.wFunc = FO_COPY;
    file_operation_params.hwnd = window_handle;
    file_operation_params.pFrom = double_terminated_path_from;
    file_operation_params.pTo = double_terminated_path_to;
    file_operation_params.fFlags = FOF_RENAMEONCOLLISION | FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | FOF_NOCOPYSECURITYATTRIBS | FOF_WANTMAPPINGHANDLE;

    const int result = SHFileOperation(&file_operation_params);

    if (!file_operation_params.hNameMappings)
    {
        // No file name collision on copy
        return destination;
    }

    struct file_operation_collision_mapping
    {
        int index;
        SHNAMEMAPPING* mapping;
    };

    file_operation_collision_mapping* mapping = static_cast<file_operation_collision_mapping*>(file_operation_params.hNameMappings);
    SHNAMEMAPPING* map = &mapping->mapping[0];
    std::wstring final_path(map->pszNewPath);

    SHFreeNameMappings(file_operation_params.hNameMappings);

    return final_path;
}

void template_item::enter_rename_mode(const ComPtr<IUnknown> site, const std::filesystem::path target_fullpath) const
{
    std::thread thread_for_renaming_workaround(rename_on_other_thread_workaround, site, target_fullpath);
    thread_for_renaming_workaround.detach();
}

void template_item::rename_on_other_thread_workaround(const ComPtr<IUnknown> site, const std::filesystem::path target_fullpath)
{
    // Have been unable to have Windows Explorer Shell enter rename mode from the main thread
    // Sleep for a bit to only enter rename mode when icon has been drawn. Not strictly needed.
    const std::chrono::milliseconds approx_wait_for_icon_redraw_not_needed{ 350 };
    std::this_thread::sleep_for(std::chrono::milliseconds(approx_wait_for_icon_redraw_not_needed));

    const std::wstring filename = target_fullpath.filename();

    ComPtr<IServiceProvider> service_provider;
    site->QueryInterface(IID_PPV_ARGS(&service_provider));
    ComPtr<IFolderView> folder_view;
    service_provider->QueryService(__uuidof(IFolderView), IID_PPV_ARGS(&folder_view));

    int count = 0;
    folder_view->ItemCount(SVGIO_ALLVIEW, &count);

    for (int i = 0; i < count; ++i)
    {
        std::wstring path_of_item(MAX_PATH, 0);
        LPITEMIDLIST pidl;

        folder_view->Item(i, &pidl);
        SHGetPathFromIDList(pidl, &path_of_item[0]);
        CoTaskMemFree(pidl);

        std::wstring current_filename = std::filesystem::path(path_of_item.c_str()).filename();

        if (utilities::wstring_same_when_comparing_ignore_case(filename, current_filename))
        {
            folder_view->SelectItem(i, SVSI_EDIT | SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED);
            break;
        }
    }
}
