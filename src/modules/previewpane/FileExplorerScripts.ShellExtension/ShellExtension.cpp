// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <wrl.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <algorithm>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

using namespace Microsoft::WRL;
using namespace winrt::Windows::Data::Json;

namespace
{
    HMODULE moduleHandle;

    std::filesystem::path DataDirectory()
    {
        PWSTR path = nullptr;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &path));
        const auto result = std::filesystem::path(path) / L"Microsoft" / L"PowerToys" / L"File Explorer" / L"PowerScripts";
        CoTaskMemFree(path);
        return result;
    }

    JsonObject ReadCatalogue()
    {
        const auto path = DataDirectory() / L"catalogue.json";
        std::error_code error;
        const auto size = std::filesystem::file_size(path, error);
        if (error || size > 1024 * 1024)
        {
            return JsonObject{};
        }

        std::ifstream stream(path, std::ios::binary);
        const std::string contents((std::istreambuf_iterator<char>(stream)), std::istreambuf_iterator<char>());
        JsonObject result;
        if (!JsonObject::TryParse(winrt::to_hstring(contents), result))
        {
            return JsonObject{};
        }

        const auto now = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::system_clock::now().time_since_epoch()).count();
        const auto age = now - result.GetNamedNumber(L"updated", 0);
        return age >= 0 && age < 60 ? result : JsonObject{};
    }

    std::vector<std::wstring> GetFiles(IShellItemArray* selection)
    {
        std::vector<std::wstring> files;
        DWORD count = 0;
        if (!selection || FAILED(selection->GetCount(&count)) || count == 0 || count > 1024)
        {
            return files;
        }

        for (DWORD i = 0; i < count; ++i)
        {
            ComPtr<IShellItem> item;
            PWSTR path = nullptr;
            if (FAILED(selection->GetItemAt(i, &item)) || FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                return {};
            }

            const auto attributes = GetFileAttributesW(path);
            const std::wstring name(path);
            CoTaskMemFree(path);
            if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)) ||
                name.size() > 32767 || name.starts_with(L"\\\\"))
            {
                return {};
            }

            files.push_back(name);
        }
        return files;
    }

    bool Accepts(const JsonObject& script, const std::vector<std::wstring>& files)
    {
        if (files.empty() || files.size() < script.GetNamedNumber(L"min", 0) || files.size() > script.GetNamedNumber(L"max", 1024))
        {
            return false;
        }
        const auto extensions = script.GetNamedArray(L"extensions", JsonArray{});
        return extensions.Size() == 0 || std::all_of(files.begin(), files.end(), [&](const auto& file) {
                   auto actual = std::filesystem::path(file).extension().wstring();
                   for (const auto& value : extensions)
                   {
                       std::wstring declared(value.GetString());
                       if (declared == L"*")
                       {
                           return true;
                       }
                       if (!declared.empty() && declared.front() != L'.')
                       {
                           declared.insert(declared.begin(), L'.');
                       }
                       if (CompareStringOrdinal(actual.c_str(), -1, declared.c_str(), -1, TRUE) == CSTR_EQUAL)
                       {
                           return true;
                       }
                   }
                   return false;
               });
    }

    HRESULT Launch(const std::wstring& id, const std::vector<std::wstring>& files)
    {
        if (files.empty())
        {
            return E_INVALIDARG;
        }
        wchar_t modulePath[32768]{};
        if (!GetModuleFileNameW(moduleHandle, modulePath, ARRAYSIZE(modulePath)))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        const auto exe = std::filesystem::path(modulePath).parent_path() / L"PowerToys.FileExplorerScripts.exe";
        GUID guid;
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t guidText[40]{};
        StringFromGUID2(guid, guidText, ARRAYSIZE(guidText));
        std::wstring requestId;
        for (const auto character : std::wstring(guidText))
        {
            if (iswxdigit(character))
            {
                requestId += character;
            }
        }
        const auto directory = DataDirectory() / L"Requests";
        std::filesystem::create_directories(directory);
        if (GetFileAttributesW(directory.c_str()) & FILE_ATTRIBUTE_REPARSE_POINT)
        {
            return E_ACCESSDENIED;
        }
        const auto path = directory / (requestId + L".json");
        JsonObject request;
        request.SetNamedValue(L"id", JsonValue::CreateStringValue(id));
        JsonArray array;
        for (const auto& file : files)
        {
            array.Append(JsonValue::CreateStringValue(file));
        }
        request.SetNamedValue(L"files", array);
        const auto bytes = winrt::to_string(request.Stringify());
        if (bytes.size() > 2 * 1024 * 1024)
        {
            return HRESULT_FROM_WIN32(ERROR_BUFFER_OVERFLOW);
        }
        const auto handle = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (handle == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        DWORD written = 0;
        const auto ok = WriteFile(handle, bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr);
        const auto writeError = GetLastError();
        CloseHandle(handle);
        if (!ok || written != bytes.size())
        {
            DeleteFileW(path.c_str());
            return HRESULT_FROM_WIN32(ok ? ERROR_WRITE_FAULT : writeError);
        }
        auto command = L"\"" + exe.wstring() + L"\" --request " + requestId;
        STARTUPINFOW startup{ sizeof(startup) };
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(exe.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, exe.parent_path().c_str(), &startup, &process))
        {
            const auto error = GetLastError();
            DeleteFileW(path.c_str());
            return HRESULT_FROM_WIN32(error);
        }
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return S_OK;
    }
}

class CommandEnumerator;

