#include "LsmrCommon.h"

#include <appmodel.h>
#include <aclapi.h>
#include <bcrypt.h>
#include <sddl.h>
#include <shellapi.h>
#include <shlobj_core.h>

#include <array>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <sstream>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "shell32.lib")

namespace
{
    [[nodiscard]] std::wstring sid_to_string(PSID sid)
    {
        LPWSTR text = nullptr;
        if (!ConvertSidToStringSidW(sid, &text))
        {
            throw ptlsmr::win32_error("ConvertSidToStringSidW", GetLastError());
        }
        ptlsmr::local_memory memory(text);
        return text;
    }

    [[nodiscard]] std::wstring hex_digest(std::wstring_view input)
    {
        BCRYPT_ALG_HANDLE algorithm = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(
            &algorithm,
            BCRYPT_SHA256_ALGORITHM,
            nullptr,
            0);
        if (status < 0)
        {
            throw std::runtime_error("BCryptOpenAlgorithmProvider(SHA256) failed");
        }
        struct algorithm_guard
        {
            BCRYPT_ALG_HANDLE value;
            ~algorithm_guard()
            {
                BCryptCloseAlgorithmProvider(value, 0);
            }
        } guard{ algorithm };

        DWORD objectBytes = 0;
        DWORD resultBytes = 0;
        status = BCryptGetProperty(
            algorithm,
            BCRYPT_OBJECT_LENGTH,
            reinterpret_cast<PUCHAR>(&objectBytes),
            sizeof(objectBytes),
            &resultBytes,
            0);
        if (status < 0)
        {
            throw std::runtime_error("BCryptGetProperty(BCRYPT_OBJECT_LENGTH) failed");
        }
        std::vector<UCHAR> object(objectBytes);
        std::array<UCHAR, 32> digest{};
        std::vector<wchar_t> inputCopy(input.begin(), input.end());
        BCRYPT_HASH_HANDLE hash = nullptr;
        status = BCryptCreateHash(
            algorithm,
            &hash,
            object.data(),
            static_cast<ULONG>(object.size()),
            nullptr,
            0,
            0);
        if (status < 0)
        {
            throw std::runtime_error("BCryptCreateHash failed");
        }
        struct hash_guard
        {
            BCRYPT_HASH_HANDLE value;
            ~hash_guard()
            {
                BCryptDestroyHash(value);
            }
        } hashGuard{ hash };
        status = BCryptHashData(
            hash,
            reinterpret_cast<PUCHAR>(inputCopy.data()),
            static_cast<ULONG>(inputCopy.size() * sizeof(wchar_t)),
            0);
        if (status < 0)
        {
            throw std::runtime_error("BCryptHashData failed");
        }
        status = BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0);
        if (status < 0)
        {
            throw std::runtime_error("BCryptFinishHash failed");
        }
        std::wstringstream value;
        for (const auto byte : digest)
        {
            value << std::hex << std::setw(2) << std::setfill(L'0') <<
                static_cast<unsigned int>(byte);
        }
        return value.str();
    }

    [[nodiscard]] std::wstring package_string_from_id(bool family, uint16_t major)
    {
        std::wstring name(ptlsmr::PackageName);
        std::wstring publisher(ptlsmr::PackagePublisher);
        PACKAGE_ID id{};
        id.processorArchitecture = PROCESSOR_ARCHITECTURE_AMD64;
        id.version.Major = major;
        id.name = name.data();
        id.publisher = publisher.data();
        UINT32 characters = 0;
        LONG result = family
            ? PackageFamilyNameFromId(&id, &characters, nullptr)
            : PackageFullNameFromId(&id, &characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error(
                family ? "PackageFamilyNameFromId(size)" : "PackageFullNameFromId(size)",
                static_cast<DWORD>(result));
        }
        std::wstring output(characters, L'\0');
        result = family
            ? PackageFamilyNameFromId(&id, &characters, output.data())
            : PackageFullNameFromId(&id, &characters, output.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error(
                family ? "PackageFamilyNameFromId" : "PackageFullNameFromId",
                static_cast<DWORD>(result));
        }
        output.resize(characters - 1);
        return output;
    }

    void protect_directory(const std::filesystem::path& directory, const std::wstring& sddl)
    {
        if (!std::filesystem::exists(directory))
        {
            std::filesystem::create_directories(directory);
        }
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl.c_str(),
                SDDL_REVISION_1,
                &descriptor,
                nullptr))
        {
            throw ptlsmr::win32_error(
                "ConvertStringSecurityDescriptorToSecurityDescriptorW",
                GetLastError());
        }
        ptlsmr::local_memory memory(descriptor);
        BOOL ownerDefaulted = FALSE;
        PSID owner = nullptr;
        if (!GetSecurityDescriptorOwner(descriptor, &owner, &ownerDefaulted))
        {
            throw ptlsmr::win32_error("GetSecurityDescriptorOwner(directory)", GetLastError());
        }
        BOOL daclPresent = FALSE;
        BOOL daclDefaulted = FALSE;
        PACL dacl = nullptr;
        if (!GetSecurityDescriptorDacl(descriptor, &daclPresent, &dacl, &daclDefaulted))
        {
            throw ptlsmr::win32_error("GetSecurityDescriptorDacl(directory)", GetLastError());
        }
        std::wstring mutablePath = directory.wstring();
        SECURITY_INFORMATION information =
            DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION;
        if (sddl.starts_with(L"O:"))
        {
            information |= OWNER_SECURITY_INFORMATION;
        }
        const DWORD result = SetNamedSecurityInfoW(
            mutablePath.data(),
            SE_FILE_OBJECT,
            information,
            owner,
            nullptr,
            daclPresent ? dacl : nullptr,
            nullptr);
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("SetNamedSecurityInfoW(directory)", result);
        }
    }
}

