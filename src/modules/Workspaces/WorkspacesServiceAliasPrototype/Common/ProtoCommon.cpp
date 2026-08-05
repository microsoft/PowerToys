#include "ProtoCommon.h"

#include <Aclapi.h>
#include <bcrypt.h>
#include <sddl.h>
#include <shlobj.h>
#include <shellapi.h>
#include <userenv.h>

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cwchar>
#include <iomanip>
#include <sstream>
#include <system_error>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "userenv.lib")

namespace
{
    std::wstring sid_to_string(PSID sid)
    {
        LPWSTR value = nullptr;
        if (!ConvertSidToStringSidW(sid, &value))
        {
            throw ptap::win32_error("ConvertSidToStringSidW", GetLastError());
        }
        ptap::local_memory memory(value);
        return value;
    }

    ptap::local_memory string_to_sid(std::wstring_view sid)
    {
        PSID value = nullptr;
        std::wstring copy(sid);
        if (!ConvertStringSidToSidW(copy.c_str(), &value))
        {
            throw ptap::win32_error("ConvertStringSidToSidW", GetLastError());
        }
        return ptap::local_memory(value);
    }

    std::filesystem::path known_folder(REFKNOWNFOLDERID id)
    {
        PWSTR path = nullptr;
        const HRESULT result = SHGetKnownFolderPath(id, KF_FLAG_CREATE, nullptr, &path);
        if (FAILED(result))
        {
            throw ptap::win32_error("SHGetKnownFolderPath", HRESULT_CODE(result));
        }
        ptap::local_memory memory(path);
        return path;
    }

    template<typename T>
    T read_fixed_file(const std::filesystem::path& path)
    {
        ptap::unique_handle file(CreateFileW(
            path.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr));
        if (!file)
        {
            throw ptap::win32_error("CreateFileW(read)", GetLastError());
        }
        LARGE_INTEGER size{};
        ptap::check_bool(GetFileSizeEx(file.get(), &size), "GetFileSizeEx");
        if (size.QuadPart != sizeof(T))
        {
            throw std::runtime_error("Unexpected prototype file size");
        }
        T value{};
        DWORD read = 0;
        ptap::check_bool(ReadFile(file.get(), &value, sizeof(value), &read, nullptr), "ReadFile");
        if (read != sizeof(value))
        {
            throw std::runtime_error("Short prototype file read");
        }
        return value;
    }

    template<typename T>
    void write_fixed_file_atomic(const std::filesystem::path& path, const T& value)
    {
        const auto temporary = path.wstring() + L".new";
        ptap::unique_handle file(CreateFileW(
            temporary.c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr));
        if (!file)
        {
            throw ptap::win32_error("CreateFileW(write)", GetLastError());
        }
        DWORD written = 0;
        ptap::check_bool(WriteFile(file.get(), &value, sizeof(value), &written, nullptr), "WriteFile");
        if (written != sizeof(value))
        {
            throw std::runtime_error("Short prototype file write");
        }
        ptap::check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers");
        file.reset();
        if (!MoveFileExW(temporary.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            const DWORD error = GetLastError();
            DeleteFileW(temporary.c_str());
            throw ptap::win32_error("MoveFileExW", error);
        }
    }

    void enable_privilege(const wchar_t* privilege)
    {
        HANDLE rawToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &rawToken),
            "OpenProcessToken(privilege)");
        ptap::unique_handle token(rawToken);
        TOKEN_PRIVILEGES privileges{};
        privileges.PrivilegeCount = 1;
        ptap::check_bool(
            LookupPrivilegeValueW(nullptr, privilege, &privileges.Privileges[0].Luid),
            "LookupPrivilegeValueW");
        privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
        SetLastError(ERROR_SUCCESS);
        ptap::check_bool(
            AdjustTokenPrivileges(token.get(), FALSE, &privileges, sizeof(privileges), nullptr, nullptr),
            "AdjustTokenPrivileges");
        if (GetLastError() == ERROR_NOT_ALL_ASSIGNED)
        {
            throw ptap::win32_error("AdjustTokenPrivileges", ERROR_PRIVILEGE_NOT_HELD);
        }
    }

    void set_directory_security(const std::filesystem::path& path, const std::wstring& sddl)
    {
        std::filesystem::create_directories(path);
        auto descriptor = ptap::security_descriptor_from_sddl(sddl);
        BOOL ownerDefaulted = FALSE;
        PSID owner = nullptr;
        ptap::check_bool(
            GetSecurityDescriptorOwner(descriptor.get(), &owner, &ownerDefaulted),
            "GetSecurityDescriptorOwner");
        BOOL daclPresent = FALSE;
        BOOL daclDefaulted = FALSE;
        PACL dacl = nullptr;
        ptap::check_bool(
            GetSecurityDescriptorDacl(descriptor.get(), &daclPresent, &dacl, &daclDefaulted),
            "GetSecurityDescriptorDacl");

        enable_privilege(SE_RESTORE_NAME);
        std::wstring mutablePath = path.wstring();
        const DWORD result = SetNamedSecurityInfoW(
            mutablePath.data(),
            SE_FILE_OBJECT,
            OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION,
            owner,
            nullptr,
            daclPresent ? dacl : nullptr,
            nullptr);
        if (result != ERROR_SUCCESS)
        {
            throw ptap::win32_error("SetNamedSecurityInfoW", result);
        }
    }
}

