// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "pch.h"

#include <Constants.h>
#include <ShlObj.h>
#include <winrt/Windows.ApplicationModel.Resources.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/base.h>

#include <algorithm>
#include <array>
#include <cwctype>
#include <optional>
#include <string>
#include <vector>

using namespace Microsoft::WRL;
namespace json = winrt::Windows::Data::Json;
namespace fc_constants = winrt::PowerToys::FileConverter::Constants;

namespace
{
    constexpr DWORD PIPE_CONNECT_TIMEOUT_MS = 1000;

    enum class FormatGroup
    {
        Png,
        Jpeg,
        Bmp,
        Tiff,
        Heif,
        Webp,
        Unknown,
    };

    struct TargetFormatSpec
    {
        const wchar_t* label_key;
        const wchar_t* label_fallback;
        const wchar_t* destination;
        FormatGroup destination_group;
        GUID canonical_name;
    };

    constexpr std::array<TargetFormatSpec, 8> TARGET_FORMATS = {
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Png", L"PNG", fc_constants::FormatPng, FormatGroup::Png, { 0x0a4200f1, 0x74e5, 0x4f59, { 0xbb, 0x5d, 0x79, 0x8a, 0xfa, 0xf8, 0x01, 0x10 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Jpg", L"JPG", fc_constants::FormatJpg, FormatGroup::Jpeg, { 0x9f0adf10, 0x3fcb, 0x4a22, { 0x9e, 0x4a, 0x9c, 0x9c, 0x5e, 0xc1, 0x16, 0x4a } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Jpeg", L"JPEG", fc_constants::FormatJpeg, FormatGroup::Jpeg, { 0x6d94f15d, 0xa2ba, 0x4912, { 0xa8, 0xf6, 0xe3, 0x89, 0xe0, 0xf8, 0x50, 0x76 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Bmp", L"BMP", fc_constants::FormatBmp, FormatGroup::Bmp, { 0x922d3030, 0x7fdb, 0x4de7, { 0x99, 0x39, 0x15, 0x95, 0x38, 0x0e, 0x81, 0x88 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Tiff", L"TIFF", fc_constants::FormatTiff, FormatGroup::Tiff, { 0x91fc7a8a, 0x34b9, 0x4ddf, { 0x86, 0xe8, 0x9f, 0xbb, 0x84, 0xf3, 0x55, 0x65 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Heic", L"HEIC", fc_constants::FormatHeic, FormatGroup::Heif, { 0xd10be4f8, 0x6e5f, 0x4c6d, { 0xa1, 0x45, 0xbe, 0x57, 0x9f, 0x42, 0x75, 0x69 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Heif", L"HEIF", fc_constants::FormatHeif, FormatGroup::Heif, { 0x7fce9037, 0x12fe, 0x40af, { 0x88, 0x95, 0x6e, 0x7f, 0xe6, 0x29, 0x2b, 0x45 } } },
        TargetFormatSpec{ L"FileConverter_ContextMenu_Format_Webp", L"WebP", fc_constants::FormatWebp, FormatGroup::Webp, { 0x5fce9315, 0x3d7b, 0x4372, { 0xac, 0x17, 0x35, 0x57, 0x91, 0xcd, 0x17, 0x61 } } },
    };

    std::wstring LoadLocalizedString(std::wstring_view key, std::wstring_view fallback)
    {
        try
        {
            static const auto loader = winrt::Windows::ApplicationModel::Resources::ResourceLoader::GetForViewIndependentUse(L"Resources");
            const auto value = loader.GetString(winrt::hstring{ key });
            if (!value.empty())
            {
                return value.c_str();
            }
        }
        catch (...)
        {
        }

        return std::wstring{ fallback };
    }

    std::wstring GetContextMenuParentLabel()
    {
        static const std::wstring label = LoadLocalizedString(L"FileConverter_ContextMenu_Entry", L"Convert to...");
        return label;
    }

    std::wstring GetTargetFormatLabel(const TargetFormatSpec& spec)
    {
        return LoadLocalizedString(spec.label_key, spec.label_fallback);
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

    FormatGroup ExtensionToGroup(const std::wstring& extension)
    {
        const std::wstring lower = ToLower(extension);
        if (lower == fc_constants::ExtensionPng)
        {
            return FormatGroup::Png;
        }

        if (lower == fc_constants::ExtensionJpg || lower == fc_constants::ExtensionJpeg)
        {
            return FormatGroup::Jpeg;
        }

        if (lower == fc_constants::ExtensionBmp)
        {
            return FormatGroup::Bmp;
        }

        if (lower == fc_constants::ExtensionTif || lower == fc_constants::ExtensionTiff)
        {
            return FormatGroup::Tiff;
        }

        if (lower == fc_constants::ExtensionHeic || lower == fc_constants::ExtensionHeif)
        {
            return FormatGroup::Heif;
        }

        if (lower == fc_constants::ExtensionWebp)
        {
            return FormatGroup::Webp;
        }

        return FormatGroup::Unknown;
    }

    bool IsPathEligibleSource(const std::wstring& path, FormatGroup& group)
    {
        const wchar_t* extension = PathFindExtension(path.c_str());
        if (extension == nullptr || extension[0] == L'\0')
        {
            return false;
        }

        group = ExtensionToGroup(extension);
        if (group == FormatGroup::Unknown)
        {
            return false;
        }

#pragma warning(suppress : 26812)
        PERCEIVED perceived_type = PERCEIVED_TYPE_UNSPECIFIED;
        PERCEIVEDFLAG perceived_flags = PERCEIVEDFLAG_UNDEFINED;
        AssocGetPerceivedType(extension, &perceived_type, &perceived_flags, nullptr);
        return perceived_type == PERCEIVED_TYPE_IMAGE;
    }

    bool CanConvertPaths(const std::vector<std::wstring>& paths, std::optional<FormatGroup> destination_group)
    {
        if (paths.empty())
        {
            return false;
        }

        for (const auto& path : paths)
        {
            FormatGroup source_group = FormatGroup::Unknown;
            if (!IsPathEligibleSource(path, source_group))
            {
                return false;
            }

            if (destination_group.has_value() && source_group == destination_group.value())
            {
                return false;
            }
        }

        return true;
    }

    bool HasAnyAvailableDestination(const std::vector<std::wstring>& paths)
    {
        for (const auto& spec : TARGET_FORMATS)
        {
            if (CanConvertPaths(paths, spec.destination_group))
            {
                return true;
            }
        }

        return false;
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
        json::JsonObject payload;
        payload.Insert(fc_constants::JsonActionKey, json::JsonValue::CreateStringValue(fc_constants::ActionFormatConvert));
        payload.Insert(fc_constants::JsonDestinationKey, json::JsonValue::CreateStringValue(destination.data()));

        json::JsonArray files;
        for (const auto& path : paths)
        {
            files.Append(json::JsonValue::CreateStringValue(path));
        }

        payload.Insert(fc_constants::JsonFilesKey, files);
        return winrt::to_string(payload.Stringify());
    }

    HRESULT SendFormatConvertRequest(const std::vector<std::wstring>& paths, std::wstring_view destination)
    {
        const TargetFormatSpec* target = FindTargetFormat(destination);
        if (target == nullptr || !CanConvertPaths(paths, target->destination_group))
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

        IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL, _Out_ EXPCMDSTATE* cmd_state)
        {
            *cmd_state = ECS_HIDDEN;

            if (selection == nullptr)
            {
                return S_OK;
            }

            std::vector<std::wstring> paths;
            if (FAILED(GetSelectedPaths(selection, paths)))
            {
                return S_OK;
            }

            if (CanConvertPaths(paths, m_spec.destination_group))
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
                (void)SendFormatConvertRequest(paths, m_spec.destination);
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

    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL, _Out_ EXPCMDSTATE* cmd_state)
    {
        *cmd_state = ECS_HIDDEN;

        if (selection == nullptr)
        {
            return S_OK;
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

    IFACEMETHODIMP QueryContextMenu(HMENU menu, UINT index_menu, UINT id_cmd_first, UINT, UINT flags)
    {
        if (menu == nullptr)
        {
            return E_INVALIDARG;
        }

        if ((flags & CMF_DEFAULTONLY) != 0 || m_data_object == nullptr)
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(m_data_object.Get(), paths)) || !HasAnyAvailableDestination(paths))
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        HMENU sub_menu = CreatePopupMenu();
        if (sub_menu == nullptr)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        m_context_menu_target_indexes.clear();
        UINT command_id = id_cmd_first;
        UINT sub_menu_index = 0;
        for (size_t i = 0; i < TARGET_FORMATS.size(); ++i)
        {
            const auto& format = TARGET_FORMATS[i];
            if (!CanConvertPaths(paths, format.destination_group))
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
        if (FAILED(GetSelectedPaths(m_data_object.Get(), paths)) || !CanConvertPaths(paths, target.destination_group))
        {
            return S_OK;
        }

        (void)SendFormatConvertRequest(paths, target.destination);
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