namespace ptlsmr
{
    win32_error::win32_error(const char* operation, DWORD error) :
        std::runtime_error(operation),
        m_code(error)
    {
    }

    DWORD win32_error::code() const noexcept
    {
        return m_code;
    }

    unique_handle::unique_handle(HANDLE value) noexcept :
        m_value(value)
    {
    }

    unique_handle::~unique_handle()
    {
        reset();
    }

    unique_handle::unique_handle(unique_handle&& other) noexcept :
        m_value(other.release())
    {
    }

    unique_handle& unique_handle::operator=(unique_handle&& other) noexcept
    {
        if (this != &other)
        {
            reset(other.release());
        }
        return *this;
    }

    HANDLE unique_handle::get() const noexcept
    {
        return m_value;
    }

    HANDLE unique_handle::release() noexcept
    {
        const HANDLE value = m_value;
        m_value = nullptr;
        return value;
    }

    void unique_handle::reset(HANDLE value) noexcept
    {
        if (m_value && m_value != INVALID_HANDLE_VALUE)
        {
            CloseHandle(m_value);
        }
        m_value = value;
    }

    unique_handle::operator bool() const noexcept
    {
        return m_value && m_value != INVALID_HANDLE_VALUE;
    }

    local_memory::local_memory(void* value) noexcept :
        m_value(value)
    {
    }

    local_memory::~local_memory()
    {
        if (m_value)
        {
            LocalFree(m_value);
        }
    }

    local_memory::local_memory(local_memory&& other) noexcept :
        m_value(other.m_value)
    {
        other.m_value = nullptr;
    }

    local_memory& local_memory::operator=(local_memory&& other) noexcept
    {
        if (this != &other)
        {
            if (m_value)
            {
                LocalFree(m_value);
            }
            m_value = other.m_value;
            other.m_value = nullptr;
        }
        return *this;
    }

    void* local_memory::get() const noexcept
    {
        return m_value;
    }

    void check_bool(BOOL result, const char* operation)
    {
        if (!result)
        {
            throw win32_error(operation, GetLastError());
        }
    }