namespace ptap
{
    win32_error::win32_error(const char* operation, DWORD error) :
        std::runtime_error(std::string(operation) + " failed with Win32 error " + std::to_string(error)),
        m_code(error)
    {
    }

    DWORD win32_error::code() const noexcept
    {
        return m_code;
    }

    void check_bool(BOOL result, const char* operation)
    {
        if (!result)
        {
            throw win32_error(operation, GetLastError());
        }
    }

    void check_lstatus(LSTATUS result, const char* operation)
    {
        if (result != ERROR_SUCCESS)
        {
            throw win32_error(operation, static_cast<DWORD>(result));
        }
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
        m_value(other.release())
    {
    }

    local_memory& local_memory::operator=(local_memory&& other) noexcept
    {
        if (this != &other)
        {
            if (m_value)
            {
                LocalFree(m_value);
            }
            m_value = other.release();
        }
        return *this;
    }

    void* local_memory::get() const noexcept
    {
        return m_value;
    }

    void* local_memory::release() noexcept
    {
        void* value = m_value;
        m_value = nullptr;
        return value;
    }

    secret_buffer::secret_buffer(size_t characters) :
        m_value(characters)
    {
    }

    secret_buffer::~secret_buffer()
    {
        if (!m_value.empty())
        {
            SecureZeroMemory(m_value.data(), m_value.size() * sizeof(wchar_t));
        }
    }

    secret_buffer::secret_buffer(secret_buffer&& other) noexcept :
        m_value(std::move(other.m_value))
    {
    }

    secret_buffer& secret_buffer::operator=(secret_buffer&& other) noexcept
    {
        if (this != &other)
        {
            if (!m_value.empty())
            {
                SecureZeroMemory(m_value.data(), m_value.size() * sizeof(wchar_t));
            }
            m_value = std::move(other.m_value);
        }
        return *this;
    }

    wchar_t* secret_buffer::data() noexcept
    {
        return m_value.data();
    }

    const wchar_t* secret_buffer::data() const noexcept
    {
        return m_value.data();
    }

    size_t secret_buffer::size() const noexcept
    {
        return m_value.size();
    }

    std::wstring format_error(DWORD error)
    {
        wchar_t* text = nullptr;
        const DWORD length = FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            nullptr,
            error,
            0,
            reinterpret_cast<LPWSTR>(&text),
            0,
            nullptr);
        local_memory memory(text);
        if (length == 0)
        {
            return L"Win32 error " + std::to_wstring(error);
        }
        std::wstring result(text, length);
        while (!result.empty() && (result.back() == L'\r' || result.back() == L'\n'))
        {
            result.pop_back();
        }
        return result;
    }

