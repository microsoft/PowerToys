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
    // Filename cases to support
    // type      | filename                             | result
    // [file]    | 01. First entry.txt                  | First entry.txt
    // [folder]  | 02. Second entry                     | Second entry
    // [folder]  | 03 Third entry                       | Third entry
    // [file]    | 04 Fourth entry.txt                  | Fourth entry.txt
    // [file]    | 05.Fifth entry.txt                   | Fifth entry.txt
    // [folder]  | 001231                               | 001231
    // [file]    | 001231.txt                           | 001231.txt
    // [file]    | 13. 0123456789012345.txt             | 0123456789012345.txt

    std::filesystem::path filename_path(filename);
    const std::wstring stem = filename_path.stem().wstring();

    bool stem_is_only_digits = !stem.empty();
    for (const wchar_t c : stem)
    {
        if (c < L'0' || c > L'9')
        {
            stem_is_only_digits = false;
            break;
        }
    }

    if (stem_is_only_digits)
    {
        // Edge cases where digits ARE the filename.
        // If it's a file, we always keep it (e.g. 001231.txt or 001231).
        // If it's a folder, we only strip if it looks like it has an extension (which is actually part of the name for folders).
        // e.g. "0123.Name" -> Strip. "001231" -> Keep.
        const bool is_folder = helpers::filesystem::is_directory(path);
        const bool has_extension = filename_path.has_extension();

        if (!is_folder || !has_extension)
        {
            return filename;
        }
    }

    // Find end of leading digits
    size_t digits_end_index = 0;
    while (digits_end_index < filename.length() && filename[digits_end_index] >= L'0' && filename[digits_end_index] <= L'9')
    {
        digits_end_index++;
    }

    if (digits_end_index == 0)
    {
        // No leading digits
        return filename;
    }

    // Determine if we should also strip a separator (dot or space)
    size_t strip_length = digits_end_index;

    // Check patterns to strip separators:
    // 1. "01. Name" -> Strip "01. "
    // 2. "01 .Name" -> Strip "01 ."
    // 3. "01.Name"  -> Strip "01."
    // 4. "01 Name"  -> Strip "01 "
    // 5. "01Name"   -> Strip "01" (No separator)

    if (strip_length < filename.length())
    {
        if (filename[strip_length] == L'.')
        {
            strip_length++;
            // If dot is followed by space, strip that too (e.g. "01. Name")
            if (strip_length < filename.length() && filename[strip_length] == L' ')
            {
                strip_length++;
            }
        }
        else if (filename[strip_length] == L' ')
        {
            strip_length++;
            // If space is followed by dot, strip that too (e.g. "01 .Name")
            if (strip_length < filename.length() && filename[strip_length] == L'.')
            {
                strip_length++;
            }
        }
    }

    return filename.substr(strip_length);
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
