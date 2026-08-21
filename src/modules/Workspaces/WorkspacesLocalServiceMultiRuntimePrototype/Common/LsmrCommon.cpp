#include "LsmrCommon.h"

#include <aclapi.h>
#include <appmodel.h>
#include <bcrypt.h>
#include <softpub.h>
#include <sddl.h>
#include <shellapi.h>
#include <shlobj_core.h>
#include <wincrypt.h>
#include <wintrust.h>

#include <algorithm>
#include <array>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <optional>
#include <sstream>
#include <tuple>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "version.lib")
#pragma comment(lib, "wintrust.lib")

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
        const NTSTATUS openStatus = BCryptOpenAlgorithmProvider(
            &algorithm,
            BCRYPT_SHA256_ALGORITHM,
            nullptr,
            0);
        if (openStatus < 0)
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
        } algorithmGuard{ algorithm };

        DWORD objectBytes = 0;
        DWORD resultBytes = 0;
        const NTSTATUS propertyStatus = BCryptGetProperty(
            algorithm,
            BCRYPT_OBJECT_LENGTH,
            reinterpret_cast<PUCHAR>(&objectBytes),
            sizeof(objectBytes),
            &resultBytes,
            0);
        if (propertyStatus < 0)
        {
            throw std::runtime_error("BCryptGetProperty(BCRYPT_OBJECT_LENGTH) failed");
        }
        std::vector<UCHAR> object(objectBytes);
        std::array<UCHAR, 32> digest{};
        std::vector<wchar_t> inputCopy(input.begin(), input.end());
        BCRYPT_HASH_HANDLE hash = nullptr;
        const NTSTATUS createStatus = BCryptCreateHash(
            algorithm,
            &hash,
            object.data(),
            static_cast<ULONG>(object.size()),
            nullptr,
            0,
            0);
        if (createStatus < 0)
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
        const NTSTATUS hashStatus = BCryptHashData(
            hash,
            reinterpret_cast<PUCHAR>(inputCopy.data()),
            static_cast<ULONG>(inputCopy.size() * sizeof(wchar_t)),
            0);
        if (hashStatus < 0)
        {
            throw std::runtime_error("BCryptHashData failed");
        }
        const NTSTATUS finishStatus = BCryptFinishHash(
            hash,
            digest.data(),
            static_cast<ULONG>(digest.size()),
            0);
        if (finishStatus < 0)
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

    void protect_directory(const std::filesystem::path& directory, const std::wstring& sddl)
    {
        std::filesystem::create_directories(directory);
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

    void protect_with_owner_fallback(
        const std::filesystem::path& directory,
        const std::wstring& withOwner,
        const std::wstring& daclOnly)
    {
        try
        {
            protect_directory(directory, withOwner);
        }
        catch (const ptlsmr::win32_error& error)
        {
            if (error.code() != ERROR_INVALID_OWNER)
            {
                throw;
            }
            protect_directory(directory, daclOnly);
        }
    }

    [[nodiscard]] std::wstring certificate_sha256(PCCERT_CONTEXT certificate)
    {
        DWORD bytes = 0;
        if (!CertGetCertificateContextProperty(
                certificate,
                CERT_SHA256_HASH_PROP_ID,
                nullptr,
                &bytes) ||
            bytes != 32)
        {
            throw ptlsmr::win32_error(
                "CertGetCertificateContextProperty(CERT_SHA256_HASH_PROP_ID size)",
                GetLastError());
        }
        std::array<BYTE, 32> hash{};
        if (!CertGetCertificateContextProperty(
                certificate,
                CERT_SHA256_HASH_PROP_ID,
                hash.data(),
                &bytes))
        {
            throw ptlsmr::win32_error(
                "CertGetCertificateContextProperty(CERT_SHA256_HASH_PROP_ID)",
                GetLastError());
        }
        std::wstringstream output;
        for (const BYTE byte : hash)
        {
            output << std::hex << std::uppercase << std::setw(2) << std::setfill(L'0') <<
                static_cast<unsigned int>(byte);
        }
        return output.str();
    }

    [[nodiscard]] std::wstring verified_leaf_signer_sha256(const std::filesystem::path& path)
    {
        WINTRUST_FILE_INFO fileInfo{};
        fileInfo.cbStruct = sizeof(fileInfo);
        std::wstring mutablePath = path.wstring();
        fileInfo.pcwszFilePath = mutablePath.c_str();
        WINTRUST_DATA trustData{};
        trustData.cbStruct = sizeof(trustData);
        trustData.dwUIChoice = WTD_UI_NONE;
        trustData.fdwRevocationChecks = WTD_REVOKE_NONE;
        trustData.dwUnionChoice = WTD_CHOICE_FILE;
        trustData.pFile = &fileInfo;
        trustData.dwStateAction = WTD_STATEACTION_VERIFY;
        trustData.dwProvFlags = WTD_SAFER_FLAG;
        GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
        const LONG result = WinVerifyTrust(
            static_cast<HWND>(INVALID_HANDLE_VALUE),
            &action,
            &trustData);
        if (result != ERROR_SUCCESS)
        {
            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            (void)WinVerifyTrust(
                static_cast<HWND>(INVALID_HANDLE_VALUE),
                &action,
                &trustData);
            throw ptlsmr::win32_error(
                "WinVerifyTrust(LocalMachine Authenticode chain)",
                static_cast<DWORD>(static_cast<uint32_t>(result)));
        }

        CRYPT_PROVIDER_DATA* provider = WTHelperProvDataFromStateData(
            trustData.hWVTStateData);
        if (!provider || provider->csSigners != 1)
        {
            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            (void)WinVerifyTrust(
                static_cast<HWND>(INVALID_HANDLE_VALUE),
                &action,
                &trustData);
            throw ptlsmr::win32_error(
                "WinVerifyTrust provider signer cardinality policy",
                static_cast<DWORD>(static_cast<uint32_t>(TRUST_E_SUBJECT_NOT_TRUSTED)));
        }
        const CRYPT_PROVIDER_SGNR* signer = WTHelperGetProvSignerFromChain(
            provider,
            0,
            FALSE,
            0);
        if (!signer ||
            signer->dwError != ERROR_SUCCESS ||
            signer->csCertChain == 0 ||
            !signer->pasCertChain ||
            !signer->pasCertChain[0].pCert)
        {
            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            (void)WinVerifyTrust(
                static_cast<HWND>(INVALID_HANDLE_VALUE),
                &action,
                &trustData);
            throw ptlsmr::win32_error(
                "WinVerifyTrust verified leaf signer policy",
                static_cast<DWORD>(static_cast<uint32_t>(TRUST_E_SUBJECT_NOT_TRUSTED)));
        }
        const std::wstring pin = certificate_sha256(signer->pasCertChain[0].pCert);
        trustData.dwStateAction = WTD_STATEACTION_CLOSE;
        (void)WinVerifyTrust(
            static_cast<HWND>(INVALID_HANDLE_VALUE),
            &action,
            &trustData);
        return pin;
    }

    [[nodiscard]] std::wstring version_resource_string(
        const std::filesystem::path& path,
        std::wstring_view name)
    {
        const DWORD bytes = GetFileVersionInfoSizeW(path.c_str(), nullptr);
        if (bytes == 0)
        {
            throw ptlsmr::win32_error("GetFileVersionInfoSizeW", GetLastError());
        }
        std::vector<BYTE> data(bytes);
        if (!GetFileVersionInfoW(path.c_str(), 0, bytes, data.data()))
        {
            throw ptlsmr::win32_error("GetFileVersionInfoW", GetLastError());
        }
        const std::wstring query =
            L"\\StringFileInfo\\040904b0\\" + std::wstring(name);
        LPWSTR value = nullptr;
        UINT characters = 0;
        if (!VerQueryValueW(data.data(), query.c_str(), reinterpret_cast<void**>(&value), &characters) ||
            !value ||
            characters == 0)
        {
            throw ptlsmr::win32_error("VerQueryValueW(version resource string)", ERROR_RESOURCE_DATA_NOT_FOUND);
        }
        return std::wstring(value, characters - 1);
    }

    [[nodiscard]] ptlsmr::file_version fixed_file_version(const std::filesystem::path& path)
    {
        const DWORD bytes = GetFileVersionInfoSizeW(path.c_str(), nullptr);
        if (bytes == 0)
        {
            throw ptlsmr::win32_error("GetFileVersionInfoSizeW", GetLastError());
        }
        std::vector<BYTE> data(bytes);
        if (!GetFileVersionInfoW(path.c_str(), 0, bytes, data.data()))
        {
            throw ptlsmr::win32_error("GetFileVersionInfoW", GetLastError());
        }
        VS_FIXEDFILEINFO* value = nullptr;
        UINT valueBytes = 0;
        if (!VerQueryValueW(data.data(), L"\\", reinterpret_cast<void**>(&value), &valueBytes) ||
            !value ||
            valueBytes < sizeof(*value) ||
            value->dwSignature != VS_FFI_SIGNATURE)
        {
            throw ptlsmr::win32_error("VerQueryValueW(VS_FIXEDFILEINFO)", ERROR_RESOURCE_DATA_NOT_FOUND);
        }
        return {
            HIWORD(value->dwFileVersionMS),
            LOWORD(value->dwFileVersionMS),
            HIWORD(value->dwFileVersionLS),
            LOWORD(value->dwFileVersionLS),
        };
    }

    [[nodiscard]] ptlsmr::file_version validate_signed_executable(
        const std::filesystem::path& path,
        std::wstring_view expectedOriginalFilename,
        std::wstring_view expectedProductName,
        std::wstring_view expectedSignerPin)
    {
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error("candidate file policy", ERROR_FILE_NOT_FOUND);
        }
        DWORD binaryType = 0;
        if (!GetBinaryTypeW(path.c_str(), &binaryType))
        {
            throw ptlsmr::win32_error("GetBinaryTypeW(candidate)", GetLastError());
        }
        if (binaryType != SCS_64BIT_BINARY)
        {
            throw ptlsmr::win32_error("candidate x64 PE policy", ERROR_EXE_MACHINE_TYPE_MISMATCH);
        }
        const auto signerPin = verified_leaf_signer_sha256(path);
        if (signerPin != ptlsmr::canonical_signer_sha256(expectedSignerPin))
        {
            throw ptlsmr::win32_error(
                "candidate WinVerifyTrust leaf signer pin policy",
                static_cast<DWORD>(static_cast<uint32_t>(TRUST_E_SUBJECT_NOT_TRUSTED)));
        }
        if (version_resource_string(path, L"CompanyName") != ptlsmr::PrototypeCompanyName ||
            version_resource_string(path, L"ProductName") != expectedProductName ||
            version_resource_string(path, L"OriginalFilename") != expectedOriginalFilename)
        {
            throw ptlsmr::win32_error("candidate version-resource identity policy", ERROR_INVALID_DATA);
        }
        const auto version = fixed_file_version(path);
        if (version_resource_string(path, L"FileVersion") != ptlsmr::format_version(version) ||
            version_resource_string(path, L"ProductVersion") != ptlsmr::format_version(version))
        {
            throw ptlsmr::win32_error("candidate version-resource version policy", ERROR_INVALID_DATA);
        }
        return version;
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

    bool operator==(const file_version& left, const file_version& right) noexcept
    {
        return left.major == right.major &&
            left.minor == right.minor &&
            left.build == right.build &&
            left.revision == right.revision;
    }

    bool operator<(const file_version& left, const file_version& right) noexcept
    {
        return std::tie(left.major, left.minor, left.build, left.revision) <
            std::tie(right.major, right.minor, right.build, right.revision);
    }

    std::wstring format_version(const file_version& value)
    {
        return std::to_wstring(value.major) + L"." +
            std::to_wstring(value.minor) + L"." +
            std::to_wstring(value.build) + L"." +
            std::to_wstring(value.revision);
    }

    file_version parse_version(std::wstring_view value)
    {
        std::array<uint16_t, 4> values{};
        size_t start = 0;
        for (size_t index = 0; index < values.size(); ++index)
        {
            const size_t end = value.find(L'.', start);
            if ((index < values.size() - 1 && end == std::wstring_view::npos) ||
                (index == values.size() - 1 && end != std::wstring_view::npos))
            {
                throw win32_error("version format policy", ERROR_INVALID_DATA);
            }
            const auto token = value.substr(
                start,
                (end == std::wstring_view::npos ? value.size() : end) - start);
            if (token.empty() || token.size() > 5 ||
                !std::all_of(token.begin(), token.end(), [](wchar_t character) {
                    return character >= L'0' && character <= L'9';
                }))
            {
                throw win32_error("version component policy", ERROR_INVALID_DATA);
            }
            unsigned long component = 0;
            try
            {
                component = std::stoul(std::wstring(token));
            }
            catch (const std::exception&)
            {
                throw win32_error("version range policy", ERROR_INVALID_DATA);
            }
            if (component > UINT16_MAX)
            {
                throw win32_error("version range policy", ERROR_INVALID_DATA);
            }
            values[index] = static_cast<uint16_t>(component);
            start = end == std::wstring_view::npos ? value.size() : end + 1;
        }
        return { values[0], values[1], values[2], values[3] };
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
        return sid_to_string(reinterpret_cast<const TOKEN_USER*>(buffer.data())->User.Sid);
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
        DWORD bytes = 0;
        GetTokenInformation(token, TokenGroups, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw win32_error("GetTokenInformation(TokenGroups size)", GetLastError());
        }
        std::vector<BYTE> buffer(bytes);
        check_bool(
            GetTokenInformation(token, TokenGroups, buffer.data(), bytes, &bytes),
            "GetTokenInformation(TokenGroups)");
        const auto* groups = reinterpret_cast<const TOKEN_GROUPS*>(buffer.data());
        for (DWORD index = 0; index < groups->GroupCount; ++index)
        {
            if (EqualSid(groups->Groups[index].Sid, sid))
            {
                return true;
            }
        }
        return current_token_user_sid(token) == text;
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
        names.serviceName = L"PtPuvrRuntime_" + names.suffix;
        names.storeDirectory = program_data_root() / names.suffix;
        names.evidencePath = names.storeDirectory / L"evidence.txt";
        return names;
    }

    std::wstring service_sid(std::wstring_view serviceName)
    {
        const std::wstring account = L"NT SERVICE\\" + std::wstring(serviceName);
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

    std::filesystem::path installation_root()
    {
        PWSTR path = nullptr;
        const HRESULT result = SHGetKnownFolderPath(FOLDERID_ProgramFiles, 0, nullptr, &path);
        if (FAILED(result))
        {
            throw win32_error("SHGetKnownFolderPath(FOLDERID_ProgramFiles)", HRESULT_CODE(result));
        }
        local_memory memory(path);
        return std::filesystem::path(path) / InstallRelativeRoot;
    }

    std::filesystem::path updater_install_directory(const file_version& version)
    {
        return installation_root() / L"Updater" / format_version(version);
    }

    std::filesystem::path runtime_root()
    {
        return installation_root() / L"Runtimes";
    }

    std::filesystem::path runtime_install_directory(
        uint16_t track,
        const file_version& version)
    {
        if (track != 1 && track != 2)
        {
            throw win32_error("runtime track policy", ERROR_INVALID_PARAMETER);
        }
        return runtime_root() /
            (L"Track" + std::to_wstring(track)) /
            format_version(version);
    }

    std::filesystem::path runtime_executable_path(
        uint16_t track,
        const file_version& version)
    {
        return runtime_install_directory(track, version) / RuntimeExe;
    }

    std::filesystem::path trusted_signer_pin_path()
    {
        return program_data_root() / TrustedSignerPinFile;
    }

    bool path_is_within(
        const std::filesystem::path& child,
        const std::filesystem::path& parent)
    {
        const auto canonicalChild = std::filesystem::weakly_canonical(child).wstring();
        std::wstring canonicalParent = std::filesystem::weakly_canonical(parent).wstring();
        if (!canonicalParent.ends_with(L"\\"))
        {
            canonicalParent += L"\\";
        }
        return canonicalChild.size() > canonicalParent.size() &&
            CompareStringOrdinal(
                canonicalChild.c_str(),
                static_cast<int>(canonicalParent.size()),
                canonicalParent.c_str(),
                static_cast<int>(canonicalParent.size()),
                TRUE) == CSTR_EQUAL;
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

    bool has_argument(
        const std::vector<std::wstring>& arguments,
        std::wstring_view name)
    {
        return std::find(arguments.begin(), arguments.end(), name) != arguments.end();
    }

    std::wstring canonical_signer_sha256(std::wstring_view value)
    {
        if (value.size() != 64)
        {
            throw win32_error("signer SHA-256 fingerprint length policy", ERROR_INVALID_DATA);
        }
        std::wstring normalized;
        normalized.reserve(value.size());
        for (const wchar_t character : value)
        {
            if ((character < L'0' || character > L'9') &&
                (character < L'a' || character > L'f') &&
                (character < L'A' || character > L'F'))
            {
                throw win32_error("signer SHA-256 fingerprint format policy", ERROR_INVALID_DATA);
            }
            normalized += static_cast<wchar_t>(towupper(character));
        }
        return normalized;
    }

    std::wstring read_trusted_signer_pin()
    {
        const auto path = trusted_signer_pin_path();
        if (!std::filesystem::is_regular_file(path))
        {
            throw win32_error("trusted signer pin policy missing", ERROR_FILE_NOT_FOUND);
        }
        return canonical_signer_sha256(read_utf8_file(path, 128));
    }

    void write_trusted_signer_pin(std::wstring_view value)
    {
        const auto pin = canonical_signer_sha256(value);
        const auto path = trusted_signer_pin_path();
        if (std::filesystem::exists(path) && read_trusted_signer_pin() != pin)
        {
            throw win32_error("trusted signer pin rotation policy", ERROR_ACCESS_DENIED);
        }
        write_utf8_file_atomic(path, pin);
    }

    DWORD require_no_package_identity()
    {
        UINT32 length = 0;
        const LONG result = GetCurrentPackageFullName(&length, nullptr);
        if (result != APPMODEL_ERROR_NO_PACKAGE)
        {
            throw win32_error(
                "GetCurrentPackageFullName ordinary-process policy",
                static_cast<DWORD>(static_cast<uint32_t>(result)));
        }
        return static_cast<DWORD>(result);
    }

    void protect_system_directory(const std::filesystem::path& directory)
    {
        protect_with_owner_fallback(
            directory,
            L"O:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)",
            L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)");
    }

    void protect_runtime_directory(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid)
    {
        std::wstring dacl =
            L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;GRGX;;;BU)";
        if (!serviceSid.empty())
        {
            dacl += L"(A;OICI;GRGX;;;" + std::wstring(serviceSid) + L")";
        }
        protect_with_owner_fallback(directory, L"O:SY" + dacl, dacl);
    }

    void protect_directory_for_service(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid)
    {
        if (serviceSid.empty())
        {
            throw win32_error("service store SID policy", ERROR_INVALID_SID);
        }
        const std::wstring dacl =
            L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;FA;;;" +
            std::wstring(serviceSid) + L")";
        protect_with_owner_fallback(directory, L"O:SY" + dacl, dacl);
    }

    std::filesystem::path create_protected_staging_directory(
        const std::filesystem::path& parent,
        std::wstring_view prefix)
    {
        protect_system_directory(parent);
        std::array<UCHAR, 16> entropy{};
        const NTSTATUS result = BCryptGenRandom(
            nullptr,
            entropy.data(),
            static_cast<ULONG>(entropy.size()),
            BCRYPT_USE_SYSTEM_PREFERRED_RNG);
        if (result < 0)
        {
            throw std::runtime_error("BCryptGenRandom(staging) failed");
        }
        std::wstringstream suffix;
        for (const auto byte : entropy)
        {
            suffix << std::hex << std::setw(2) << std::setfill(L'0') <<
                static_cast<unsigned int>(byte);
        }
        const auto directory = parent / (std::wstring(prefix) + L"-" + suffix.str());
        if (!CreateDirectoryW(directory.c_str(), nullptr))
        {
            throw win32_error("CreateDirectoryW(protected staging)", GetLastError());
        }
        protect_system_directory(directory);
        return directory;
    }

    void copy_file_to_protected_stage(
        const std::filesystem::path& source,
        const std::filesystem::path& stagedFile)
    {
        if (!std::filesystem::is_regular_file(source))
        {
            throw win32_error("candidate source policy", ERROR_FILE_NOT_FOUND);
        }
        if (!CopyFileW(source.c_str(), stagedFile.c_str(), TRUE))
        {
            throw win32_error("CopyFileW(protected staging)", GetLastError());
        }
    }

    void move_file_atomically(
        const std::filesystem::path& source,
        const std::filesystem::path& destination)
    {
        if (!MoveFileExW(
                source.c_str(),
                destination.c_str(),
                MOVEFILE_WRITE_THROUGH))
        {
            throw win32_error("MoveFileExW(atomic install)", GetLastError());
        }
    }

    bool files_are_identical(
        const std::filesystem::path& first,
        const std::filesystem::path& second)
    {
        if (!std::filesystem::is_regular_file(first) ||
            !std::filesystem::is_regular_file(second) ||
            std::filesystem::file_size(first) != std::filesystem::file_size(second))
        {
            return false;
        }
        std::ifstream firstStream(first, std::ios::binary);
        std::ifstream secondStream(second, std::ios::binary);
        if (!firstStream || !secondStream)
        {
            throw win32_error("open file comparison", ERROR_OPEN_FAILED);
        }
        std::array<char, 64 * 1024> firstBuffer{};
        std::array<char, 64 * 1024> secondBuffer{};
        while (firstStream)
        {
            firstStream.read(firstBuffer.data(), firstBuffer.size());
            secondStream.read(secondBuffer.data(), secondBuffer.size());
            const auto firstBytes = firstStream.gcount();
            const auto secondBytes = secondStream.gcount();
            if (firstBytes != secondBytes ||
                !std::equal(
                    firstBuffer.begin(),
                    firstBuffer.begin() + firstBytes,
                    secondBuffer.begin()))
            {
                return false;
            }
        }
        return firstStream.eof() && secondStream.eof();
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
            throw win32_error("CreateFileW(protected state)", GetLastError());
        }
        if (!utf8.empty())
        {
            DWORD written = 0;
            check_bool(
                WriteFile(file.get(), utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr) &&
                    written == utf8.size(),
                "WriteFile(protected state)");
        }
        check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers(protected state)");
        file.reset();
        check_bool(
            MoveFileExW(
                temporary.c_str(),
                path.c_str(),
                MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH),
            "MoveFileExW(protected state)");
    }

    std::wstring read_utf8_file(const std::filesystem::path& path, size_t maximumBytes)
    {
        const auto size = std::filesystem::file_size(path);
        if (size > maximumBytes)
        {
            throw win32_error("protected state size policy", ERROR_FILE_TOO_LARGE);
        }
        if (size == 0)
        {
            return {};
        }
        std::ifstream input(path, std::ios::binary);
        if (!input)
        {
            throw win32_error("open protected state", ERROR_OPEN_FAILED);
        }
        std::string bytes(static_cast<size_t>(size), '\0');
        input.read(bytes.data(), static_cast<std::streamsize>(bytes.size()));
        if (!input && !input.eof())
        {
            throw win32_error("read protected state", ERROR_READ_FAULT);
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
        std::wstring output(static_cast<size_t>(characters), L'\0');
        if (MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                bytes.data(),
                static_cast<int>(bytes.size()),
                output.data(),
                characters) != characters)
        {
            throw win32_error("MultiByteToWideChar", GetLastError());
        }
        return output;
    }

    file_version validate_updater_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin)
    {
        const auto version = validate_signed_executable(
            path,
            UpdaterExe,
            UpdaterProductName,
            expectedSignerPin);
        const auto expected = parse_version(UpdaterVersion);
        if (!(version == expected))
        {
            throw win32_error("updater version policy", ERROR_REVISION_MISMATCH);
        }
        return version;
    }

    file_version validate_runtime_candidate(
        const std::filesystem::path& path,
        uint16_t expectedTrack,
        std::wstring_view expectedSignerPin)
    {
        if (expectedTrack != 1 && expectedTrack != 2)
        {
            throw win32_error("runtime track policy", ERROR_INVALID_PARAMETER);
        }
        const auto version = validate_signed_executable(
            path,
            RuntimeExe,
            RuntimeProductName,
            expectedSignerPin);
        if (version.major != expectedTrack)
        {
            throw win32_error("runtime track version policy", ERROR_REVISION_MISMATCH);
        }
        if ((expectedTrack == 1 && version.minor > 8) ||
            (expectedTrack == 2 && version != file_version{ 2, 0, 0, 0 }))
        {
            throw win32_error("runtime release-train policy", ERROR_REVISION_MISMATCH);
        }
        return version;
    }
}