    std::wstring current_token_user_sid(HANDLE token)
    {
        unique_handle current;
        if (!token)
        {
            HANDLE raw = nullptr;
            check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw), "OpenProcessToken");
            current.reset(raw);
            token = current.get();
        }
        DWORD bytes = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("GetTokenInformation(TokenUser size)", GetLastError());
        }
        std::vector<BYTE> buffer(bytes);
        check_bool(
            GetTokenInformation(token, TokenUser, buffer.data(), bytes, &bytes),
            "GetTokenInformation(TokenUser)");
        const auto* user = reinterpret_cast<const TOKEN_USER*>(buffer.data());
        return sid_to_string(user->User.Sid);
    }

    bool token_contains_sid(HANDLE token, std::wstring_view sidText)
    {
        std::wstring text(sidText);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(text.c_str(), &sid))
        {
            throw win32_error("ConvertStringSidToSidW(token membership)", GetLastError());
        }
        local_memory memory(sid);
        BOOL member = FALSE;
        check_bool(CheckTokenMembership(token, sid, &member), "CheckTokenMembership");
        return member != FALSE;
    }

    bool token_is_administrator(HANDLE token)
    {
        return token_contains_sid(token, L"S-1-5-32-544");
    }

    std::wstring canonical_owner_sid(std::wstring_view value)
    {
        if (value.empty() || value.size() >= MaxOwnerSidChars)
        {
            throw win32_error("owner SID length", ERROR_INVALID_SID);
        }
        std::wstring copy(value);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(copy.c_str(), &sid))
        {
            throw win32_error("ConvertStringSidToSidW(owner)", GetLastError());
        }
        local_memory memory(sid);
        const SID_IDENTIFIER_AUTHORITY expectedAuthority = SECURITY_NT_AUTHORITY;
        if (!IsValidSid(sid) ||
            std::memcmp(
                GetSidIdentifierAuthority(sid),
                &expectedAuthority,
                sizeof(SID_IDENTIFIER_AUTHORITY)) != 0 ||
            *GetSidSubAuthorityCount(sid) != 5 ||
            *GetSidSubAuthority(sid, 0) != SECURITY_NT_NON_UNIQUE ||
            *GetSidSubAuthority(sid, 4) == 0)
        {
            throw win32_error("owner SID policy", ERROR_INVALID_SID);
        }
        return sid_to_string(sid);
    }

    InstanceNames instance_names(std::wstring_view ownerSid)
    {
        InstanceNames names;
        names.ownerSid = canonical_owner_sid(ownerSid);
        names.suffix = hex_digest(names.ownerSid).substr(0, 16);
        names.serviceName = L"PtLsmrRuntime_" + names.suffix;
        names.storeDirectory = program_data_root() / names.suffix;
        names.evidencePath = names.storeDirectory / L"evidence.txt";
        return names;
    }

    std::wstring service_sid(std::wstring_view serviceName)
    {
        std::wstring account = L"NT SERVICE\\";
        account.append(serviceName);
        DWORD sidBytes = 0;
        DWORD domainChars = 0;
        SID_NAME_USE use{};
        LookupAccountNameW(
            nullptr,
            account.c_str(),
            nullptr,
            &sidBytes,
            nullptr,
            &domainChars,
            &use);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("LookupAccountNameW(service SID size)", GetLastError());
        }
        std::vector<BYTE> sidBuffer(sidBytes);
        std::wstring domain(domainChars, L'\0');
        check_bool(
            LookupAccountNameW(
                nullptr,
                account.c_str(),
                sidBuffer.data(),
                &sidBytes,
                domain.data(),
                &domainChars,
                &use),
            "LookupAccountNameW(service SID)");
        return sid_to_string(sidBuffer.data());
    }

    std::filesystem::path program_data_root()
    {
        PWSTR path = nullptr;
        const HRESULT result = SHGetKnownFolderPath(FOLDERID_ProgramData, 0, nullptr, &path);
        if (FAILED(result))
        {
            throw win32_error("SHGetKnownFolderPath(FOLDERID_ProgramData)", HRESULT_CODE(result));
        }
        local_memory memory(path);
        return std::filesystem::path(path) / StoreRelativeRoot;
    }

    std::filesystem::path installed_updater_root()
    {
        PWSTR path = nullptr;
        const HRESULT result = SHGetKnownFolderPath(FOLDERID_ProgramFiles, 0, nullptr, &path);
        if (FAILED(result))
        {
            throw win32_error("SHGetKnownFolderPath(FOLDERID_ProgramFiles)", HRESULT_CODE(result));
        }
        local_memory memory(path);
        return std::filesystem::path(path) / L"PowerToys\\WorkspacesLocalServiceMultiRuntimePrototype";
    }

    std::wstring expected_package_full_name(uint16_t major)
    {
        if (major != 1 && major != 2)
        {
            throw win32_error("package version policy", ERROR_INVALID_PARAMETER);
        }
        return package_string_from_id(false, major);
    }

    std::wstring expected_package_family_name()
    {
        return package_string_from_id(true, 0);
    }

    bool is_allowed_package_full_name(std::wstring_view value)
    {
        return value == expected_package_full_name(1) || value == expected_package_full_name(2);
    }

    uint16_t package_major_version(std::wstring_view fullName)
    {
        std::wstring copy(fullName);
        UINT32 bytes = 0;
        LONG result = PackageIdFromFullName(copy.c_str(), 0, &bytes, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("PackageIdFromFullName(size)", static_cast<DWORD>(result));
        }
        std::vector<BYTE> buffer(bytes);
        result = PackageIdFromFullName(
            copy.c_str(),
            0,
            &bytes,
            buffer.data());
        if (result != ERROR_SUCCESS)
        {
            throw win32_error("PackageIdFromFullName", static_cast<DWORD>(result));
        }
        const auto* id = reinterpret_cast<const PACKAGE_ID*>(buffer.data());
        return id->version.Major;
    }

    std::wstring quote_argument(std::wstring_view value)
    {
        std::wstring quoted = L"\"";
        for (const wchar_t character : value)
        {
            if (character == L'"')
            {
                throw win32_error("argument quote policy", ERROR_INVALID_PARAMETER);
            }
            quoted += character;
        }
        quoted += L"\"";
        return quoted;
    }

    std::vector<std::wstring> command_line_arguments()
    {
        int count = 0;
        LPWSTR* raw = CommandLineToArgvW(GetCommandLineW(), &count);
        if (!raw)
        {
            throw win32_error("CommandLineToArgvW", GetLastError());
        }
        local_memory memory(raw);
        std::vector<std::wstring> arguments;
        arguments.reserve(static_cast<size_t>(count));
        for (int index = 0; index < count; ++index)
        {
            arguments.emplace_back(raw[index]);
        }
        return arguments;
    }

    std::wstring argument_value(
        const std::vector<std::wstring>& arguments,
        std::wstring_view name)
    {
        for (size_t index = 0; index + 1 < arguments.size(); ++index)
        {
            if (arguments[index] == name)
            {
                return arguments[index + 1];
            }
        }
        return {};
    }

    void protect_directory_for_service(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid)
    {
        const std::wstring sddl =
            L"O:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;FA;;;" +
            std::wstring(serviceSid) + L")";
        protect_directory(directory, sddl);
    }

    void protect_system_directory(const std::filesystem::path& directory)
    {
        try
        {
            protect_directory(directory, L"O:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)");
        }
        catch (const win32_error& error)
        {
            if (error.code() != ERROR_INVALID_OWNER)
            {
                throw;
            }
            protect_directory(directory, L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)");
        }
    }

    void write_utf8_file_atomic(const std::filesystem::path& path, std::wstring_view value)
    {
        const std::filesystem::path temporary = path.wstring() + L".new";
        std::vector<char> utf8;
        if (!value.empty())
        {
            const int bytes = WideCharToMultiByte(
                CP_UTF8,
                WC_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                nullptr,
                0,
                nullptr,
                nullptr);
            if (bytes <= 0)
            {
                throw win32_error("WideCharToMultiByte(size)", GetLastError());
            }
            utf8.resize(static_cast<size_t>(bytes));
            if (WideCharToMultiByte(
                    CP_UTF8,
                    WC_ERR_INVALID_CHARS,
                    value.data(),
                    static_cast<int>(value.size()),
                    utf8.data(),
                    bytes,
                    nullptr,
                    nullptr) != bytes)
            {
                throw win32_error("WideCharToMultiByte", GetLastError());
            }
        }
        unique_handle file(CreateFileW(
            temporary.c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr));
        if (!file)
        {
            throw win32_error("CreateFileW(evidence)", GetLastError());
        }
        if (!utf8.empty())
        {
            DWORD written = 0;
            check_bool(
                WriteFile(file.get(), utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr) &&
                    written == utf8.size(),
                "WriteFile(evidence)");
        }
        check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers(evidence)");
        file.reset();
        check_bool(
            MoveFileExW(
                temporary.c_str(),
                path.c_str(),
                MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH),
            "MoveFileExW(evidence)");
    }

    std::wstring read_utf8_file(const std::filesystem::path& path, size_t maximumBytes)
    {
        const auto size = std::filesystem::file_size(path);
        if (size > maximumBytes)
        {
            throw win32_error("evidence file size policy", ERROR_FILE_TOO_LARGE);
        }
        if (size == 0)
        {
            return {};
        }
        std::ifstream input(path, std::ios::binary);
        if (!input)
        {
            throw win32_error("open UTF-8 file", ERROR_OPEN_FAILED);
        }
        std::string bytes(static_cast<size_t>(size), '\0');
        input.read(bytes.data(), static_cast<std::streamsize>(bytes.size()));
        if (!input && !input.eof())
        {
            throw win32_error("read UTF-8 file", ERROR_READ_FAULT);
        }
        const int characters = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            bytes.data(),
            static_cast<int>(bytes.size()),
            nullptr,
            0);
        if (characters <= 0)
        {
            throw win32_error("MultiByteToWideChar(size)", GetLastError());
        }
        std::wstring result(static_cast<size_t>(characters), L'\0');
        if (MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                bytes.data(),
                static_cast<int>(bytes.size()),
                result.data(),
                characters) != characters)
        {
            throw win32_error("MultiByteToWideChar", GetLastError());
        }
        return result;
    }
}
