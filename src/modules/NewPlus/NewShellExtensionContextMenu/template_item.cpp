

#include "pch.h"
#include "template_item.h"
#include <shellapi.h>
#include "new_utilities.h"
#include <cassert>
#include <thread>
#include <shlobj_core.h>

using namespace Microsoft::WRL;
using namespace newplus;

template_item::template_item(const std::filesystem::path entry)
{
    path = entry;
}

std::wstring template_item::get_menu_title(const bool show_extension, const bool show_starting_digits, const bool show_resolved_variables) const
{
    std::wstring title = path.filename();

    if (!show_starting_digits)
    {
        // Hide starting digits, spaces, and .
        title = remove_starting_digits_from_filename(title);
    }

    if (show_resolved_variables)
    {
        title = helpers::variables::resolve_variables_in_filename(title, constants::non_localizable::parent_folder_name_variable);
    }

    if (show_extension || !path.has_extension())
    {
        return title;
    }

    if (!helpers::filesystem::is_directory(path))
    {
        std::wstring ext = path.extension();
        title = title.substr(0, title.length() - ext.length());
    }

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
    filename.erase(0, min(filename.find_first_not_of(L"0123456789"), filename.size()));
    filename.erase(0, min(filename.find_first_not_of(L" ."), filename.size()));

    return filename;
}

std::wstring template_item::get_explorer_icon() const
{
    return utilities::get_explorer_icon(path);
}

HICON template_item::get_explorer_icon_handle() const
{
    return utilities::get_explorer_icon_handle(path);
}

std::filesystem::path template_item::copy_object_to(const HWND window_handle, const std::filesystem::path destination) const
{
    // SHFILEOPSTRUCT wants the from and to paths to be terminated with two NULLs.
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
    file_operation_params.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | FOF_NOCOPYSECURITYATTRIBS;

    const int result = SHFileOperation(&file_operation_params);

    if (result != 0)
    {
        throw std::runtime_error("Failed to copy template");
    }

    return destination;
}

void template_item::refresh_target(const std::filesystem::path target_final_fullpath) const
{
    SHChangeNotify(SHCNE_CREATE, SHCNF_PATH | SHCNF_FLUSH, target_final_fullpath.wstring().c_str(), NULL);
}

void template_item::enter_rename_mode(const std::filesystem::path target_fullpath) const
{
    std::thread thread_for_renaming_workaround(rename_on_other_thread_workaround, target_fullpath);
    thread_for_renaming_workaround.detach();
}

void template_item::rename_on_other_thread_workaround(const std::filesystem::path target_fullpath)
{
    // Have been unable to have Windows Explorer Shell enter rename mode from the main thread
    // Sleep for a bit to only enter rename mode when icon has been drawn.
    const std::chrono::milliseconds approx_wait_for_icon_redraw_not_needed{ 50 };
    std::this_thread::sleep_for(std::chrono::milliseconds(approx_wait_for_icon_redraw_not_needed));

    newplus::utilities::explorer_enter_rename_mode(target_fullpath);
}