class __declspec(uuid("4D97E44F-258D-480D-AC48-7A43F3088284")) ScriptCommand :
    public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSelection>
{
public:
    ScriptCommand() = default;
    ScriptCommand(std::wstring id, std::wstring name, bool enabled, std::wstring reason, std::vector<std::wstring> files, std::wstring iconPath = {}) :
        root(false), id(std::move(id)), name(std::move(name)), enabled(enabled), reason(std::move(reason)), files(std::move(files)), iconLocation(iconPath.empty() ? L"" : L"\"" + iconPath + L"\",0")
    {
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* title) override
    {
        return SHStrDupW(name.c_str(), title);
    }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        if (!icon)
        {
            return E_POINTER;
        }
        *icon = nullptr;
        return iconLocation.empty() ? E_NOTIMPL : SHStrDupW(iconLocation.c_str(), icon);
    }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* tooltip) override
    {
        return SHStrDupW(reason.c_str(), tooltip);
    }
    IFACEMETHODIMP GetCanonicalName(GUID* guid) override
    {
        *guid = root ? __uuidof(ScriptCommand) : GUID_NULL;
        return S_OK;
    }
    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL, EXPCMDSTATE* state) override
    {
        *state = ECS_HIDDEN;
        try
        {
            if (!root)
            {
                *state = enabled ? ECS_ENABLED : ECS_DISABLED;
                return S_OK;
            }
            files = GetFiles(selection ? selection : storedSelection.Get());
            catalogue = ReadCatalogue();
            if (!files.empty() && catalogue.GetNamedBoolean(L"enabled", false))
            {
                const auto scripts = catalogue.GetNamedArray(L"scripts", JsonArray{});
                const auto available = catalogue.GetNamedBoolean(L"truncated", false) || catalogue.HasKey(L"error") ||
                                       std::any_of(scripts.begin(), scripts.end(), [&](const auto& script) { return Accepts(script.GetObject(), files); });
                *state = available ? ECS_ENABLED : ECS_HIDDEN;
            }
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }
    }
    IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
    {
        try
        {
            return Launch(id, selection ? GetFiles(selection) : files);
        }
        catch (...)
        {
            return winrt::to_hresult();
        }
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        *flags = root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** result) override;
    IFACEMETHODIMP SetSelection(IShellItemArray* selection) override
    {
        storedSelection = selection;
        return S_OK;
    }
    IFACEMETHODIMP GetSelection(REFIID iid, void** selection) override
    {
        return storedSelection ? storedSelection.CopyTo(iid, selection) : E_FAIL;
    }

private:
    bool root = true;
    std::wstring id;
    std::wstring name = L"PowerScripts";
    bool enabled = true;
    std::wstring reason;
    std::vector<std::wstring> files;
    std::wstring iconLocation;
    JsonObject catalogue;
    ComPtr<IShellItemArray> storedSelection;
};

class CommandEnumerator : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IEnumExplorerCommand>
{
public:
    std::vector<ComPtr<IExplorerCommand>> commands;
    size_t index = 0;
    IFACEMETHODIMP Next(ULONG count, IExplorerCommand** result, ULONG* fetched) override
    {
        if (!result || (!fetched && count != 1))
        {
            return E_POINTER;
        }
        ULONG copied = 0;
        while (copied < count && index < commands.size())
        {
            commands[index++].CopyTo(&result[copied++]);
        }
        if (fetched)
        {
            *fetched = copied;
        }
        return copied == count ? S_OK : S_FALSE;
    }
    IFACEMETHODIMP Skip(ULONG count) override
    {
        const auto remaining = commands.size() - index;
        index += std::min(static_cast<size_t>(count), remaining);
        return count <= remaining ? S_OK : S_FALSE;
    }
    IFACEMETHODIMP Reset() override
    {
        index = 0;
        return S_OK;
    }
    IFACEMETHODIMP Clone(IEnumExplorerCommand** result) override
    {
        const auto copy = Make<CommandEnumerator>();
        copy->commands = commands;
        copy->index = index;
        return copy.CopyTo(result);
    }
};

HRESULT ScriptCommand::EnumSubCommands(IEnumExplorerCommand** result)
{
    *result = nullptr;
    try
    {
        const auto list = Make<CommandEnumerator>();
        catalogue = ReadCatalogue();
        if (files.empty() && storedSelection)
        {
            files = GetFiles(storedSelection.Get());
        }
        const auto showParameters = catalogue.GetNamedBoolean(L"showParameters", true);
        auto needsPicker = catalogue.GetNamedBoolean(L"truncated", false) || catalogue.HasKey(L"error");
        for (const auto& item : catalogue.GetNamedArray(L"scripts", JsonArray{}))
        {
            const auto script = item.GetObject();
            if (Accepts(script, files))
            {
                if (list->commands.size() == 14)
                {
                    needsPicker = true;
                    break;
                }
                const auto canRun = showParameters || script.GetNamedBoolean(L"defaults", false);
                const std::wstring why = canRun ? L"" : std::wstring(catalogue.GetNamedString(L"missingDefaults", L"Requires parameter options"));
                std::wstring title(script.GetNamedString(L"name"));
                const auto iconValue = script.TryLookup(L"iconPath");
                const std::wstring icon = iconValue && iconValue.ValueType() == JsonValueType::String ? std::wstring(iconValue.GetString()) : L"";
                list->commands.push_back(Make<ScriptCommand>(std::wstring(script.GetNamedString(L"id")), title, canRun, why, files, icon));
            }
        }
        if (needsPicker)
        {
            list->commands.push_back(Make<ScriptCommand>(L"", std::wstring(catalogue.GetNamedString(L"more", L"Choose a script...")), true, L"", files));
        }
        return list.CopyTo(result);
    }
    catch (...)
    {
        return winrt::to_hresult();
    }
}

CoCreatableClass(ScriptCommand)

    BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** result)
{
    return Module<InProc>::GetModule().GetClassObject(clsid, iid, result);
}
