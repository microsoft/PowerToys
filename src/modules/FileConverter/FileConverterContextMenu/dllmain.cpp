// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "pch.h"

#include <Constants.h>
#include <FileConversionEngine.h>
#include <common/utils/resources.h>
#include "../Strings/FileConverterResourceIds.h"
#include <ShlObj.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/base.h>

#include <algorithm>
#include <array>
#include <cstdio>
#include <cwctype>
#include <optional>
#include <string>
#include <vector>

using namespace Microsoft::WRL;
namespace winrt_json = winrt::Windows::Data::Json;
namespace fc_constants = winrt::PowerToys::FileConverter::Constants;

namespace
{
    constexpr DWORD PIPE_CONNECT_TIMEOUT_MS = 1000;

    using file_converter::ImageFormat;

    struct TargetFormatSpec
    {
        UINT label_id;
        const wchar_t* label_fallback;
        const wchar_t* destination;
        ImageFormat format;
        GUID canonical_name;
    };

    constexpr std::array<TargetFormatSpec, 8> TARGET_FORMATS = {
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_PNG, L"PNG", fc_constants::FormatPng, ImageFormat::Png, { 0x0a4200f1, 0x74e5, 0x4f59, { 0xbb, 0x5d, 0x79, 0x8a, 0xfa, 0xf8, 0x01, 0x10 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_JPG, L"JPG", fc_constants::FormatJpg, ImageFormat::Jpeg, { 0x9f0adf10, 0x3fcb, 0x4a22, { 0x9e, 0x4a, 0x9c, 0x9c, 0x5e, 0xc1, 0x16, 0x4a } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_JPEG, L"JPEG", fc_constants::FormatJpeg, ImageFormat::Jpeg, { 0x6d94f15d, 0xa2ba, 0x4912, { 0xa8, 0xf6, 0xe3, 0x89, 0xe0, 0xf8, 0x50, 0x76 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_BMP, L"BMP", fc_constants::FormatBmp, ImageFormat::Bmp, { 0x922d3030, 0x7fdb, 0x4de7, { 0x99, 0x39, 0x15, 0x95, 0x38, 0x0e, 0x81, 0x88 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_TIFF, L"TIFF", fc_constants::FormatTiff, ImageFormat::Tiff, { 0x91fc7a8a, 0x34b9, 0x4ddf, { 0x86, 0xe8, 0x9f, 0xbb, 0x84, 0xf3, 0x55, 0x65 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_HEIC, L"HEIC", fc_constants::FormatHeic, ImageFormat::Heif, { 0xd10be4f8, 0x6e5f, 0x4c6d, { 0xa1, 0x45, 0xbe, 0x57, 0x9f, 0x42, 0x75, 0x69 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_HEIF, L"HEIF", fc_constants::FormatHeif, ImageFormat::Heif, { 0x7fce9037, 0x12fe, 0x40af, { 0x88, 0x95, 0x6e, 0x7f, 0xe6, 0x29, 0x2b, 0x45 } } },
        TargetFormatSpec{ IDS_FILECONVERTER_CONTEXTMENU_FORMAT_WEBP, L"WebP", fc_constants::FormatWebp, ImageFormat::Webp, { 0x5fce9315, 0x3d7b, 0x4372, { 0xac, 0x17, 0x35, 0x57, 0x91, 0xcd, 0x17, 0x61 } } },
    };

    std::wstring GetContextMenuParentLabel()
    {
        static const std::wstring label = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_CONTEXTMENU_ENTRY, L"Convert to...");
        return label;
    }

    std::wstring GetTargetFormatLabel(const TargetFormatSpec& spec)
    {
        return GET_RESOURCE_STRING_FALLBACK(spec.label_id, spec.label_fallback);
    }

    std::wstring GetPipeNameForCurrentSession()
    {
        DWORD session_id = 0;
        if (!ProcessIdToSessionId(GetCurrentProcessId(), &session_id))
        {
            session_id = 0;
        }

        return std::wstring(fc_constants::PipeNamePrefix) + std::to_wstring(session_id);
    }

    HRESULT GetSelectedPaths(IShellItemArray* selection, std::vector<std::wstring>& paths)
    {
        if (selection == nullptr)
        {
            return E_INVALIDARG;
        }

        paths.clear();

        DWORD count = 0;
        const HRESULT count_hr = selection->GetCount(&count);
        if (FAILED(count_hr))
        {
            return count_hr;
        }

        for (DWORD i = 0; i < count; ++i)
        {
            ComPtr<IShellItem> item;
            const HRESULT item_hr = selection->GetItemAt(i, &item);
            if (FAILED(item_hr) || item == nullptr)
            {
                continue;
            }

            PWSTR path_value = nullptr;
            const HRESULT path_hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path_value);
            if (FAILED(path_hr) || path_value == nullptr || path_value[0] == L'\0')
            {
                if (path_value != nullptr)
                {
                    CoTaskMemFree(path_value);
                }

                continue;
            }

            paths.emplace_back(path_value);
            CoTaskMemFree(path_value);
        }

        return paths.empty() ? E_FAIL : S_OK;
    }

    HRESULT GetSelectedPaths(IDataObject* data_object, std::vector<std::wstring>& paths)
    {
        if (data_object == nullptr)
        {
            return E_INVALIDARG;
        }

        ComPtr<IShellItemArray> shell_item_array;
        const HRESULT hr = SHCreateShellItemArrayFromDataObject(data_object, IID_PPV_ARGS(&shell_item_array));
        if (FAILED(hr))
        {
            return hr;
        }

        return GetSelectedPaths(shell_item_array.Get(), paths);
    }

    std::wstring ToLower(std::wstring value)
    {
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t ch) {
            return static_cast<wchar_t>(std::towlower(ch));
        });

        return value;
    }

    struct SourceFormatSpec
    {
        std::wstring_view extension;
        std::optional<ImageFormat> format;
    };

    constexpr auto SOURCE_FORMATS = std::to_array<SourceFormatSpec>({
        { fc_constants::ExtensionPng, ImageFormat::Png },
        { fc_constants::ExtensionJpg, ImageFormat::Jpeg },
        { fc_constants::ExtensionJpeg, ImageFormat::Jpeg },
        { L".jfif", ImageFormat::Jpeg },
        { L".jpe", ImageFormat::Jpeg },
        { fc_constants::ExtensionBmp, ImageFormat::Bmp },
        { L".dib", ImageFormat::Bmp },
        { fc_constants::ExtensionTif, ImageFormat::Tiff },
        { fc_constants::ExtensionTiff, ImageFormat::Tiff },
        { fc_constants::ExtensionHeic, ImageFormat::Heif },
        { fc_constants::ExtensionHeif, ImageFormat::Heif },
        { fc_constants::ExtensionWebp, ImageFormat::Webp },
        { L".gif", std::nullopt },
        { L".jxr", std::nullopt },
        { L".wdp", std::nullopt },
    });

    bool CanConvertPaths(const std::vector<std::wstring>& paths, ImageFormat destination)
    {
        if (paths.empty())
        {
            return false;
        }

        for (const auto& path : paths)
        {
            const auto extension = ToLower(PathFindExtension(path.c_str()));
            const auto source = std::ranges::find(SOURCE_FORMATS, extension, &SourceFormatSpec::extension);
            if (source == SOURCE_FORMATS.end() || source->format == destination)
            {
                return false;
            }
        }

        return true;
    }

    bool CanOfferDestination(const std::vector<std::wstring>& paths, ImageFormat destination)
    {
        return CanConvertPaths(paths, destination) && file_converter::IsOutputFormatSupported(destination).succeeded();
    }

    bool HasAnyAvailableDestination(const std::vector<std::wstring>& paths)
    {
        return std::ranges::any_of(TARGET_FORMATS, [&](const auto& target) {
            return CanOfferDestination(paths, target.format);
        });
    }

    const TargetFormatSpec* FindTargetFormat(std::wstring_view destination)
    {
        const std::wstring lower_destination = ToLower(std::wstring(destination));
        for (const auto& spec : TARGET_FORMATS)
        {
            if (lower_destination == spec.destination)
            {
                return &spec;
            }
        }

        return nullptr;
    }

    std::string BuildFormatConvertPayload(const std::vector<std::wstring>& paths, std::wstring_view destination)
    {
        winrt_json::JsonObject payload;
        payload.Insert(fc_constants::JsonActionKey, winrt_json::JsonValue::CreateStringValue(fc_constants::ActionFormatConvert));
        payload.Insert(fc_constants::JsonDestinationKey, winrt_json::JsonValue::CreateStringValue(destination.data()));

        winrt_json::JsonArray files;
        for (const auto& path : paths)
        {
            files.Append(winrt_json::JsonValue::CreateStringValue(path));
        }

        payload.Insert(fc_constants::JsonFilesKey, files);
        return winrt::to_string(payload.Stringify());
    }

    bool IsConversionServiceAvailable()
    {
        const std::wstring pipe_name = GetPipeNameForCurrentSession();
        if (WaitNamedPipeW(pipe_name.c_str(), NMPWAIT_NOWAIT))
        {
            return true;
        }

        // A busy pipe still belongs to a running service. Probing must not consume a connection.
        return GetLastError() == ERROR_SEM_TIMEOUT;
    }

    HRESULT SendFormatConvertRequest(const std::vector<std::wstring>& paths, std::wstring_view destination)
    {
        const TargetFormatSpec* target = FindTargetFormat(destination);
        if (target == nullptr || !CanConvertPaths(paths, target->format))
        {
            return E_INVALIDARG;
        }

        const std::wstring pipe_name = GetPipeNameForCurrentSession();
        if (!WaitNamedPipeW(pipe_name.c_str(), PIPE_CONNECT_TIMEOUT_MS))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        HANDLE pipe_handle = CreateFileW(
            pipe_name.c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            0,
            nullptr);

        if (pipe_handle == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        const std::string payload = BuildFormatConvertPayload(paths, target->destination);

        DWORD bytes_written = 0;
        const BOOL write_result = WriteFile(
            pipe_handle,
            payload.data(),
            static_cast<DWORD>(payload.size()),
            &bytes_written,
            nullptr);

        const DWORD write_error = write_result ? ERROR_SUCCESS : GetLastError();
        CloseHandle(pipe_handle);

        if (!write_result || bytes_written != payload.size())
        {
            return HRESULT_FROM_WIN32(write_result ? ERROR_WRITE_FAULT : write_error);
        }

        return S_OK;
    }

    class FileConverterSubCommand final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand>
    {
    public:
        explicit FileConverterSubCommand(const TargetFormatSpec& spec)
            : m_spec(spec)
        {
        }

        IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* name)
        {
            const auto label = GetTargetFormatLabel(m_spec);
            return SHStrDup(label.c_str(), name);
        }

        IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
        {
            *icon = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* info_tip)
        {
            *info_tip = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetCanonicalName(_Out_ GUID* guid_command_name)
        {
            *guid_command_name = m_spec.canonical_name;
            return S_OK;
        }

        IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL ok_to_be_slow, _Out_ EXPCMDSTATE* cmd_state)
        {
            *cmd_state = ECS_HIDDEN;

            if (selection == nullptr || !IsConversionServiceAvailable())
            {
                return S_OK;
            }

            if (!ok_to_be_slow)
            {
                return E_PENDING;
            }

            std::vector<std::wstring> paths;
            if (FAILED(GetSelectedPaths(selection, paths)))
            {
                return S_OK;
            }

            if (CanOfferDestination(paths, m_spec.format))
            {
                *cmd_state = ECS_ENABLED;
            }

            return S_OK;
        }

        IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*)
        {
            if (selection == nullptr)
            {
                return S_OK;
            }

            std::vector<std::wstring> paths;
            if (SUCCEEDED(GetSelectedPaths(selection, paths)))
            {
                const HRESULT hr = SendFormatConvertRequest(paths, m_spec.destination);
                if (FAILED(hr))
                {
                    wchar_t message[256]{};
                    swprintf_s(message, L"[FileConverterContextMenu] SendFormatConvertRequest failed: 0x%08lx\n", static_cast<unsigned long>(hr));
                    OutputDebugStringW(message);
                }
            }

            return S_OK;
        }

        IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* flags)
        {
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enum_commands)
        {
            *enum_commands = nullptr;
            return E_NOTIMPL;
        }

    private:
        TargetFormatSpec m_spec;
    };

    class FileConverterSubCommandEnumerator final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IEnumExplorerCommand>
    {
    public:
        FileConverterSubCommandEnumerator()
        {
            for (const auto& spec : TARGET_FORMATS)
            {
                m_commands.push_back(Make<FileConverterSubCommand>(spec));
            }
        }

        IFACEMETHODIMP Next(ULONG celt, __out_ecount_part(celt, *pceltFetched) IExplorerCommand** ap_ui_command, __out_opt ULONG* pcelt_fetched)
        {
            if (ap_ui_command == nullptr)
            {
                return E_POINTER;
            }

            ULONG fetched = 0;
            if (pcelt_fetched != nullptr)
            {
                *pcelt_fetched = 0;
            }

            while (fetched < celt && m_current_index < m_commands.size())
            {
                m_commands[m_current_index].CopyTo(&ap_ui_command[fetched]);
                ++m_current_index;
                ++fetched;
            }

            if (pcelt_fetched != nullptr)
            {
                *pcelt_fetched = fetched;
            }

            return fetched == celt ? S_OK : S_FALSE;
        }

        IFACEMETHODIMP Skip(ULONG celt)
        {
            m_current_index = (std::min)(m_current_index + static_cast<size_t>(celt), m_commands.size());
            return m_current_index < m_commands.size() ? S_OK : S_FALSE;
        }

        IFACEMETHODIMP Reset()
        {
            m_current_index = 0;
            return S_OK;
        }

        IFACEMETHODIMP Clone(__deref_out IEnumExplorerCommand** ppenum)
        {
            *ppenum = nullptr;
            return E_NOTIMPL;
        }

    private:
        std::vector<ComPtr<IExplorerCommand>> m_commands;
        size_t m_current_index = 0;
    };
}

HINSTANCE g_module_instance = 0;

BOOL APIENTRY DllMain(HMODULE module_handle, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module_instance = module_handle;
    }

    return TRUE;
}

class __declspec(uuid("57EC18F5-24D5-4DC6-AE2E-9D0F7A39F8BA")) FileConverterContextMenuCommand final :
    public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite, IShellExtInit, IContextMenu>
{
public:
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* name)
    {
        const auto label = GetContextMenuParentLabel();
        return SHStrDup(label.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
    {
        *icon = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* info_tip)
    {
        *info_tip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* guid_command_name)
    {
        *guid_command_name = __uuidof(this);
        return S_OK;
    }

    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL ok_to_be_slow, _Out_ EXPCMDSTATE* cmd_state)
    {
        *cmd_state = ECS_HIDDEN;

        if (selection == nullptr || !IsConversionServiceAvailable())
        {
            return S_OK;
        }

        if (!ok_to_be_slow)
        {
            return E_PENDING;
        }

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(selection, paths)))
        {
            return S_OK;
        }

        if (HasAnyAvailableDestination(paths))
        {
            *cmd_state = ECS_ENABLED;
        }

        return S_OK;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*)
    {
        UNREFERENCED_PARAMETER(selection);
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* flags)
    {
        *flags = ECF_HASSUBCOMMANDS;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enum_commands)
    {
        auto enumerator = Make<FileConverterSubCommandEnumerator>();
        return enumerator->QueryInterface(IID_PPV_ARGS(enum_commands));
    }

    IFACEMETHODIMP SetSite(_In_ IUnknown* site)
    {
        m_site = site;
        return S_OK;
    }

    IFACEMETHODIMP GetSite(_In_ REFIID riid, _COM_Outptr_ void** site)
    {
        return m_site.CopyTo(riid, site);
    }

    IFACEMETHODIMP Initialize(_In_opt_ PCIDLIST_ABSOLUTE, _In_opt_ IDataObject* data_object, _In_opt_ HKEY)
    {
        m_data_object = data_object;
        return S_OK;
    }

    IFACEMETHODIMP QueryContextMenu(HMENU menu, UINT index_menu, UINT id_cmd_first, UINT id_cmd_last, UINT flags)
    {
        if (menu == nullptr)
        {
            return E_INVALIDARG;
        }

        m_context_menu_target_indexes.clear();
        if ((flags & CMF_DEFAULTONLY) != 0 || id_cmd_first > id_cmd_last || m_data_object == nullptr || !IsConversionServiceAvailable())
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(m_data_object.Get(), paths)))
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        HMENU sub_menu = CreatePopupMenu();
        if (sub_menu == nullptr)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        UINT command_id = id_cmd_first;
        UINT sub_menu_index = 0;
        for (size_t i = 0; i < TARGET_FORMATS.size(); ++i)
        {
            const auto& format = TARGET_FORMATS[i];
            if (!CanOfferDestination(paths, format.format))
            {
                continue;
            }

            const auto target_label = GetTargetFormatLabel(format);

            if (!InsertMenuW(sub_menu, sub_menu_index, MF_BYPOSITION | MF_STRING, command_id, target_label.c_str()))
            {
                const HRESULT hr = HRESULT_FROM_WIN32(GetLastError());
                DestroyMenu(sub_menu);
                m_context_menu_target_indexes.clear();
                return hr;
            }

            m_context_menu_target_indexes.push_back(i);
            if (command_id == id_cmd_last)
            {
                break;
            }

            ++command_id;
            ++sub_menu_index;
        }

