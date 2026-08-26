#include "pch.h"
#include "template_item.h"
#include "newplus_icon_utilities.h"
#include "new_utilities.h"
#include <chrono>
#include <thread>
#include <shlobj_core.h>

using namespace Microsoft::WRL;
using namespace newplus;

namespace
{
    struct rename_worker_context
    {
        std::filesystem::path target_fullpath;
        POINT mouse_position_at_invoke;
        HMODULE module_reference;
    };
}

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
    // Use the non-throwing filesystem query: this runs while Explorer builds the context menu, so a
    // throwing directory check here could take down the shell extension. On error, treat as a file.
    std::error_code ec;
    const bool is_dir = std::filesystem::is_directory(path, ec) && !ec;
    return icon_utilities::get_explorer_icon(path, is_dir);
}

HICON template_item::get_explorer_icon_handle() const
{
    return icon_utilities::get_explorer_icon_handle(path);
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

void template_item::enter_rename_mode(const std::filesystem::path target_fullpath, const POINT mouse_position_at_invoke) const
{
    HMODULE module_reference = nullptr;
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
            reinterpret_cast<LPCWSTR>(&module_instance_handle),
            &module_reference))
    {
        return;
    }

    std::unique_ptr<rename_worker_context> context;
    try
    {
        context = std::make_unique<rename_worker_context>(
            target_fullpath,
            mouse_position_at_invoke,
            module_reference);
    }
    catch (...)
    {
        FreeLibrary(module_reference);
        return;
    }

    active_rename_workers.fetch_add(1);
    const HANDLE thread = CreateThread(nullptr, 0, rename_worker_thread_proc, context.get(), 0, nullptr);
    if (thread == nullptr)
    {
        active_rename_workers.fetch_sub(1);
        FreeLibrary(module_reference);
        return;
    }

    context.release();
    CloseHandle(thread);
}

DWORD WINAPI template_item::rename_worker_thread_proc(void* parameter)
{
    std::unique_ptr<rename_worker_context> context(static_cast<rename_worker_context*>(parameter));
    const HMODULE module_reference = context->module_reference;

    rename_on_other_thread_workaround(context->target_fullpath, context->mouse_position_at_invoke);
    context.reset();
    active_rename_workers.fetch_sub(1);
    FreeLibraryAndExitThread(module_reference, 0);
}

void template_item::rename_on_other_thread_workaround(const std::filesystem::path& target_fullpath, const POINT mouse_position_at_invoke)
{
    struct worker_cleanup
    {
        bool com_initialized = false;

        ~worker_cleanup()
        {
            if (com_initialized)
            {
                CoUninitialize();
            }
        }
    } cleanup;

    const HRESULT com_result = CoInitializeEx(nullptr, COINIT_MULTITHREADED | COINIT_DISABLE_OLE1DDE);
    if (FAILED(com_result))
    {
        return;
    }
    cleanup.com_initialized = true;

    // Have been unable to have Windows Explorer Shell enter rename mode from the main thread.
    // Poll until the item appears in the folder view so icon is positioned and rename mode is entered
    // without a jump in the positioning
    constexpr std::chrono::milliseconds initial_poll_interval{ 30 };
    constexpr std::chrono::milliseconds maximum_poll_interval{ 240 };
    constexpr std::chrono::milliseconds poll_timeout{ 2000 };
    const auto deadline = std::chrono::steady_clock::now() + poll_timeout;
    auto poll_interval = initial_poll_interval;

    try
    {
        while (std::chrono::steady_clock::now() < deadline)
        {
            if (newplus::utilities::explorer_enter_rename_mode_and_reposition(target_fullpath, mouse_position_at_invoke))
            {
                return;
            }
            std::this_thread::sleep_for(poll_interval);
            poll_interval = std::min(poll_interval * 2, maximum_poll_interval);
        }

        // Final attempt: the item may have appeared during the last sleep interval (after the previous
        // attempt but before the deadline), so try once more so a just-in-time item still enters rename mode.
        newplus::utilities::explorer_enter_rename_mode_and_reposition(target_fullpath, mouse_position_at_invoke);
    }
    catch (...)
    {
    }
}