    std::wstring token_user_sid(HANDLE token)
    {
        DWORD bytes = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("GetTokenInformation(size)", GetLastError());
        }
        std::vector<std::byte> buffer(bytes);
        check_bool(GetTokenInformation(token, TokenUser, buffer.data(), bytes, &bytes), "GetTokenInformation");
        const auto user = reinterpret_cast<const TOKEN_USER*>(buffer.data());
        return sid_to_string(user->User.Sid);
    }

    std::wstring current_token_user_sid()
    {
        HANDLE raw = nullptr;
        check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw), "OpenProcessToken");
        unique_handle token(raw);
        return token_user_sid(token.get());
    }

    bool token_contains_sid(HANDLE token, std::wstring_view sid)
    {
        auto expected = string_to_sid(sid);
        DWORD bytes = 0;
        GetTokenInformation(token, TokenGroups, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("GetTokenInformation(TokenGroups size)", GetLastError());
        }
        std::vector<std::byte> buffer(bytes);
        check_bool(
            GetTokenInformation(token, TokenGroups, buffer.data(), bytes, &bytes),
            "GetTokenInformation(TokenGroups)");
        const auto groups = reinterpret_cast<const TOKEN_GROUPS*>(buffer.data());
        for (DWORD index = 0; index < groups->GroupCount; ++index)
        {
            if (EqualSid(groups->Groups[index].Sid, expected.get()))
            {
                return true;
            }
        }
        return EqualSid(
                   reinterpret_cast<PSID>(expected.get()),
                   reinterpret_cast<PSID>(string_to_sid(token_user_sid(token)).get())) != FALSE;
    }

    bool token_is_administrator(HANDLE token)
    {
        SID_IDENTIFIER_AUTHORITY authority = SECURITY_NT_AUTHORITY;
        PSID raw = nullptr;
        check_bool(
            AllocateAndInitializeSid(
                &authority,
                2,
                SECURITY_BUILTIN_DOMAIN_RID,
                DOMAIN_ALIAS_RID_ADMINS,
                0,
                0,
                0,
                0,
                0,
                0,
                &raw),
            "AllocateAndInitializeSid");
        struct sid_guard
        {
            PSID value;
            ~sid_guard()
            {
                FreeSid(value);
            }
        } guard{ raw };
        BOOL member = FALSE;
        check_bool(CheckTokenMembership(token, raw, &member), "CheckTokenMembership(admin)");
        return member != FALSE;
    }

    std::wstring sid_for_account(std::wstring_view account)
    {
        std::wstring accountCopy(account);
        DWORD sidBytes = 0;
        DWORD domainChars = 0;
        SID_NAME_USE use{};
        LookupAccountNameW(nullptr, accountCopy.c_str(), nullptr, &sidBytes, nullptr, &domainChars, &use);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("LookupAccountNameW(size)", GetLastError());
        }
        std::vector<std::byte> sid(sidBytes);
        std::vector<wchar_t> domain(domainChars);
        check_bool(
            LookupAccountNameW(
                nullptr,
                accountCopy.c_str(),
                sid.data(),
                &sidBytes,
                domain.data(),
                &domainChars,
                &use),
            "LookupAccountNameW");
        return sid_to_string(reinterpret_cast<PSID>(sid.data()));
    }

    std::wstring service_sid(std::wstring_view serviceName)
    {
        if (serviceName.empty() || serviceName.size() > 256)
        {
            throw win32_error("Service SID name policy", ERROR_INVALID_NAME);
        }
        std::wstring upper(serviceName);
        if (CharUpperBuffW(upper.data(), static_cast<DWORD>(upper.size())) != upper.size())
        {
            throw win32_error("CharUpperBuffW(service SID)", GetLastError());
        }
        BCRYPT_ALG_HANDLE algorithm = nullptr;
        if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA1_ALGORITHM, nullptr, 0) != 0)
        {
            throw std::runtime_error("BCryptOpenAlgorithmProvider(SHA1) failed");
        }
        struct algorithm_guard
        {
            BCRYPT_ALG_HANDLE value;
            ~algorithm_guard()
            {
                BCryptCloseAlgorithmProvider(value, 0);
            }
        } guard{ algorithm };
        std::array<UCHAR, 20> digest{};
        if (BCryptHash(
                algorithm,
                nullptr,
                0,
                reinterpret_cast<PUCHAR>(upper.data()),
                static_cast<ULONG>(upper.size() * sizeof(wchar_t)),
                digest.data(),
                static_cast<ULONG>(digest.size())) != 0)
        {
            throw std::runtime_error("BCryptHash(service SID) failed");
        }
        std::wostringstream output;
        output << L"S-1-5-80";
        for (size_t index = 0; index < digest.size(); index += sizeof(uint32_t))
        {
            uint32_t subAuthority = 0;
            memcpy(&subAuthority, digest.data() + index, sizeof(subAuthority));
            output << L"-" << subAuthority;
        }
        return output.str();
    }

    std::wstring owner_hash(std::wstring_view ownerSid)
    {
        BCRYPT_ALG_HANDLE algorithm = nullptr;
        if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) != 0)
        {
            throw std::runtime_error("BCryptOpenAlgorithmProvider failed");
        }
        struct algorithm_guard
        {
            BCRYPT_ALG_HANDLE value;
            ~algorithm_guard()
            {
                BCryptCloseAlgorithmProvider(value, 0);
            }
        } guard{ algorithm };
        std::array<UCHAR, 32> digest{};
        std::wstring ownerCopy(ownerSid);
        const auto bytes = reinterpret_cast<PUCHAR>(ownerCopy.data());
        const ULONG byteCount = static_cast<ULONG>(ownerCopy.size() * sizeof(wchar_t));
        if (BCryptHash(algorithm, nullptr, 0, bytes, byteCount, digest.data(), static_cast<ULONG>(digest.size())) != 0)
        {
            throw std::runtime_error("BCryptHash failed");
        }
        std::wostringstream output;
        output << std::hex << std::setfill(L'0');
        for (size_t index = 0; index < 4; ++index)
        {
            output << std::setw(2) << static_cast<unsigned>(digest[index]);
        }
        return output.str();
    }

    InstanceNames instance_names(std::wstring_view ownerSid)
    {
        const std::wstring suffix = owner_hash(ownerSid);
        const auto programData = known_folder(FOLDERID_ProgramData);
        const auto programFiles = known_folder(FOLDERID_ProgramFiles);
        InstanceNames result;
        result.suffix = suffix;
        result.accountName = L"PtAliasProto" + suffix;
        result.serviceName = L"PtAliasProtoSvc_" + suffix;
        result.pipeName = L"\\\\.\\pipe\\PtAliasProto_" + suffix;
        result.storeDirectory = programData / StoreRootName / suffix;
        result.statePath = result.storeDirectory / L"state.bin";
        result.evidencePath = result.storeDirectory / L"evidence.bin";
        result.launcherDirectory = programFiles / L"PowerToys" / L"PtAliasProto" / suffix;
        result.launcherPath = result.launcherDirectory / L"PtAliasProtoLauncher.exe";
        return result;
    }

    std::wstring expected_package_family_name()
    {
        PACKAGE_ID id{};
        std::wstring name(PackageName);
        std::wstring publisher(PackagePublisher);
        id.name = name.data();
        id.publisher = publisher.data();
        UINT32 length = 0;
        LONG result = PackageFamilyNameFromId(&id, &length, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("PackageFamilyNameFromId(size)", result);
        }
        std::wstring family(length, L'\0');
        result = PackageFamilyNameFromId(&id, &length, family.data());
        if (result != ERROR_SUCCESS)
        {
            throw win32_error("PackageFamilyNameFromId", result);
        }
        family.resize(length - 1);
        return family;
    }

    PackageIdentity validate_package_full_name(std::wstring_view fullName)
    {
        if (fullName.empty() || fullName.size() >= 256 || fullName.find_first_of(L"\\/\r\n\t") != std::wstring_view::npos)
        {
            throw win32_error("Package full name policy", ERROR_INVALID_DATA);
        }
        std::wstring copy(fullName);
        UINT32 bytes = 0;
        LONG result = PackageIdFromFullName(copy.c_str(), 0, &bytes, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("PackageIdFromFullName(size)", result);
        }
        std::vector<std::byte> buffer(bytes);
        auto id = reinterpret_cast<PACKAGE_ID*>(buffer.data());
        result = PackageIdFromFullName(copy.c_str(), 0, &bytes, reinterpret_cast<BYTE*>(id));
        if (result != ERROR_SUCCESS)
        {
            throw win32_error("PackageIdFromFullName", result);
        }
        if (!id->name || wcscmp(id->name, PackageName) != 0 || (id->resourceId && *id->resourceId))
        {
            throw win32_error("Package identity policy", ERROR_INVALID_DATA);
        }
        if (id->processorArchitecture != PROCESSOR_ARCHITECTURE_AMD64)
        {
            throw win32_error("Package architecture policy", ERROR_INVALID_DATA);
        }
        UINT32 familyChars = 0;
        result = PackageFamilyNameFromFullName(copy.c_str(), &familyChars, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("PackageFamilyNameFromFullName(size)", result);
        }
        std::wstring family(familyChars, L'\0');
        result = PackageFamilyNameFromFullName(copy.c_str(), &familyChars, family.data());
        if (result != ERROR_SUCCESS)
        {
            throw win32_error("PackageFamilyNameFromFullName", result);
        }
        family.resize(familyChars - 1);
        if (family != expected_package_family_name())
        {
            throw win32_error("Package family policy", ERROR_INVALID_DATA);
        }
        PackageIdentity identity;
        identity.fullName = copy;
        identity.familyName = family;
        identity.publisherId = id->publisherId ? id->publisherId : L"";
        identity.version = {
            id->version.Major,
            id->version.Minor,
            id->version.Build,
            id->version.Revision,
        };
        identity.architecture = id->processorArchitecture;
        if (!is_allowed_version(identity.version))
        {
            throw win32_error("Package version policy", ERROR_REVISION_MISMATCH);
        }
        return identity;
    }

    uint64_t version_value(const PackageVersion& version) noexcept
    {
        return (static_cast<uint64_t>(version.major) << 48) |
               (static_cast<uint64_t>(version.minor) << 32) |
               (static_cast<uint64_t>(version.build) << 16) |
               version.revision;
    }

    uint32_t compact_version(const PackageVersion& version) noexcept
    {
        return (static_cast<uint32_t>(version.major) << 24) |
               (static_cast<uint32_t>(version.minor) << 16) |
               (static_cast<uint32_t>(version.build) << 8) |
               static_cast<uint32_t>(version.revision);
    }

    bool is_allowed_version(const PackageVersion& version) noexcept
    {
        return version.major >= 1 &&
               version.major <= 3 &&
               version.minor == 0 &&
               version.build == 0 &&
               version.revision == 0;
    }

    bool is_package_staged(std::wstring_view fullName)
    {
        std::wstring copy(fullName);
        UINT32 chars = 0;
        LONG result = GetStagedPackagePathByFullName(copy.c_str(), &chars, nullptr);
        if (result == APPMODEL_ERROR_NO_PACKAGE || result == ERROR_NOT_FOUND)
        {
            return false;
        }
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("GetStagedPackagePathByFullName(size)", result);
        }
        std::wstring path(chars, L'\0');
        result = GetStagedPackagePathByFullName(copy.c_str(), &chars, path.data());
        if (result != ERROR_SUCCESS)
        {
            throw win32_error("GetStagedPackagePathByFullName", result);
        }
        return true;
    }

    std::filesystem::path current_local_app_data()
    {
        return known_folder(FOLDERID_LocalAppData);
    }

    std::filesystem::path alias_path()
    {
        return current_local_app_data() / L"Microsoft" / L"WindowsApps" / AliasName;
    }

    PrototypeState read_state(const std::filesystem::path& path)
    {
        const auto state = read_fixed_file<PrototypeState>(path);
        if (state.magic != StateMagic || state.formatVersion != 1)
        {
            throw win32_error("Prototype state format", ERROR_INVALID_DATA);
        }
        return state;
    }

    void write_state_atomic(const std::filesystem::path& path, PrototypeState state)
    {
        state.magic = StateMagic;
        state.formatVersion = 1;
        ++state.stateGeneration;
        write_fixed_file_atomic(path, state);
    }

    void write_evidence_atomic(const std::filesystem::path& path, const EvidenceRecord& evidence)
    {
        write_fixed_file_atomic(path, evidence);
    }

    EvidenceRecord read_evidence(const std::filesystem::path& path)
    {
        const auto evidence = read_fixed_file<EvidenceRecord>(path);
        if (evidence.magic != StateMagic || evidence.formatVersion != 1)
        {
            throw win32_error("Evidence format", ERROR_INVALID_DATA);
        }
        return evidence;
    }

    void append_log(const std::filesystem::path& storeDirectory, std::wstring_view component, std::wstring_view message) noexcept
    {
        try
        {
            const auto path = storeDirectory / L"prototype.log";
            unique_handle file(CreateFileW(
                path.c_str(),
                FILE_APPEND_DATA,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                nullptr,
                OPEN_ALWAYS,
                FILE_ATTRIBUTE_NORMAL,
                nullptr));
            if (!file)
            {
                return;
            }
            SYSTEMTIME time{};
            GetSystemTime(&time);
            std::wostringstream line;
            line << std::setfill(L'0') << std::setw(4) << time.wYear << L"-" << std::setw(2) << time.wMonth << L"-"
                 << std::setw(2) << time.wDay << L"T" << std::setw(2) << time.wHour << L":" << std::setw(2)
                 << time.wMinute << L":" << std::setw(2) << time.wSecond << L"Z [" << component << L"] " << message
                 << L"\r\n";
            const std::wstring text = line.str();
            DWORD written = 0;
            WriteFile(file.get(), text.data(), static_cast<DWORD>(text.size() * sizeof(wchar_t)), &written, nullptr);
        }
        catch (...)
        {
            OutputDebugStringW(L"PtAliasProto: protected file logging failed.\n");
        }
    }

    local_memory security_descriptor_from_sddl(const std::wstring& sddl)
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl.c_str(),
                SDDL_REVISION_1,
                &descriptor,
                nullptr))
        {
            throw win32_error("ConvertStringSecurityDescriptorToSecurityDescriptorW", GetLastError());
        }
        return local_memory(descriptor);
    }

    void set_protected_directory_acl(
        const std::filesystem::path& path,
        std::wstring_view serviceAccountSid,
        std::wstring_view ownerSid,
        bool ownerReadOnly,
        bool serviceAccountFullControl)
    {
        std::wstring sddl = L"O:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
        sddl += serviceAccountFullControl ? L"(A;OICI;FA;;;" : L"(A;OICI;GRGX;;;";
        sddl += serviceAccountSid;
        sddl += L")";
        if (!ownerSid.empty())
        {
            sddl += ownerReadOnly ? L"(A;OICI;GRGX;;;" : L"(A;OICI;GRGWGX;;;";
            sddl += ownerSid;
            sddl += L")";
        }
        set_directory_security(path, sddl);
    }

    void set_protected_root_acl(const std::filesystem::path& path)
    {
        set_directory_security(
            path,
            L"O:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;;GRGX;;;BU)");
    }

    std::wstring quote_argument(std::wstring_view value)
    {
        std::wstring result = L"\"";
        size_t slashes = 0;
        for (const wchar_t character : value)
        {
            if (character == L'\\')
            {
                ++slashes;
                continue;
            }
            if (character == L'"')
            {
                result.append(slashes * 2 + 1, L'\\');
                result.push_back(L'"');
                slashes = 0;
                continue;
            }
            result.append(slashes, L'\\');
            slashes = 0;
            result.push_back(character);
        }
        result.append(slashes * 2, L'\\');
        result.push_back(L'"');
        return result;
    }

    std::vector<std::wstring> command_line_arguments()
    {
        int count = 0;
        LPWSTR* values = CommandLineToArgvW(GetCommandLineW(), &count);
        if (!values)
        {
            throw win32_error("CommandLineToArgvW", GetLastError());
        }
        local_memory memory(values);
        std::vector<std::wstring> result;
        result.reserve(count);
        for (int index = 0; index < count; ++index)
        {
            result.emplace_back(values[index]);
        }
        return result;
    }

    std::wstring argument_value(const std::vector<std::wstring>& args, std::wstring_view name)
    {
        for (size_t index = 1; index + 1 < args.size(); ++index)
        {
            if (args[index] == name)
            {
                return args[index + 1];
            }
        }
        return {};
    }

    bool has_argument(const std::vector<std::wstring>& args, std::wstring_view name)
    {
        return std::find(args.begin() + std::min<size_t>(1, args.size()), args.end(), name) != args.end();
    }

    void copy_bounded(wchar_t* destination, size_t destinationCount, std::wstring_view source)
    {
        if (!destination || destinationCount == 0 || source.size() >= destinationCount)
        {
            throw win32_error("Fixed string bounds", ERROR_INSUFFICIENT_BUFFER);
        }
        std::fill(destination, destination + destinationCount, L'\0');
        std::copy(source.begin(), source.end(), destination);
    }

    std::wstring bounded_string(const wchar_t* source, size_t sourceCount)
    {
        if (!source)
        {
            throw win32_error("Fixed string pointer", ERROR_INVALID_DATA);
        }
        const size_t length = wcsnlen_s(source, sourceCount);
        if (length == sourceCount)
        {
            throw win32_error("Fixed string termination", ERROR_INVALID_DATA);
        }
        return std::wstring(source, length);
    }

    uint64_t increment_launch_count(const std::filesystem::path& storeDirectory)
    {
        const auto path = storeDirectory / L"launch-count.bin";
        unique_handle file(CreateFileW(
            path.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr));
        if (!file)
        {
            throw win32_error("CreateFileW(launch count)", GetLastError());
        }
        uint64_t value = 0;
        DWORD read = 0;
        check_bool(ReadFile(file.get(), &value, sizeof(value), &read, nullptr), "ReadFile(launch count)");
        if (read != 0 && read != sizeof(value))
        {
            throw win32_error("Launch count format", ERROR_INVALID_DATA);
        }
        ++value;
        LARGE_INTEGER beginning{};
        check_bool(SetFilePointerEx(file.get(), beginning, nullptr, FILE_BEGIN), "SetFilePointerEx");
        DWORD written = 0;
        check_bool(WriteFile(file.get(), &value, sizeof(value), &written, nullptr), "WriteFile(launch count)");
        if (written != sizeof(value))
        {
            throw std::runtime_error("Short launch count write");
        }
        check_bool(SetEndOfFile(file.get()), "SetEndOfFile");
        check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers(launch count)");
        return value;
    }

    std::wstring make_nonce()
    {
        GUID value{};
        const HRESULT result = CoCreateGuid(&value);
        if (FAILED(result))
        {
            throw win32_error("CoCreateGuid", HRESULT_CODE(result));
        }
        wchar_t text[64]{};
        if (StringFromGUID2(value, text, ARRAYSIZE(text)) == 0)
        {
            throw std::runtime_error("StringFromGUID2 failed");
        }
        std::wstring nonce(text);
        nonce.erase(std::remove_if(nonce.begin(), nonce.end(), [](wchar_t character) {
                        return character == L'{' || character == L'}' || character == L'-';
                    }),
                    nonce.end());
        return nonce;
    }
}