        if (m_context_menu_target_indexes.empty())
        {
            DestroyMenu(sub_menu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        const auto parent_label = GetContextMenuParentLabel();
        if (!InsertMenuW(menu, index_menu, MF_BYPOSITION | MF_POPUP | MF_STRING, reinterpret_cast<UINT_PTR>(sub_menu), parent_label.c_str()))
        {
            const HRESULT hr = HRESULT_FROM_WIN32(GetLastError());
            DestroyMenu(sub_menu);
            m_context_menu_target_indexes.clear();
            return hr;
        }

        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, static_cast<USHORT>(m_context_menu_target_indexes.size()));
    }

    IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* invoke_info)
    {
        if (invoke_info == nullptr || m_data_object == nullptr)
        {
            return S_OK;
        }

        if (!IS_INTRESOURCE(invoke_info->lpVerb))
        {
            return S_OK;
        }

        const UINT command_index = LOWORD(invoke_info->lpVerb);
        if (command_index >= m_context_menu_target_indexes.size())
        {
            return S_OK;
        }

        const size_t target_index = m_context_menu_target_indexes[command_index];
        if (target_index >= TARGET_FORMATS.size())
        {
            return S_OK;
        }

        const auto& target = TARGET_FORMATS[target_index];

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(m_data_object.Get(), paths)) || !CanConvertPaths(paths, target.format))
        {
            return S_OK;
        }

        const HRESULT hr = SendFormatConvertRequest(paths, target.destination);
        if (FAILED(hr))
        {
            wchar_t message[256]{};
            swprintf_s(message, L"[FileConverterContextMenu] SendFormatConvertRequest failed: 0x%08lx\n", static_cast<unsigned long>(hr));
            OutputDebugStringW(message);
        }

        return S_OK;
    }

    IFACEMETHODIMP GetCommandString(UINT_PTR, UINT, UINT*, LPSTR, UINT)
    {
        return E_NOTIMPL;
    }

private:
    ComPtr<IUnknown> m_site;
    ComPtr<IDataObject> m_data_object;
    std::vector<size_t> m_context_menu_target_indexes;
};

CoCreatableClass(FileConverterContextMenuCommand)
CoCreatableClassWrlCreatorMapInclude(FileConverterContextMenuCommand)

STDAPI DllGetActivationFactory(_In_ HSTRING activatableClassId, _COM_Outptr_ IActivationFactory** factory)
{
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatableClassId, factory);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** instance)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, instance);
}
