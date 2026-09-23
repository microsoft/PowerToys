#include "Common.h"
#include "Authentication.h"

// Static-library consumers do not inherit this project's MSBuild linker settings.
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#include <algorithm>
#include <cwctype>
#include <exception>
#include <limits>
#include <memory>
#include <sstream>

namespace PowerToys::ProtectedStorage
{
    [[noreturn]] void Fail(const std::string& message, DWORD code)
    {
        throw Error(message + (code == ERROR_ACCESS_DENIED ? ": access denied" : "") +
                        " (win32=" + std::to_string(code) + ")",
                    code);
    }
    void Require(bool condition, const std::string& message)
    {
        if (!condition)
            throw Error(message);
    }
    std::string Utf8(std::wstring_view text)
    {
        if (text.empty())
            return {};
        int n = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
        if (!n)
            Fail("UTF-16 conversion");
        std::string result(n, '\0');
        if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), result.data(), n, nullptr, nullptr))
            Fail("UTF-16 conversion");
        return result;
    }
    std::wstring Wide(std::string_view text)
    {
        if (text.empty())
            return {};
        int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), nullptr, 0);
        if (!n)
            Fail("UTF-8 conversion");
        std::wstring result(n, L'\0');
        if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), result.data(), n))
            Fail("UTF-8 conversion");
        return result;
    }
    std::string Json(std::string_view text)
    {
        std::string result = "\"";
        constexpr char hex[] = "0123456789abcdef";
        for (unsigned char c : text)
        {
            if (c == '"' || c == '\\')
            {
                result += '\\';
                result += static_cast<char>(c);
            }
            else if (c < 32)
            {
                result += "\\u00";
                result += hex[c >> 4];
                result += hex[c & 15];
            }
            else
                result += static_cast<char>(c);
        }
        return result + "\"";
    }
    std::string Json(std::wstring_view text)
    {
        return Json(Utf8(text));
    }
    std::wstring Quote(std::wstring_view text)
    {
        Require(text.find(L'"') == text.npos && text.find(L'\0') == text.npos, "invalid quoted argument");
        std::wstring result = L"\"";
        result += text;
        size_t trailing = 0;
        for (size_t i = text.size(); i && text[i - 1] == L'\\'; --i)
            ++trailing;
        result.append(trailing, L'\\');
        return result + L"\"";
    }
    std::wstring SidText(PSID sid)
    {
        LPWSTR raw = nullptr;
        if (!ConvertSidToStringSidW(sid, &raw))
            Fail("ConvertSidToStringSid");
        std::wstring result(raw);
        LocalFree(raw);
        return result;
    }
    std::wstring TokenSid(HANDLE process)
    {
        Handle token;
        HANDLE raw = nullptr;
        if (!OpenProcessToken(process, TOKEN_QUERY, &raw))
            Fail("OpenProcessToken");
        token.reset(raw);
        DWORD size = 0;
        GetTokenInformation(token.get(), TokenUser, nullptr, 0, &size);
        if (!size)
            Fail("TokenUser size");
        std::vector<BYTE> bytes(size);
        if (!GetTokenInformation(token.get(), TokenUser, bytes.data(), size, &size))
            Fail("TokenUser");
        return SidText(reinterpret_cast<TOKEN_USER*>(bytes.data())->User.Sid);
    }
    bool ValidOwner(std::wstring_view text)
    {
        if (text.size() > 184 || text.find(L'\0') != text.npos)
            return false;
        PSID raw = nullptr;
        std::wstring value(text);
        if (!ConvertStringSidToSidW(value.c_str(), &raw))
            return false;
        std::unique_ptr<void, decltype(&LocalFree)> sid(raw, LocalFree);
        if (!IsValidSid(raw) || SidText(raw) != text)
            return false;
        auto auth = GetSidIdentifierAuthority(raw);
        const BYTE count = *GetSidSubAuthorityCount(raw);
        bool prefix = true;
        for (size_t i = 0; i != 5; ++i)
            prefix = prefix && auth->Value[i] == 0;
        if (!prefix)
            return false;
        if (auth->Value[5] == 5)
            return count == 5 && *GetSidSubAuthority(raw, 0) == 21 && *GetSidSubAuthority(raw, 4) != 0;
        return auth->Value[5] == 12 && count == 5 && *GetSidSubAuthority(raw, 0) == 1;
    }
    std::wstring AccountSid(const std::wstring& name)
    {
        DWORD size = 0, domainSize = 0;
        SID_NAME_USE use{};
        LookupAccountNameW(nullptr, name.c_str(), nullptr, &size, nullptr, &domainSize, &use);
        if (!size)
            Fail("LookupAccountName size");
        std::vector<BYTE> sid(size);
        std::vector<wchar_t> domain(static_cast<size_t>(domainSize) + 1);
        if (!LookupAccountNameW(nullptr, name.c_str(), sid.data(), &size, domain.data(), &domainSize, &use))
            Fail("LookupAccountName");
        return SidText(sid.data());
    }
    uint64_t ProcessBirth(HANDLE process)
    {
        FILETIME creation{}, exit{}, kernel{}, user{};
        if (!GetProcessTimes(process, &creation, &exit, &kernel, &user))
            Fail("GetProcessTimes");
        return (static_cast<uint64_t>(creation.dwHighDateTime) << 32) | creation.dwLowDateTime;
    }
    std::wstring ProcessImage(HANDLE process)
    {
        std::wstring result(32768, L'\0');
        DWORD length = static_cast<DWORD>(result.size());
        if (!QueryFullProcessImageNameW(process, 0, result.data(), &length))
            Fail("QueryFullProcessImageName");
        result.resize(length);
        return result;
    }
    std::wstring ModulePath()
    {
        return ProcessImage(GetCurrentProcess());
    }
    std::wstring NewId()
    {
        GUID id{};
        if (FAILED(CoCreateGuid(&id)))
            Fail("CoCreateGuid", ERROR_GEN_FAILURE);
        wchar_t text[40]{};
        if (!StringFromGUID2(id, text, 40))
            Fail("StringFromGUID2", ERROR_GEN_FAILURE);
        return std::wstring(text + 1, 36);
    }
    bool ValidId(std::wstring_view text)
    {
        if (text.size() != 36)
            return false;
        for (size_t i = 0; i != text.size(); ++i)
        {
            if (i == 8 || i == 13 || i == 18 || i == 23)
            {
                if (text[i] != L'-')
                    return false;
            }
            else if (!((text[i] >= L'0' && text[i] <= L'9') || (text[i] >= L'a' && text[i] <= L'f') || (text[i] >= L'A' && text[i] <= L'F')))
                return false;
        }
        return true;
    }
    bool IsAbsoluteLocal(std::wstring_view path)
    {
        if (path.size() < 3 || path.size() > 3000 ||
            !((path[0] >= L'A' && path[0] <= L'Z') || (path[0] >= L'a' && path[0] <= L'z')) ||
            path[1] != L':' || path[2] != L'\\')
            return false;
        if (path.find_first_of(L"/\"<>|?*", 3) != path.npos || path.find(L':', 2) != path.npos || path.find(L'\0') != path.npos)
            return false;
        size_t start = 3;
        while (start < path.size())
        {
            const size_t end = path.find(L'\\', start);
            auto component = path.substr(start, end == path.npos ? path.size() - start : end - start);
            if (component.empty() || component == L"." || component == L".." || component.back() == L'.' || component.back() == L' ')
                return false;
            for (wchar_t c : component)
                if (c < 32)
                    return false;
            if (end == path.npos)
                break;
            start = end + 1;
        }
        return path.size() == 3 || path.back() != L'\\';
    }
    std::wstring Parent(const std::wstring& path)
    {
        const auto p = path.find_last_of(L'\\');
        Require(p != path.npos, "path has no parent");
        return path.substr(0, p == 2 ? 3 : p);
    }
    std::vector<Handle> HoldDirectories(const std::wstring& path)
    {
        Require(IsAbsoluteLocal(path), "absolute local canonical path required");
        Require(GetDriveTypeW(path.substr(0, 3).c_str()) == DRIVE_FIXED, "local fixed disk required");
        std::vector<Handle> handles;
        size_t end = 3;
        while (true)
        {
            auto part = path.substr(0, end);
            Handle file(CreateFileW(part.c_str(), FILE_READ_ATTRIBUTES | READ_CONTROL, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            if (!file)
                Fail("open directory " + Utf8(part));
            BY_HANDLE_FILE_INFORMATION info{};
            if (!GetFileInformationByHandle(file.get(), &info))
                Fail("directory information");
            Require((info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) && !(info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT), "directory reparse/non-directory refused");
            handles.push_back(std::move(file));
            if (end == path.size())
                break;
            end = path.find(L'\\', end == 3 ? end : end + 1);
            if (end == path.npos)
                end = path.size();
        }
        return handles;
    }
    void CheckAcl(HANDLE handle, const std::wstring& va, bool privatePath, bool requireProtected)
    {
        PSECURITY_DESCRIPTOR raw = nullptr;
        PSID owner = nullptr;
        PACL acl = nullptr;
        DWORD status = GetSecurityInfo(handle, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, &owner, nullptr, &acl, nullptr, &raw);
        if (status != ERROR_SUCCESS)
            Fail("GetSecurityInfo", status);
        std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
        auto ownerText = SidText(owner);
        Require(ownerText == L"S-1-5-18" || (privatePath && ownerText == va), "unexpected protected-path owner");
        Require(acl != nullptr, "NULL DACL refused");
        SECURITY_DESCRIPTOR_CONTROL control{};
        DWORD revision = 0;
        if (!GetSecurityDescriptorControl(raw, &control, &revision))
            Fail("descriptor control");
        Require(!requireProtected || (control & SE_DACL_PROTECTED) != 0, "protected DACL required");
        constexpr DWORD mutation = FILE_WRITE_DATA | FILE_APPEND_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES |
                                   FILE_DELETE_CHILD | DELETE | WRITE_DAC | WRITE_OWNER | GENERIC_WRITE | GENERIC_ALL | MAXIMUM_ALLOWED;
        for (DWORD i = 0; i != acl->AceCount; ++i)
        {
            void* aceRaw = nullptr;
            if (!GetAce(acl, i, &aceRaw))
                Fail("GetAce");
            auto header = static_cast<ACE_HEADER*>(aceRaw);
            if (header->AceFlags & INHERIT_ONLY_ACE)
                continue;
            if (header->AceType == ACCESS_DENIED_ACE_TYPE)
                continue;
            Require(header->AceType == ACCESS_ALLOWED_ACE_TYPE, "unsupported protected DACL ACE");
            auto ace = static_cast<ACCESS_ALLOWED_ACE*>(aceRaw);
            if (!(ace->Mask & mutation))
                continue;
            auto sid = SidText(&ace->SidStart);
            Require(sid == L"S-1-5-18" || (privatePath && sid == va), "foreign write authority on protected path");
        }
    }
    static void CheckRegularFile(HANDLE file, uint64_t limit, bool allowUnlinked = false)
    {
        BY_HANDLE_FILE_INFORMATION info{};
        LARGE_INTEGER size{};
        if (!GetFileInformationByHandle(file, &info) || !GetFileSizeEx(file, &size))
            Fail("source metadata");
        Require(!(info.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)), "source must be regular non-reparse file");
        Require(info.nNumberOfLinks == 1 || (allowUnlinked && info.nNumberOfLinks == 0), "hardlinked source refused");
        Require(size.QuadPart >= 0 && static_cast<uint64_t>(size.QuadPart) <= limit, "source too large");
        Require(GetFileType(file) == FILE_TYPE_DISK, "disk file required");
    }
    Handle OpenRead(const std::wstring& path, uint64_t limit)
    {
        Handle file(CreateFileW(path.c_str(), GENERIC_READ | READ_CONTROL, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_SEQUENTIAL_SCAN, nullptr));
        if (!file)
            Fail("open source " + Utf8(path));
        CheckRegularFile(file.get(), limit);
        return file;
    }
    Handle OpenObservation(const std::wstring& path, TextObservation kind)
    {
        const bool replaced = kind == TextObservation::AtomicallyReplaced;
        Handle file(CreateFileW(path.c_str(), GENERIC_READ | READ_CONTROL, FILE_SHARE_READ | FILE_SHARE_WRITE | (replaced ? FILE_SHARE_DELETE : 0), nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_SEQUENTIAL_SCAN, nullptr));
        if (!file)
            Fail("open mutable observation " + Utf8(path));
        CheckRegularFile(file.get(), MaxText, replaced);
        return file;
    }
    std::vector<BYTE> ReadAll(HANDLE file, uint64_t limit)
    {
        LARGE_INTEGER size{}, zero{};
        if (!GetFileSizeEx(file, &size))
            Fail("file size");
        Require(size.QuadPart >= 0 && static_cast<uint64_t>(size.QuadPart) <= limit, "read size bound");
        if (!SetFilePointerEx(file, zero, nullptr, FILE_BEGIN))
            Fail("seek");
        std::vector<BYTE> bytes(static_cast<size_t>(size.QuadPart));
        size_t offset = 0;
        while (offset != bytes.size())
        {
            DWORD read = 0;
            DWORD chunk = static_cast<DWORD>((std::min)(bytes.size() - offset, size_t{ 65536 }));
            if (!ReadFile(file, bytes.data() + offset, chunk, &read, nullptr))
                Fail("read file");
            Require(read != 0, "short file read");
            offset += read;
        }
        return bytes;
    }
    std::string ReadText(const std::wstring& path)
    {
        auto file = OpenRead(path, MaxText);
        auto bytes = ReadAll(file.get(), MaxText);
        return std::string(bytes.begin(), bytes.end());
    }
    std::string ReadObservation(const std::wstring& path, TextObservation kind)
    {
        auto file = OpenObservation(path, kind);
        // ReadAll captures one bounded length, not a moving append-until-EOF target.
        auto bytes = ReadAll(file.get(), MaxText);
        return std::string(bytes.begin(), bytes.end());
    }
    static std::string Hex(const BYTE* bytes, size_t size)
    {
        constexpr char hex[] = "0123456789abcdef";
        std::string result(size * 2, '0');
        for (size_t i = 0; i != size; ++i)
        {
            result[i * 2] = hex[bytes[i] >> 4];
            result[i * 2 + 1] = hex[bytes[i] & 15];
        }
        return result;
    }
    std::string HashBytes(const BYTE* bytes, DWORD count)
    {
        std::array<BYTE, 32> digest{};
        DWORD size = static_cast<DWORD>(digest.size());
        if (!CryptHashCertificate2(BCRYPT_SHA256_ALGORITHM, 0, nullptr, bytes, count, digest.data(), &size))
            Fail("SHA256");
        Require(size == digest.size(), "SHA256 size");
        return Hex(digest.data(), digest.size());
    }
    std::string Hash(HANDLE file)
    {
        auto bytes = ReadAll(file);
        return HashBytes(bytes.data(), static_cast<DWORD>(bytes.size()));
    }
    std::string HashPath(const std::wstring& path)
    {
        auto file = OpenRead(path);
        return Hash(file.get());
    }
    static void WriteBytes(HANDLE file, const BYTE* data, size_t size)
    {
        size_t offset = 0;
        while (offset != size)
        {
            DWORD wrote = 0;
            const DWORD count = static_cast<DWORD>((std::min)(size - offset, size_t{ 65536 }));
            if (!WriteFile(file, data + offset, count, &wrote, nullptr))
                Fail("write file");
            Require(wrote != 0, "short file write");
            offset += wrote;
        }
        if (!FlushFileBuffers(file))
            Fail("flush file");
    }
    void CopyHeld(HANDLE source, const std::wstring& destination)
    {
        auto bytes = ReadAll(source);
        Handle file(CreateFileW(destination.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_WRITE_THROUGH, nullptr));
        if (!file)
            Fail("create copy " + Utf8(destination));
        WriteBytes(file.get(), bytes.data(), bytes.size());
    }
    void PublishPair(const std::wstring& sourceDirectory, const std::wstring& codeDirectory)
    {
        auto sourceDirectories = HoldDirectories(sourceDirectory);
        auto destinationDirectories = HoldDirectories(codeDirectory);
        // Per-file rename is atomic. The journal covers the non-atomic release transition.
        for (const auto& name : { L"Bootstrap.exe", L"Runtime.exe", L"manifest.txt", L"manifest.p7s", L"ClientCatalog.json", L"ClientCatalog.p7s" })
        {
            auto source = OpenRead(sourceDirectory + L"\\" + name);
            auto temporary = codeDirectory + L"\\" + name + L"." + NewId() + L".tmp";
            CopyHeld(source.get(), temporary);
            const auto destination = codeDirectory + L"\\" + name;
            ULONGLONG deadline = GetTickCount64() + 5000;
            while (!MoveFileExW(temporary.c_str(), destination.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                DWORD error = GetLastError();
                if ((error == ERROR_SHARING_VIOLATION || error == ERROR_ACCESS_DENIED) && GetTickCount64() < deadline)
                {
                    Sleep(100);
                    continue;
                }
                DeleteFileW(temporary.c_str());
                Fail("publish fixed executable " + Utf8(name), error);
            }
        }
    }
    void WriteNew(const std::wstring& path, std::string_view content)
    {
        Handle file(CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_WRITE_THROUGH, nullptr));
        if (!file)
            Fail("create text " + Utf8(path));
        WriteBytes(file.get(), reinterpret_cast<const BYTE*>(content.data()), content.size());
    }
    void ReplaceText(const std::wstring& path, std::string_view content)
    {
        auto pending = path + L"." + NewId() + L".tmp";
        WriteNew(pending, content);
        // ReplaceFile permits held delete-sharing snapshots and preserves the file ACL.
        const BOOL replaced = Exists(path) ?
                                  ReplaceFileW(path.c_str(), pending.c_str(), nullptr, 0, nullptr, nullptr) :
                                  MoveFileExW(pending.c_str(), path.c_str(), MOVEFILE_WRITE_THROUGH);
        if (!replaced)
        {
            DWORD error = GetLastError();
            DeleteFileW(pending.c_str());
            Fail("replace text " + Utf8(path), error);
        }
    }
    void AppendText(const std::wstring& path, std::string_view content)
    {
        Handle file(CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_WRITE_THROUGH, nullptr));
        if (!file)
            Fail("open journal");
        BY_HANDLE_FILE_INFORMATION info{};
        if (!GetFileInformationByHandle(file.get(), &info))
            Fail("journal metadata");
        Require(!(info.dwFileAttributes & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY)) && info.nNumberOfLinks == 1, "invalid journal object");
        WriteBytes(file.get(), reinterpret_cast<const BYTE*>(content.data()), content.size());
    }
    bool Exists(const std::wstring& path)
    {
        DWORD attr = GetFileAttributesW(path.c_str());
        if (attr != INVALID_FILE_ATTRIBUTES)
            return true;
        DWORD error = GetLastError();
        if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND)
            return false;
        Fail("path existence", error);
    }
    void MakeDirectory(const std::wstring& path)
    {
        if (!CreateDirectoryW(path.c_str(), nullptr))
        {
            DWORD error = GetLastError();
            if (error != ERROR_ALREADY_EXISTS)
                Fail("create directory " + Utf8(path), error);
        }
        auto held = HoldDirectories(path);
    }
    uint64_t ParseVersion(std::string_view text)
    {
        uint64_t result = 0;
        size_t start = 0;
        for (size_t part = 0; part != 4; ++part)
        {
            auto end = text.find('.', start);
            Require((part == 3) == (end == text.npos), "version must have four components");
            if (end == text.npos)
                end = text.size();
            Require(end > start && end - start <= 5, "invalid version component");
            Require(end - start == 1 || text[start] != '0', "noncanonical version");
            unsigned value = 0;
            for (size_t i = start; i != end; ++i)
            {
                Require(text[i] >= '0' && text[i] <= '9', "version is not decimal");
                value = value * 10 + static_cast<unsigned>(text[i] - '0');
            }
            Require(value <= 65535, "version overflow");
            result = (result << 16) | value;
            start = end + 1;
        }
        return result;
    }
    std::string VersionText(uint64_t version)
    {
        return std::to_string(version >> 48) + "." + std::to_string((version >> 32) & 65535) + "." +
               std::to_string((version >> 16) & 65535) + "." + std::to_string(version & 65535);
    }
    uint64_t FileVersion(const std::wstring& path)
    {
        struct VersionApi
        {
            HMODULE module = nullptr;
            decltype(&GetFileVersionInfoSizeW) size = nullptr;
            decltype(&GetFileVersionInfoW) read = nullptr;
            decltype(&VerQueryValueW) query = nullptr;
            VersionApi()
            {
                wchar_t system[MAX_PATH]{};
                const auto count = GetSystemDirectoryW(system, MAX_PATH);
                Require(count && count < MAX_PATH, "system directory for version API");
                const auto library = std::wstring(system, count) + L"\\version.dll";
                // PowerToys also produces version.lib; do not resolve that name via consumer LIBPATHs.
                module = LoadLibraryExW(library.c_str(), nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
                if (!module)
                    Fail("load system version API");
                size = reinterpret_cast<decltype(size)>(GetProcAddress(module, "GetFileVersionInfoSizeW"));
                read = reinterpret_cast<decltype(read)>(GetProcAddress(module, "GetFileVersionInfoW"));
                query = reinterpret_cast<decltype(query)>(GetProcAddress(module, "VerQueryValueW"));
                if (!size || !read || !query)
                {
                    FreeLibrary(module);
                    module = nullptr;
                    Fail("resolve system version API", ERROR_PROC_NOT_FOUND);
                }
            }
            ~VersionApi()
            {
                if (module)
                    FreeLibrary(module);
            }
            VersionApi(const VersionApi&) = delete;
            VersionApi& operator=(const VersionApi&) = delete;
        };
        static const VersionApi api;
        DWORD ignored = 0;
        DWORD size = api.size(path.c_str(), &ignored);
        if (!size || size > 1024 * 1024)
            Fail("PE version size");
        std::vector<BYTE> bytes(size);
        if (!api.read(path.c_str(), 0, size, bytes.data()))
            Fail("PE version");
        VS_FIXEDFILEINFO* info = nullptr;
        UINT length = 0;
        if (!api.query(bytes.data(), L"\\", reinterpret_cast<void**>(&info), &length))
            Fail("PE fixed version");
        Require(length >= sizeof(*info) && info->dwSignature == 0xFEEF04BD, "invalid PE fixed version");
        return (static_cast<uint64_t>(info->dwFileVersionMS) << 32) | info->dwFileVersionLS;
    }
    uint64_t OwnVersion()
    {
        return FileVersion(ModulePath());
    }
    std::map<std::string, std::string> ParseKeys(std::string_view text, const std::vector<std::string>& keys)
    {
        Require(!text.empty() && text.size() <= MaxText, "key document size");
        for (unsigned char c : text)
            Require((c >= 32 && c <= 126) || c == '\r' || c == '\n', "key document must be ASCII");
        std::map<std::string, std::string> result;
        size_t offset = 0;
        while (offset < text.size())
        {
            size_t end = text.find('\n', offset);
            if (end == text.npos)
                end = text.size();
            auto line = text.substr(offset, end - offset);
            if (!line.empty() && line.back() == '\r')
                line.remove_suffix(1);
            Require(!line.empty() && line.find('\r') == line.npos, "empty/malformed key line");
            size_t eq = line.find('=');
            Require(eq != line.npos && eq && eq + 1 < line.size(), "invalid key/value");
            std::string key(line.substr(0, eq)), value(line.substr(eq + 1));
            Require(std::find(keys.begin(), keys.end(), key) != keys.end(), "unknown key " + key);
            Require(result.emplace(key, value).second, "duplicate key " + key);
            offset = end + 1;
        }
        Require(result.size() == keys.size(), "missing required key");
        return result;
    }
    static std::wstring Known(REFKNOWNFOLDERID id)
    {
        PWSTR value = nullptr;
        HRESULT hr = SHGetKnownFolderPath(id, KF_FLAG_DONT_VERIFY, nullptr, &value);
        if (FAILED(hr))
            Fail("known folder", static_cast<DWORD>(hr));
        std::wstring result(value);
        CoTaskMemFree(value);
        Require(IsAbsoluteLocal(result), "known folder is not absolute local path");
        return result;
    }
    Paths::Paths(const std::wstring& ownerSid) : owner(ownerSid)
    {
        Require(ValidOwner(owner), "canonical real owner SID required");
        service = L"PowerToysProtectedStorage_" + owner;
        va = AccountSid(L"NT SERVICE\\" + service);
        root = Known(FOLDERID_ProgramFiles) + L"\\" + App;
        state = Known(FOLDERID_ProgramData) + L"\\" + App;
        code = root + L"\\Owners\\" + owner + L"\\Code";
        data = state + L"\\Data\\" + owner;
        policy = state + L"\\Policy\\" + owner + L"\\policy.txt";
        installer = state + L"\\Installer\\" + owner;
        pipe = L"\\\\.\\pipe\\PowerToysProtectedStorage.Control." + owner;
    }
    void Paths::Check() const
    {
        const std::vector<std::wstring> shared{ root, root + L"\\Owners", root + L"\\Owners\\" + owner, state, state + L"\\Data", state + L"\\Policy", Parent(policy), state + L"\\Installer", installer };
        for (const auto& path : shared)
        {
            auto handles = HoldDirectories(path);
            CheckAcl(handles.back().get(), va, false);
        }
        for (const auto& path : { code, data })
        {
            auto handles = HoldDirectories(path);
            CheckAcl(handles.back().get(), va, true);
        }
        auto policyFile = OpenRead(policy, MaxText);
        CheckAcl(policyFile.get(), va, false);
    }
    Handle Paths::Lock() const
    {
        Handle file(CreateFileW((installer + L"\\maintenance.lock").c_str(), GENERIC_READ | READ_CONTROL, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (!file)
            Fail("update/maintenance busy or missing maintenance.lock");
        BY_HANDLE_FILE_INFORMATION info{};
        if (!GetFileInformationByHandle(file.get(), &info))
            Fail("update lock information");
        Require(!(info.dwFileAttributes & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY)) && info.nNumberOfLinks == 1, "invalid update lock object");
        CheckAcl(file.get(), va, false);
        return file;
    }
    bool Paths::Draining() const
    {
        return Exists(installer + L"\\drain");
    }
    void ValidatePurgeTombstone(std::string_view text, const std::wstring& owner)
    {
        const auto keys = ParseKeys(text, { "format", "owner", "mode" });
        Require(ValidOwner(owner) && keys.at("format") == "1" &&
                    keys.at("owner") == Utf8(owner) && keys.at("mode") == "purge",
                "invalid purge tombstone identity");
    }
    bool Paths::AutoImportSuppressed() const
    {
        try
        {
            const auto ancestors = HoldDirectories(installer);
            CheckAcl(ancestors.back().get(), va, false);
            const auto marker = installer + L"\\purged";
            if (!Exists(marker))
                return false;
            auto file = OpenRead(marker, MaxText);
            CheckAcl(file.get(), va, false, false);
            const auto bytes = ReadAll(file.get(), MaxText);
            ValidatePurgeTombstone(std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()), owner);
            return true;
        }
        catch (const Error& error)
        {
            throw StorageError(ErrorCode::RecoveryRequired, error.code);
        }
    }
    Policy LoadPolicy(const Paths& paths)
    {
        auto file = OpenRead(paths.policy, MaxText);
        CheckAcl(file.get(), paths.va, false);
        auto bytes = ReadAll(file.get(), MaxText);
        auto keys = ParseKeys(std::string(bytes.begin(), bytes.end()), { "format", "app", "signer_sha256", "minimum_version" });
        Require(keys.at("format") == "1" && keys.at("app") == Utf8(App), "policy identity/format");
        Require(IsHash(keys.at("signer_sha256")), "invalid signer pin");
        Policy result{ keys.at("signer_sha256"), ParseVersion(keys.at("minimum_version")) };
        std::transform(result.signer.begin(), result.signer.end(), result.signer.begin(), [](unsigned char c) { return static_cast<char>(tolower(c)); });
        return result;
    }
    Bundle ValidateBundle(const std::wstring& directory, const Policy& policy, uint64_t current)
    {
        Bundle result;
        result.directories = HoldDirectories(directory);
        result.manifest = OpenRead(directory + L"\\manifest.txt", MaxText);
        result.signature = OpenRead(directory + L"\\manifest.p7s", 1024 * 1024);
        result.bootstrap = OpenRead(directory + L"\\Bootstrap.exe");
        result.runtime = OpenRead(directory + L"\\Runtime.exe");
        const auto verifyArchitecture = [](HANDLE image) {
            auto bytes = ReadAll(image);
            Require(bytes.size() >= sizeof(IMAGE_DOS_HEADER), "PE DOS header");
            IMAGE_DOS_HEADER dos{};
            memcpy(&dos, bytes.data(), sizeof(dos));
            Require(dos.e_magic == IMAGE_DOS_SIGNATURE && dos.e_lfanew > 0 &&
                        static_cast<size_t>(dos.e_lfanew) <= bytes.size() - sizeof(DWORD) - sizeof(IMAGE_FILE_HEADER),
                    "PE NT header range");
            DWORD signature = 0;
            IMAGE_FILE_HEADER header{};
            memcpy(&signature, bytes.data() + dos.e_lfanew, sizeof(signature));
            memcpy(&header, bytes.data() + dos.e_lfanew + sizeof(signature), sizeof(header));
#if defined(_M_ARM64)
            constexpr WORD machine = IMAGE_FILE_MACHINE_ARM64;
#else
            constexpr WORD machine = IMAGE_FILE_MACHINE_AMD64;
#endif
            Require(signature == IMAGE_NT_SIGNATURE && header.Machine == machine &&
                        !(header.Characteristics & IMAGE_FILE_DLL),
                    "payload PE architecture/type mismatch");
        };
        verifyArchitecture(result.bootstrap.get());
        verifyArchitecture(result.runtime.get());
        result.catalog = OpenRead(directory + L"\\ClientCatalog.json", MaxMetadata);
        result.catalogSignature = OpenRead(directory + L"\\ClientCatalog.p7s", 1024 * 1024);
        auto manifest = ReadAll(result.manifest.get(), MaxText);
        auto signature = ReadAll(result.signature.get(), 1024 * 1024);
        CRYPT_VERIFY_MESSAGE_PARA parameters{ sizeof(parameters), X509_ASN_ENCODING | PKCS_7_ASN_ENCODING };
        const BYTE* content = manifest.data();
        DWORD contentSize = static_cast<DWORD>(manifest.size());
        PCCERT_CONTEXT signer = nullptr;
        if (!CryptVerifyDetachedMessageSignature(&parameters, 0, signature.data(), static_cast<DWORD>(signature.size()), 1, &content, &contentSize, &signer))
            Fail("detached CMS signature verification");
        std::unique_ptr<const CERT_CONTEXT, decltype(&CertFreeCertificateContext)> signerGuard(signer, CertFreeCertificateContext);
        Require(signer && HashBytes(signer->pbCertEncoded, signer->cbCertEncoded) == policy.signer, "bundle signer pin mismatch");
        HCRYPTMSG message = CryptMsgOpenToDecode(parameters.dwMsgAndCertEncodingType, CMSG_DETACHED_FLAG, 0, 0, nullptr, nullptr);
        if (!message)
            Fail("CMS decode open");
        auto messageGuard = std::unique_ptr<void, decltype(&CryptMsgClose)>(message, CryptMsgClose);
        if (!CryptMsgUpdate(message, signature.data(), static_cast<DWORD>(signature.size()), TRUE))
            Fail("CMS decode");
        DWORD count = 0, size = sizeof(count);
        if (!CryptMsgGetParam(message, CMSG_SIGNER_COUNT_PARAM, 0, &count, &size))
            Fail("CMS signer count");
        Require(count == 1, "exactly one CMS signer required");
        size = 0;
        if (!CryptMsgGetParam(message, CMSG_SIGNER_INFO_PARAM, 0, nullptr, &size))
            Fail("CMS signer info size");
        Require(size && size <= 1024 * 1024, "CMS signer info bound");
        std::vector<BYTE> signerBytes(size);
        if (!CryptMsgGetParam(message, CMSG_SIGNER_INFO_PARAM, 0, signerBytes.data(), &size))
            Fail("CMS signer info");
        auto signerInfo = reinterpret_cast<CMSG_SIGNER_INFO*>(signerBytes.data());
        Require(signerInfo->HashAlgorithm.pszObjId && std::string_view(signerInfo->HashAlgorithm.pszObjId) == szOID_NIST_sha256,
                "CMS manifest digest must be SHA256");
        auto keys = ParseKeys(std::string(manifest.begin(), manifest.end()), { "format", "app", "version", "bootstrap_sha256", "runtime_sha256", "catalog_sha256" });
        Require(keys.at("format") == "1" && keys.at("app") == Utf8(App), "bundle identity/format");
        result.version = ParseVersion(keys.at("version"));
        Require(result.version >= current && result.version >= policy.minimum, "bundle downgrade/policy floor refused");
        result.bootstrapHash = keys.at("bootstrap_sha256");
        result.runtimeHash = keys.at("runtime_sha256");
        result.catalogHash = keys.at("catalog_sha256");
        Require(IsHash(result.bootstrapHash) && IsHash(result.runtimeHash), "invalid payload hash");
        auto lower = [](std::string& text) { std::transform(text.begin(), text.end(), text.begin(), [](unsigned char c) { return static_cast<char>(tolower(c)); }); };
        lower(result.bootstrapHash);
        lower(result.runtimeHash);
        lower(result.catalogHash);
        Require(IsHash(result.catalogHash) && Hash(result.catalog.get()) == result.catalogHash, "catalog SHA256 mismatch");
        VerifyClientCatalog(directory, policy, result.version);
        Require(Hash(result.bootstrap.get()) == result.bootstrapHash && Hash(result.runtime.get()) == result.runtimeHash, "payload SHA256 mismatch");
        // Held source handles disallow write/delete while the version API opens the same paths.
        Require(FileVersion(directory + L"\\Bootstrap.exe") == result.version &&
                    FileVersion(directory + L"\\Runtime.exe") == result.version,
                "both PE versions must match manifest");
        return result;
    }
    void ValidateRequest(const Request& request)
    {
        Require(request.magic == ProtocolMagic, "protocol magic");
        Require(request.command == Command::Status || request.command == Command::MsiPrepare ||
                    request.command == Command::MsiCommit || request.command == Command::MsiRollback ||
                    request.command == Command::MsiQuery,
                "unsupported command; updates require MSI coordination");
        auto operationEnd = std::find(std::begin(request.operation), std::end(request.operation), L'\0');
        Require(operationEnd != std::end(request.operation), "unterminated operation");
        Require(std::all_of(operationEnd, std::end(request.operation), [](wchar_t c) { return c == 0; }), "nonzero operation tail");
        auto operation = std::wstring_view(request.operation, operationEnd - std::begin(request.operation));
        Require(request.command == Command::Status ? operation.empty() : ValidId(operation), "invalid MSI operation GUID");
        auto end = std::find(std::begin(request.bundle), std::end(request.bundle), L'\0');
        Require(end != std::end(request.bundle), "unterminated bundle path");
        Require(std::all_of(end, std::end(request.bundle), [](wchar_t c) { return c == 0; }), "nonzero request tail");
        if (request.command == Command::Status)
            Require(end == std::begin(request.bundle), "status does not take a bundle");
        else if (request.command == Command::MsiPrepare || end != std::begin(request.bundle))
            Require(IsAbsoluteLocal(std::wstring_view(request.bundle, end - std::begin(request.bundle))), "invalid absolute bundle path");
    }
    DWORD BoundedIo(HANDLE pipe, bool write, void* buffer, DWORD count, HANDLE cancel, DWORD timeout)
    {
        Handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
            Fail("I/O event");
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        DWORD transferred = 0;
        BOOL completed = write ? WriteFile(pipe, buffer, count, &transferred, &operation) : ReadFile(pipe, buffer, count, &transferred, &operation);
        if (completed)
            return transferred;
        DWORD error = GetLastError();
        if (error != ERROR_IO_PENDING)
            Fail(write ? "pipe write" : "pipe read", error);
        HANDLE waits[]{ event.get(), cancel };
        DWORD result = WaitForMultipleObjects(cancel ? 2 : 1, waits, FALSE, timeout);
        if (result != WAIT_OBJECT_0)
        {
            CancelIoEx(pipe, &operation);
            GetOverlappedResult(pipe, &operation, &transferred, TRUE);
            Fail(result == WAIT_OBJECT_0 + 1 ? "I/O cancelled" : "I/O timeout", result == WAIT_FAILED ? GetLastError() : ERROR_TIMEOUT);
        }
        if (!GetOverlappedResult(pipe, &operation, &transferred, FALSE))
            Fail("pipe completion");
        return transferred;
    }
    std::string CompleteResponse(HANDLE activation, const std::function<void()>& respond, const std::function<void(std::string_view)>& logFailure)
    {
        std::exception_ptr responseFailure;
        try
        {
            respond();
        }
        catch (const std::exception&)
        {
            responseFailure = std::current_exception();
        }
        // A prepared transaction is independent of client ACK or diagnostic storage.
        if (activation && !SetEvent(activation))
            Fail("activate updater");
        if (!responseFailure)
            return {};
        try
        {
            std::rethrow_exception(responseFailure);
        }
        catch (const std::exception& failure)
        {
            std::string detail = failure.what();
            try
            {
                logFailure(detail);
            }
            catch (const std::exception& loggingFailure)
            {
                detail += "; response log failed: ";
                detail += loggingFailure.what();
            }
            return detail;
        }
    }
    std::wstring PipeSddl(const std::wstring& owner, const std::wstring& va)
    {
        Require(ValidOwner(owner), "pipe owner must be a canonical real-user SID");
        return L"D:P(A;;GA;;;SY)(A;;GA;;;" + va + L")(A;;GRGW;;;" + owner + L")";
    }
    std::wstring PipeCaller(HANDLE pipe)
    {
        if (!ImpersonateNamedPipeClient(pipe))
            Fail("pipe impersonation");
        HANDLE raw = nullptr;
        BOOL opened = OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &raw);
        DWORD error = GetLastError();
        if (!RevertToSelf())
            TerminateProcess(GetCurrentProcess(), ERROR_CANNOT_IMPERSONATE);
        if (!opened)
            Fail("pipe caller token", error);
        Handle token(raw);
        DWORD size = 0;
        GetTokenInformation(token.get(), TokenUser, nullptr, 0, &size);
        if (!size)
            Fail("caller TokenUser size");
        std::vector<BYTE> bytes(size);
        if (!GetTokenInformation(token.get(), TokenUser, bytes.data(), size, &size))
            Fail("caller TokenUser");
        return SidText(reinterpret_cast<TOKEN_USER*>(bytes.data())->User.Sid);
    }
    std::string Call(const Paths& paths, const Request& request, DWORD timeout)
    {
        ValidateRequest(request);
        auto pipe = ConnectOwnerPipe(paths, paths.pipe, timeout);
        VerifyServer(pipe.get(), paths, true);
        DWORD mode = PIPE_READMODE_MESSAGE;
        if (!SetNamedPipeHandleState(pipe.get(), &mode, nullptr, nullptr))
            Fail("pipe message mode");
        Request sent = request;
        Require(BoundedIo(pipe.get(), true, &sent, sizeof(sent), nullptr, timeout) == sizeof(sent), "short request write");
        std::array<char, MaxText> response{};
        DWORD size = BoundedIo(pipe.get(), false, response.data(), static_cast<DWORD>(response.size()), nullptr, timeout);
        Require(size != 0, "empty service response");
        DWORD acknowledgement = ProtocolMagic;
        Require(BoundedIo(pipe.get(), true, &acknowledgement, sizeof(acknowledgement), nullptr, timeout) == sizeof(acknowledgement), "short response acknowledgement");
        return std::string(response.data(), size);
    }
    std::map<std::string, std::string> ParseMsiResponse(std::string_view response, const std::wstring& operation)
    {
        try
        {
            auto result = ParseKeys(response, { "format", "operation", "phase", "version" });
            Require(result.at("format") == "1" && ValidId(operation) &&
                        result.at("operation") == Utf8(operation),
                    "MSI response operation/format mismatch");
            const auto& phase = result.at("phase");
            Require(phase == "preparing" || phase == "prepared" || phase == "committed" ||
                        phase == "rolled_back" || phase == "unchanged" || phase == "recovery_required" ||
                        phase == "absent",
                    "unknown MSI response phase");
            ParseVersion(result.at("version"));
            return result;
        }
        catch (const std::exception& failure)
        {
            throw Error("MSI service response: " + std::string(response) + "; " + failure.what());
        }
    }
    std::map<std::string, std::string> MsiCall(const Paths& paths, Command command, const std::wstring& operation, const std::wstring& bundle)
    {
        Require(command == Command::MsiPrepare || command == Command::MsiCommit ||
                    command == Command::MsiRollback || command == Command::MsiQuery,
                "MSI command required");
        Require(TokenSid() == paths.owner, "MSI caller must be actual ordinary owner");
        RequireOrdinaryMaintenanceProcess(GetCurrentProcess());
        Require(operation.size() < std::size(Request{}.operation) && bundle.size() < std::size(Request{}.bundle),
                "MSI request too long");
        Request request{};
        request.command = command;
        std::copy(operation.begin(), operation.end(), request.operation);
        std::copy(bundle.begin(), bundle.end(), request.bundle);
        ValidateRequest(request);
        // Prepare validates/copies four bounded files; final commit rechecks readiness.
        return ParseMsiResponse(Call(paths, request, 60000), operation);
    }
    void CheckOwnVa(const Paths& paths)
    {
        Require(TokenSid() == paths.va, "runtime must execute as its exact service VA, never SYSTEM/owner");
    }
    PACL BuildIdentityQueryAcl(PACL previous, const std::wstring& vaSid, const std::wstring& ownerSid, DWORD access)
    {
        Require(previous != nullptr, "kernel object NULL DACL refused");
        PSID rawVa = nullptr, rawOwner = nullptr, rawSystem = nullptr;
        if (!ConvertStringSidToSidW(vaSid.c_str(), &rawVa))
            Fail("observation VA SID");
        std::unique_ptr<void, decltype(&LocalFree)> va(rawVa, LocalFree);
        if (!ConvertStringSidToSidW(ownerSid.c_str(), &rawOwner))
            Fail("observation owner SID");
        std::unique_ptr<void, decltype(&LocalFree)> owner(rawOwner, LocalFree);
        if (!ConvertStringSidToSidW(L"S-1-5-18", &rawSystem))
            Fail("observation SYSTEM SID");
        std::unique_ptr<void, decltype(&LocalFree)> system(rawSystem, LocalFree);
        EXPLICIT_ACCESSW entries[3]{};
        for (auto& entry : entries)
        {
            entry.grfAccessPermissions = access;
            entry.grfAccessMode = GRANT_ACCESS;
            entry.grfInheritance = NO_INHERITANCE;
        }
        BuildTrusteeWithSidW(&entries[0].Trustee, rawVa);
        BuildTrusteeWithSidW(&entries[1].Trustee, rawOwner);
        BuildTrusteeWithSidW(&entries[2].Trustee, rawSystem);
        PACL updated = nullptr;
        DWORD error = SetEntriesInAclW(3, entries, previous, &updated);
        if (error != ERROR_SUCCESS)
            Fail("construct observation DACL", error);
        return updated;
    }
    static void GrantIdentityQuery(const std::wstring& observer, const std::wstring& owner)
    {
        auto grant = [&](HANDLE object, DWORD access) {
            PACL previous = nullptr;
            PSECURITY_DESCRIPTOR raw = nullptr;
            DWORD error = GetSecurityInfo(object, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION, nullptr, nullptr, &previous, nullptr, &raw);
            if (error != ERROR_SUCCESS)
                Fail("query kernel object DACL", error);
            std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
            PACL updated = BuildIdentityQueryAcl(previous, observer, owner, access);
            std::unique_ptr<void, decltype(&LocalFree)> acl(updated, LocalFree);
            error = SetSecurityInfo(object, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION, nullptr, nullptr, updated, nullptr);
            if (error != ERROR_SUCCESS)
                Fail("grant read-only identity observation", error);
        };
        // Explicit VA access spans SCM logon sessions; explicit SYSTEM access lets
        // privilege-stripped MSI actions observe exit without SeDebugPrivilege.
        // These grants add query only, never duplication or process mutation.
        grant(GetCurrentProcess(), IdentityProcessAccess);
        HANDLE raw = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | READ_CONTROL | WRITE_DAC, &raw))
            Fail("open own token DACL");
        Handle token(raw);
        grant(token.get(), TOKEN_QUERY);
    }
    void AllowIdentityQuery(const Paths& paths)
    {
        GrantIdentityQuery(paths.va, paths.owner);
    }
    void AllowPeerIdentityQuery(const std::wstring& peerSid)
    {
        Require(ValidOwner(peerSid), "peer observer must be a canonical real-user SID");
        GrantIdentityQuery(peerSid, TokenSid());
    }
    Child Spawn(const std::wstring& executable, const std::wstring& arguments, const std::vector<HANDLE>& inherited, DWORD flags)
    {
        SIZE_T size = 0;
        InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
        std::vector<BYTE> bytes(size);
        auto attributes = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(bytes.data());
        if (!InitializeProcThreadAttributeList(attributes, 1, 0, &size))
            Fail("process attribute list");
        auto cleanup = std::unique_ptr<_PROC_THREAD_ATTRIBUTE_LIST, decltype(&DeleteProcThreadAttributeList)>(attributes, DeleteProcThreadAttributeList);
        Require(!inherited.empty(), "explicit inherited handles required");
        auto handleList = inherited;
        if (!UpdateProcThreadAttribute(attributes, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handleList.data(), handleList.size() * sizeof(HANDLE), nullptr, nullptr))
            Fail("process handle list");
        STARTUPINFOEXW startup{};
        startup.StartupInfo.cb = sizeof(startup);
        startup.lpAttributeList = attributes;
        PROCESS_INFORMATION info{};
        auto command = Quote(executable) + L" " + arguments;
        if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT | flags, nullptr, Parent(executable).c_str(), &startup.StartupInfo, &info))
            Fail("CreateProcess " + Utf8(executable));
        return Child{ Handle(info.hProcess), Handle(info.hThread), info.dwProcessId };
    }
    Handle InheritableDuplicate(HANDLE source, DWORD access)
    {
        HANDLE copy = nullptr;
        if (!DuplicateHandle(GetCurrentProcess(), source, GetCurrentProcess(), &copy, access, TRUE, access ? 0 : DUPLICATE_SAME_ACCESS))
            Fail("duplicate inherited handle");
        return Handle(copy);
    }
    std::wstring HandleText(HANDLE handle)
    {
        return std::to_wstring(reinterpret_cast<uintptr_t>(handle));
    }
    HANDLE ParseHandle(std::wstring_view text)
    {
        Require(!text.empty() && text.size() <= 20, "invalid handle argument");
        uintptr_t value = 0;
        for (wchar_t c : text)
        {
            Require(c >= L'0' && c <= L'9' && value <= (std::numeric_limits<uintptr_t>::max() - (c - L'0')) / 10, "invalid/overflowed handle");
            value = value * 10 + (c - L'0');
        }
        Require(value != 0 && value != std::numeric_limits<uintptr_t>::max(), "invalid inherited handle value");
        HANDLE handle = reinterpret_cast<HANDLE>(value);
        DWORD flags = 0;
        if (!GetHandleInformation(handle, &flags))
            Fail("inherited handle not valid");
        return handle;
    }
    std::string ErrorJson(const std::exception& error)
    {
        return "{\"ok\":false,\"error\":" + Json(error.what()) + "}";
    }
    std::string Phase(std::string_view journal)
    {
        if (journal.empty() || journal.back() != '\n')
            return "interrupted";
        auto before = journal.rfind('\n', journal.size() - 2);
        auto line = journal.substr(before == journal.npos ? 0 : before + 1);
        auto delimiter = line.find('\t');
        if (delimiter == line.npos)
            return "interrupted";
        return std::string(line.substr(0, delimiter));
    }
    bool IsTerminal(std::string_view journal)
    {
        auto phase = Phase(journal);
        return phase == "committed" || phase == "rolled_back" || phase == "unchanged";
    }
    std::wstring CurrentTransaction(const Paths& paths)
    {
        if (!Exists(paths.data + L"\\current.txt"))
            return {};
        auto id = Wide(ReadObservation(paths.data + L"\\current.txt", TextObservation::AtomicallyReplaced));
        Require(ValidId(id), "invalid current transaction; recovery required");
        return paths.data + L"\\transactions\\" + id;
    }
    std::string Journal(const std::wstring& transaction)
    {
        return transaction.empty() ? "" : ReadObservation(transaction + L"\\journal.txt", TextObservation::AppendOnly);
    }
    void Record(const std::wstring& transaction, std::string_view phase, std::string_view detail)
    {
        Require(phase.find_first_not_of("abcdefghijklmnopqrstuvwxyz_") == phase.npos && !phase.empty(), "invalid journal phase");
        AppendText(transaction + L"\\journal.txt", std::string(phase) + "\t" + Json(detail) + "\n");
    }
    void WriteTransaction(const std::wstring& directory, const Transaction& transaction)
    {
        Require(ValidOwner(transaction.owner) && ValidId(transaction.operation), "invalid transaction identity");
        Require(transaction.newVersion >= transaction.oldVersion, "transaction downgrade refused");
        for (const auto& hash : { transaction.oldBootstrapHash, transaction.oldRuntimeHash, transaction.newBootstrapHash, transaction.newRuntimeHash, transaction.oldCatalogHash, transaction.newCatalogHash })
            Require(IsHash(hash), "invalid transaction hash");
        WriteNew(directory + L"\\transaction.txt", "format=1\nowner=" + Utf8(transaction.owner) + "\noperation=" + Utf8(transaction.operation) + "\nold_version=" + VersionText(transaction.oldVersion) + "\nnew_version=" + VersionText(transaction.newVersion) + "\nold_bootstrap_sha256=" + transaction.oldBootstrapHash + "\nold_runtime_sha256=" + transaction.oldRuntimeHash + "\nnew_bootstrap_sha256=" + transaction.newBootstrapHash + "\nnew_runtime_sha256=" + transaction.newRuntimeHash + "\nold_catalog_sha256=" + transaction.oldCatalogHash + "\nnew_catalog_sha256=" + transaction.newCatalogHash + "\n");
        WriteNew(directory + L"\\decision.lock", "");
    }
    Transaction ReadTransaction(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation)
    {
        Require(ValidOwner(owner) && ValidId(operation), "invalid transaction owner/operation");
        auto values = ParseKeys(ReadText(directory + L"\\transaction.txt"),
                                { "format", "owner", "operation", "old_version", "new_version", "old_bootstrap_sha256", "old_runtime_sha256", "new_bootstrap_sha256", "new_runtime_sha256", "old_catalog_sha256", "new_catalog_sha256" });
        Require(values.at("format") == "1" && values.at("owner") == Utf8(owner) &&
                    values.at("operation") == Utf8(operation),
                "transaction owner/operation mismatch");
        Transaction result{ owner, operation, ParseVersion(values.at("old_version")), ParseVersion(values.at("new_version")), values.at("old_bootstrap_sha256"), values.at("old_runtime_sha256"), values.at("new_bootstrap_sha256"), values.at("new_runtime_sha256"), values.at("old_catalog_sha256"), values.at("new_catalog_sha256") };
        Require(result.newVersion >= result.oldVersion, "invalid transaction versions");
        for (const auto& hash : { result.oldBootstrapHash, result.oldRuntimeHash, result.newBootstrapHash, result.newRuntimeHash, result.oldCatalogHash, result.newCatalogHash })
            Require(IsHash(hash), "invalid transaction hash");
        return result;
    }
    Handle TransactionGate(const std::wstring& directory)
    {
        const auto deadline = GetTickCount64() + IoTimeout;
        while (true)
        {
            Handle gate(CreateFileW((directory + L"\\decision.lock").c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            if (gate)
            {
                CheckRegularFile(gate.get(), 0);
                return gate;
            }
            DWORD error = GetLastError();
            if (error != ERROR_SHARING_VIOLATION || GetTickCount64() >= deadline)
                Fail("transaction decision gate unavailable", error);
            Sleep(20);
        }
    }
    void RecordOperation(const std::wstring& directory, std::string_view phase, std::string_view detail)
    {
        auto gate = TransactionGate(directory);
        Record(directory, phase, detail);
    }
    static std::string PublicPhase(std::string_view journal)
    {
        auto phase = Phase(journal);
        if (phase == "prepared" || phase == "committed" || phase == "rolled_back" ||
            phase == "unchanged" || phase == "recovery_required")
            return phase;
        if (phase == "staged" || phase == "updater_spawned" || phase == "stopping" || phase == "publishing" ||
            phase == "starting" || phase == "rollback_started" || phase == "rollback_starting")
            return "preparing";
        return "recovery_required";
    }
    std::wstring ResolveMsiOperation(const std::wstring& current, const std::wstring& owner, const std::wstring& operation, Command command)
    {
        Require(ValidOwner(owner) && ValidId(operation), "invalid MSI operation identity");
        Require(command == Command::MsiQuery || command == Command::MsiRollback || command == Command::MsiCommit,
                "MSI query/decision command required");
        const auto currentId = current.empty() ? L"" : current.substr(current.find_last_of(L'\\') + 1);
        Require(current.empty() || ValidId(currentId), "invalid current transaction; recovery required");
        if (currentId != operation)
        {
            Require(command != Command::MsiCommit, "MSI commit operation is not the exact current invocation");
            return {};
        }
        ReadTransaction(current, owner, operation);
        return current;
    }
    static std::string OperationResponse(const std::wstring& operation, std::string_view phase, uint64_t version)
    {
        Require(ValidId(operation), "invalid response operation GUID");
        return "format=1\noperation=" + Utf8(operation) + "\nphase=" + std::string(phase) +
               "\nversion=" + VersionText(version) + "\n";
    }
    std::string AbsentMsiResponse(const std::wstring& operation, uint64_t currentVersion)
    {
        return OperationResponse(operation, "absent", currentVersion);
    }
    std::string MsiResponse(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation)
    {
        const auto metadata = ReadTransaction(directory, owner, operation);
        auto gate = TransactionGate(directory);
        const auto phase = PublicPhase(Journal(directory));
        const auto version = phase == "rolled_back" ? metadata.oldVersion : metadata.newVersion;
        return OperationResponse(operation, phase, version);
    }
    static Command ReadDecision(const std::wstring& directory, const std::wstring& operation)
    {
        if (!Exists(directory + L"\\decision.txt"))
            return Command::MsiQuery;
        auto values = ParseKeys(ReadText(directory + L"\\decision.txt"), { "format", "operation", "decision" });
        Require(values.at("format") == "1" && values.at("operation") == Utf8(operation), "decision operation mismatch");
        Require(values.at("decision") == "commit" || values.at("decision") == "rollback", "invalid MSI decision");
        return values.at("decision") == "commit" ? Command::MsiCommit : Command::MsiRollback;
    }
    static void WriteDecision(const std::wstring& directory, const std::wstring& operation, Command command)
    {
        WriteNew(directory + L"\\decision.txt", "format=1\noperation=" + Utf8(operation) + "\ndecision=" + (command == Command::MsiCommit ? "commit\n" : "rollback\n"));
    }
    void DecideOperation(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation, Command command)
    {
        Require(command == Command::MsiCommit || command == Command::MsiRollback, "MSI decision command required");
        ReadTransaction(directory, owner, operation);
        auto gate = TransactionGate(directory);
        auto phase = PublicPhase(Journal(directory));
        if (phase == "unchanged")
            return;
        if (phase == "committed")
        {
            Require(command == Command::MsiCommit, "committed operation cannot be rolled back");
            return;
        }
        if (phase == "rolled_back")
        {
            Require(command == Command::MsiRollback, "rolled-back operation cannot be committed");
            return;
        }
        Require(phase != "recovery_required", "operation requires explicit recovery");
        Require(command == Command::MsiRollback || phase == "prepared", "MSI commit requires prepared readiness");
        const auto previous = ReadDecision(directory, operation);
        Require(previous == Command::MsiQuery || previous == command, "conflicting MSI decision refused");
        if (previous == Command::MsiQuery)
            WriteDecision(directory, operation, command);
    }
    Command WaitDecision(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation, DWORD timeout)
    {
        ReadTransaction(directory, owner, operation);
        const auto deadline = GetTickCount64() + timeout;
        while (true)
        {
            {
                auto gate = TransactionGate(directory);
                const auto decision = ReadDecision(directory, operation);
                if (decision != Command::MsiQuery)
                    return decision;
                if (GetTickCount64() >= deadline)
                {
                    // Serialize timeout with host commit: the first durable decision wins.
                    WriteDecision(directory, operation, Command::MsiRollback);
                    return Command::MsiRollback;
                }
            }
            Sleep(100);
        }
    }
    void RequireNewOperation(const std::wstring& current, const std::wstring& owner, const std::wstring& operation)
    {
        Require(ValidOwner(owner) && ValidId(operation), "invalid new operation identity");
        if (current.empty())
            return;
        const auto id = current.substr(current.find_last_of(L'\\') + 1);
        ReadTransaction(current, owner, id);
        auto gate = TransactionGate(current);
        Require(IsTerminal(Journal(current)), "pending/unresolved operation refuses a different MSI invocation");
        Require(id != operation, "current operation requires idempotent retry, not a new generation");
    }
    void VerifySameVersion(const Bundle& bundle, uint64_t current, const std::string& bootstrapHash, const std::string& runtimeHash)
    {
        Require(bundle.version >= current, "bundle downgrade refused");
        if (bundle.version == current)
            Require(bundle.bootstrapHash == bootstrapHash && bundle.runtimeHash == runtimeHash,
                    "same-version bundle differs from installed signed pair");
    }
    int RunUpdate(const std::wstring& transaction, const UpdateActions& actions)
    {
        bool commitDecided = false;
        try
        {
            RecordOperation(transaction, "stopping", "same-VA updater stopping fixed SCM service");
            actions.stop();
            RecordOperation(transaction, "publishing", "old exact processes exited; replacing both fixed PEs");
            actions.publishNew();
            RecordOperation(transaction, "starting", "starting candidate via unchanged SCM ImagePath/account");
            actions.startNew();
            actions.verifyNew();
            RecordOperation(transaction, "prepared", "both new PEs/readiness verified; awaiting MSI decision under maintenance lease");
            Require(actions.decision() == Command::MsiCommit, "MSI rollback requested or decision deadline expired");
            commitDecided = true;
            // Readiness may have changed during the MSI decision interval.
            actions.verifyNew();
            RecordOperation(transaction, "committed", "MSI commit decision durable; both exact VA actors/readiness reverified");
            return 0;
        }
        catch (const std::exception& failure)
        {
            const std::string cause = failure.what();
            if (commitDecided)
            {
                RecordOperation(transaction, "recovery_required", "durable MSI commit decision; automatic rollback forbidden: " + cause);
                return 3;
            }
            try
            {
                RecordOperation(transaction, "rollback_started", cause);
                actions.stop();
                actions.publishOld();
                RecordOperation(transaction, "rollback_starting", cause);
                actions.startOld();
                actions.verifyOld();
                RecordOperation(transaction, "rolled_back", cause);
                return 2;
            }
            catch (const std::exception& rollbackFailure)
            {
                RecordOperation(transaction, "recovery_required", cause + "; rollback failure: " + rollbackFailure.what());
                return 3;
            }
        }
    }
}
