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
#include <cstring>
#include <filesystem>
#include <string>
#include <string_view>
#include <vector>

using namespace Microsoft::WRL;
using namespace winrt::Windows::Data::Json;

namespace
{
    HMODULE moduleHandle;

    winrt::handle OpenCatalogue(const std::filesystem::path& path)
    {
        const auto raw = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        return winrt::handle{ raw == INVALID_HANDLE_VALUE ? nullptr : raw };
    }

    std::filesystem::path DataDirectory()
    {
        PWSTR path = nullptr;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &path));
        const auto result = std::filesystem::path(path) / L"Microsoft" / L"PowerToys" / L"File Explorer" / L"PowerScripts";
        CoTaskMemFree(path);
        return result;
    }

    JsonObject ReadCatalogue(bool okToBeSlow = false)
    {
        const auto path = DataDirectory() / L"catalogue.json";
        auto file = OpenCatalogue(path);
        auto error = file ? ERROR_SUCCESS : GetLastError();
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(25);
        // ReplaceFile briefly publishes its exclusively held replacement handle.
        while (!file && error == ERROR_SHARING_VIOLATION)
        {
            if (!okToBeSlow)
            {
                winrt::throw_hresult(HRESULT_FROM_WIN32(error));
            }
            if (std::chrono::steady_clock::now() >= deadline)
            {
                return JsonObject{};
            }
            Sleep(1);
            file = OpenCatalogue(path);
            error = file ? ERROR_SUCCESS : GetLastError();
        }
        LARGE_INTEGER size{};
        if (!file || !GetFileSizeEx(file.get(), &size) || size.QuadPart <= 0 || size.QuadPart > 1024 * 1024)
        {
            return JsonObject{};
        }

        std::string contents(static_cast<size_t>(size.QuadPart), '\0');
        DWORD read = 0;
        if (!ReadFile(file.get(), contents.data(), static_cast<DWORD>(contents.size()), &read, nullptr) || read != contents.size())
        {
            return JsonObject{};
        }

        JsonObject result;
        if (!JsonObject::TryParse(winrt::to_hstring(contents), result))
        {
            return JsonObject{};
        }

        const auto now = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::system_clock::now().time_since_epoch()).count();
        const auto age = now - result.GetNamedNumber(L"updated", 0);
        return age >= 0 && age < 60 ? result : JsonObject{};
    }

    bool IsSafePathComponent(std::wstring_view name)
    {
        if (name.empty() || name.size() > 255 || name == L"." || name == L".." || name.back() == L'.' || name.back() == L' ' ||
            std::all_of(name.begin(), name.end(), [](wchar_t character) {
                return character == L' ' || character == L'\x0085' || character == L'\x00a0' || character == L'\x1680' ||
                       (character >= L'\x2000' && character <= L'\x200a') || character == L'\x2028' || character == L'\x2029' ||
                       character == L'\x202f' || character == L'\x205f' || character == L'\x3000';
            }) ||
            std::any_of(name.begin(), name.end(), [](wchar_t character) { return character < 32 || std::wstring_view(L"<>:\"/\\|?*").find(character) != std::wstring_view::npos; }))
        {
            return false;
        }

        auto stem = name.substr(0, name.find(L'.'));
        while (!stem.empty() && stem.back() == L' ')
        {
            stem.remove_suffix(1);
        }

        const auto equals = [&](std::wstring_view value) {
            return CompareStringOrdinal(stem.data(), static_cast<int>(stem.size()), value.data(), static_cast<int>(value.size()), TRUE) == CSTR_EQUAL;
        };
        for (const auto reserved : { L"CON", L"PRN", L"AUX", L"NUL", L"CONIN$", L"CONOUT$" })
        {
            if (equals(reserved))
            {
                return false;
            }
        }

        if (stem.size() == 4 &&
            (CompareStringOrdinal(stem.data(), 3, L"COM", 3, TRUE) == CSTR_EQUAL || CompareStringOrdinal(stem.data(), 3, L"LPT", 3, TRUE) == CSTR_EQUAL) &&
            ((stem[3] >= L'0' && stem[3] <= L'9') || stem[3] == L'\x00b9' || stem[3] == L'\x00b2' || stem[3] == L'\x00b3'))
        {
            return false;
        }

        return true;
    }

    bool IsSupportedFile(const std::wstring& name)
    {
        // Keep menu eligibility aligned with FileExplorerScripts.Core/LocalPaths.cs.
        if (name.size() < 3 || name.size() > 259 ||
            !((name[0] >= L'A' && name[0] <= L'Z') || (name[0] >= L'a' && name[0] <= L'z')) ||
            name[1] != L':' || name[2] != L'\\' || name.back() == L'\\' ||
            name.find(L'/') != std::wstring::npos || name.find(L':', 2) != std::wstring::npos)
        {
            return false;
        }

        for (size_t start = 3; start < name.size();)
        {
            const auto end = name.find(L'\\', start);
            if (!IsSafePathComponent(std::wstring_view(name).substr(start, end == std::wstring::npos ? name.size() - start : end - start)))
            {
                return false;
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
        }

        const auto drive = GetDriveTypeW(name.substr(0, 3).c_str());
        if (drive != DRIVE_FIXED && drive != DRIVE_REMOVABLE && drive != DRIVE_RAMDISK)
        {
            return false;
        }

        const auto attributes = GetFileAttributesW(name.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DEVICE)))
        {
            return false;
        }

        for (auto parent = std::filesystem::path(name).parent_path(); !parent.empty(); parent = parent.parent_path())
        {
            const auto parentAttributes = GetFileAttributesW(parent.c_str());
            if (parentAttributes == INVALID_FILE_ATTRIBUTES || (parentAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
            {
                return false;
            }
            if (parent == parent.root_path())
            {
                break;
            }
        }

        return true;
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

            const std::wstring name(path);
            CoTaskMemFree(path);
            if (!IsSupportedFile(name))
            {
                return {};
            }

            files.push_back(name);
        }
        return files;
    }

    std::wstring MediaMimeType(const std::wstring& file)
    {
        const auto extension = std::filesystem::path(file).extension().wstring();
        for (const auto& [suffix, mime] : { std::pair{ L".png", L"image/png" }, { L".jpg", L"image/jpeg" }, { L".jpeg", L"image/jpeg" }, { L".webp", L"image/webp" }, { L".wav", L"audio/wav" }, { L".mp3", L"audio/mpeg" }, { L".flac", L"audio/flac" } })
        {
            if (CompareStringOrdinal(extension.c_str(), -1, suffix, -1, TRUE) == CSTR_EQUAL)
            {
                return mime;
            }
        }
        return {};
    }

    std::wstring ProbeMedia(const std::wstring& path)
    {
        const auto mime = MediaMimeType(path);
        const auto attributes = GetFileAttributesW(path.c_str());
        if (mime.empty() || attributes == INVALID_FILE_ATTRIBUTES ||
            (attributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_OFFLINE | FILE_ATTRIBUTE_RECALL_ON_OPEN | FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS)))
        {
            return {};
        }

        const auto raw = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_OPEN_NO_RECALL, nullptr);
        if (raw == INVALID_HANDLE_VALUE)
        {
            return {};
        }
        winrt::handle file{ raw };
        BY_HANDLE_FILE_INFORMATION info{};
        LARGE_INTEGER length{};
        if (!GetFileInformationByHandle(file.get(), &info) || (info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) ||
            !GetFileSizeEx(file.get(), &length) || length.QuadPart <= 0 || length.QuadPart > 16 * 1024 * 1024)
        {
            return {};
        }

        unsigned char header[64]{};
        DWORD count = 0;
        if (!ReadFile(file.get(), header, static_cast<DWORD>(std::min<LONGLONG>(sizeof(header), length.QuadPart)), &count, nullptr))
        {
            return {};
        }
        const auto tag = [&](size_t offset, const char* value, size_t size) { return count >= offset + size && std::memcmp(header + offset, value, size) == 0; };
        const auto riffSize = count >= 8 ? static_cast<unsigned long>(header[4]) | (static_cast<unsigned long>(header[5]) << 8) |
                                               (static_cast<unsigned long>(header[6]) << 16) | (static_cast<unsigned long>(header[7]) << 24) :
                                           0;
        bool valid = false;
        if (mime == L"image/png")
        {
            valid = length.QuadPart >= 33 && tag(0, "\x89PNG\r\n\x1a\n", 8) && tag(12, "IHDR", 4);
        }
        else if (mime == L"image/jpeg")
        {
            valid = length.QuadPart >= 4 && count >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        }
        else if (mime == L"image/webp" || mime == L"audio/wav")
        {
            valid = length.QuadPart >= (mime == L"image/webp" ? 20 : 44) && tag(0, "RIFF", 4) &&
                    tag(8, mime == L"image/webp" ? "WEBP" : "WAVE", 4) && riffSize == length.QuadPart - 8;
        }
        else if (mime == L"audio/flac")
        {
            valid = length.QuadPart >= 42 && count >= 8 && tag(0, "fLaC", 4) &&
                    (header[4] & 0x7f) == 0 && header[5] == 0 && header[6] == 0 && header[7] == 34;
        }
        else if (mime == L"audio/mpeg")
        {
            if (tag(0, "ID3", 3))
            {
                if (count < 10 || header[3] < 2 || header[3] > 4 || (header[6] | header[7] | header[8] | header[9]) >= 128)
                {
                    return {};
                }
                LARGE_INTEGER offset{};
                offset.QuadPart = 10LL + (header[6] << 21 | header[7] << 14 | header[8] << 7 | header[9]);
                if (header[3] == 4 && (header[5] & 16))
                {
                    offset.QuadPart += 10;
                }
                if (offset.QuadPart > length.QuadPart - 4 || !SetFilePointerEx(file.get(), offset, nullptr, FILE_BEGIN) ||
                    !ReadFile(file.get(), header, 4, &count, nullptr))
                {
                    return {};
                }
            }
            valid = count >= 4 && header[0] == 0xff && (header[1] & 0xe0) == 0xe0 &&
                    (header[1] & 0x18) != 8 && (header[1] & 6) == 2 &&
                    (header[2] & 0xf0) != 0 && (header[2] & 0xf0) != 0xf0 && (header[2] & 0x0c) != 0x0c;
        }
        return valid ? mime : L"";
    }

    bool Accepts(const JsonObject& script, const std::vector<std::wstring>& files, const std::wstring& mediaMime = {})
    {
        if (files.empty() || files.size() < script.GetNamedNumber(L"min", 0) || files.size() > script.GetNamedNumber(L"max", 1024))
        {
            return false;
        }
        const auto kind = script.GetNamedString(L"kind", L"files");
        if (kind != L"files")
        {
            if (files.size() != 1 || mediaMime.empty() || !mediaMime.starts_with(std::wstring(kind) + L"/"))
            {
                return false;
            }
            const auto types = script.GetNamedArray(L"mimeTypes", JsonArray{});
            return std::any_of(types.begin(), types.end(), [&](const auto& value) { return value.GetString() == mediaMime; });
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
    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL okToBeSlow, EXPCMDSTATE* state) override
    {
        *state = ECS_HIDDEN;
        try
        {
            if (!root)
            {
                *state = enabled ? ECS_ENABLED : ECS_DISABLED;
                return S_OK;
            }
            const auto currentFiles = GetFiles(selection ? selection : storedSelection.Get());
            if (currentFiles != files)
            {
                mediaChecked = false;
                mediaMime.clear();
            }
            files = currentFiles;
            catalogue = ReadCatalogue(okToBeSlow != FALSE);
            if (!files.empty() && catalogue.GetNamedBoolean(L"enabled", false))
            {
                const auto scripts = catalogue.GetNamedArray(L"scripts", JsonArray{});
                const auto mediaState = CheckMedia(okToBeSlow);
                if (mediaState != S_OK)
                {
                    return mediaState;
                }
                const auto available = catalogue.GetNamedBoolean(L"truncated", false) || catalogue.HasKey(L"error") ||
                                       std::any_of(scripts.begin(), scripts.end(), [&](const auto& script) { return Accepts(script.GetObject(), files, mediaMime); });
                *state = available ? ECS_ENABLED : ECS_HIDDEN;
            }
            return S_OK;
        }
        catch (const winrt::hresult_error& error)
        {
            return !okToBeSlow && error.code() == HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION) ? E_PENDING : error.code().value;
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
        files.clear();
        mediaChecked = false;
        mediaMime.clear();
        return S_OK;
    }
    IFACEMETHODIMP GetSelection(REFIID iid, void** selection) override
    {
        return storedSelection ? storedSelection.CopyTo(iid, selection) : E_FAIL;
    }

private:
    HRESULT CheckMedia(BOOL okToBeSlow)
    {
        const auto candidate = files.size() == 1 ? MediaMimeType(files[0]) : L"";
        const auto scripts = catalogue.GetNamedArray(L"scripts", JsonArray{});
        if (!mediaChecked && !candidate.empty() &&
            std::any_of(scripts.begin(), scripts.end(), [&](const auto& value) {
                const auto script = value.GetObject();
                return script.GetNamedString(L"kind", L"files") != L"files" && Accepts(script, files, candidate);
            }))
        {
            // Keep the fast GetState path free of media reads.
            if (!okToBeSlow)
            {
                return E_PENDING;
            }
            mediaMime = ProbeMedia(files[0]);
            mediaChecked = true;
        }
        return S_OK;
    }

    bool root = true;
    std::wstring id;
    std::wstring name = L"PowerScripts";
    bool enabled = true;
    std::wstring reason;
    std::vector<std::wstring> files;
    std::wstring iconLocation;
    std::wstring mediaMime;
    bool mediaChecked = false;
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
        catalogue = ReadCatalogue(true);
        if (files.empty() && storedSelection)
        {
            files = GetFiles(storedSelection.Get());
        }
        if (files.empty() || !catalogue.GetNamedBoolean(L"enabled", false))
        {
            return list.CopyTo(result);
        }
        // The classic menu can enumerate without a preceding slow GetState callback.
        winrt::check_hresult(CheckMedia(TRUE));
        const auto showParameters = catalogue.GetNamedBoolean(L"showParameters", true);
        auto needsPicker = catalogue.GetNamedBoolean(L"truncated", false) || catalogue.HasKey(L"error");
        for (const auto& item : catalogue.GetNamedArray(L"scripts", JsonArray{}))
        {
            const auto script = item.GetObject();
            if (Accepts(script, files, mediaMime))
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
