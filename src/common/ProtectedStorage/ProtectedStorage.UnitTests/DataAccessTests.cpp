#include "../ProtectedStorage.Common/Common.h"
#include <authz.h>
#include <filesystem>
#include <memory>
#include <winioctl.h>
#pragma comment(lib, "authz.lib")

using namespace PowerToys::ProtectedStorage;
namespace fs = std::filesystem;

namespace
{
    using Descriptor = std::unique_ptr<void, decltype(&LocalFree)>;
    Descriptor Parse(const std::wstring& sddl)
    {
        PSECURITY_DESCRIPTOR raw = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &raw, nullptr))
            Fail("test data access descriptor");
        return Descriptor(raw, LocalFree);
    }
    void SetDacl(const fs::path& path, const std::wstring& sddl)
    {
        auto descriptor = Parse(sddl);
        if (!SetFileSecurityW(path.c_str(), DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, descriptor.get()))
            Fail("test data DACL");
    }
    bool Allowed(PSECURITY_DESCRIPTOR descriptor, const std::wstring& sidText, DWORD access)
    {
        PSID raw = nullptr;
        if (!ConvertStringSidToSidW(sidText.c_str(), &raw)) Fail("test authorization SID");
        Descriptor sid(raw, LocalFree);
        AUTHZ_RESOURCE_MANAGER_HANDLE manager = nullptr;
        if (!AuthzInitializeResourceManager(AUTHZ_RM_FLAG_NO_AUDIT, nullptr, nullptr, nullptr, L"StorageAclTests", &manager))
            Fail("test authorization manager");
        struct Manager { AUTHZ_RESOURCE_MANAGER_HANDLE value; ~Manager() { AuthzFreeResourceManager(value); } } releaseManager{ manager };
        AUTHZ_CLIENT_CONTEXT_HANDLE context = nullptr;
        if (!AuthzInitializeContextFromSid(AUTHZ_SKIP_TOKEN_GROUPS, sid.get(), manager, nullptr, LUID{}, nullptr, &context))
            Fail("test authorization context");
        struct Context { AUTHZ_CLIENT_CONTEXT_HANDLE value; ~Context() { AuthzFreeContext(value); } } releaseContext{ context };
        AUTHZ_ACCESS_REQUEST request{};
        request.DesiredAccess = access;
        DWORD granted = 0, error = 0;
        AUTHZ_ACCESS_REPLY reply{};
        reply.ResultListLength = 1;
        reply.GrantedAccessMask = &granted;
        reply.Error = &error;
        if (!AuthzAccessCheck(0, context, &request, nullptr, descriptor, nullptr, 0, &reply, nullptr))
            Fail("test authorization check");
        return error == ERROR_SUCCESS && (granted & access) == access;
    }
    Descriptor Security(const fs::path& path)
    {
        PSECURITY_DESCRIPTOR raw = nullptr;
        const auto result = GetNamedSecurityInfoW(path.c_str(), SE_FILE_OBJECT,
            OWNER_SECURITY_INFORMATION | GROUP_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
            nullptr, nullptr, nullptr, nullptr, &raw);
        if (result != ERROR_SUCCESS) Fail("test file security", result);
        return Descriptor(raw, LocalFree);
    }
    void CheckReadOnly(PSECURITY_DESCRIPTOR descriptor, const std::wstring& reader)
    {
        Require(Allowed(descriptor, reader, FILE_GENERIC_READ), "troubleshooting reader cannot read");
        for (const DWORD mutation : std::array<DWORD, 8>{ FILE_WRITE_DATA, FILE_APPEND_DATA, FILE_WRITE_EA, FILE_WRITE_ATTRIBUTES,
                 FILE_DELETE_CHILD, DELETE, WRITE_DAC, WRITE_OWNER })
            Require(!Allowed(descriptor, reader, mutation), "troubleshooting reader acquired mutation authority");
    }
    void Junction(const fs::path& link, const fs::path& target)
    {
        fs::create_directory(link);
        Handle directory(CreateFileW(link.c_str(), GENERIC_WRITE, 0, nullptr, OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS, nullptr));
        if (!directory) Fail("open test junction");
        struct MountPoint
        {
            DWORD tag;
            WORD length, reserved, substituteOffset, substituteLength, printOffset, printLength;
        };
        static_assert(sizeof(MountPoint) == 16);
        const auto substitute = L"\\??\\" + fs::absolute(target).wstring();
        std::vector<BYTE> bytes(sizeof(MountPoint) + (substitute.size() + 2) * sizeof(wchar_t), 0);
        auto& point = *reinterpret_cast<MountPoint*>(bytes.data());
        point.tag = IO_REPARSE_TAG_MOUNT_POINT;
        point.length = static_cast<WORD>(bytes.size() - 8);
        point.substituteLength = static_cast<WORD>(substitute.size() * sizeof(wchar_t));
        point.printOffset = point.substituteLength + sizeof(wchar_t);
        memcpy(bytes.data() + sizeof(MountPoint), substitute.data(), point.substituteLength);
        DWORD returned = 0;
        if (!DeviceIoControl(directory.get(), FSCTL_SET_REPARSE_POINT, bytes.data(),
            static_cast<DWORD>(bytes.size()), nullptr, 0, &returned, nullptr))
            Fail("create test junction");
    }
}

void RunDataAccessTests()
{
    const std::wstring owner = L"S-1-5-21-101-102-103-1001";
    const std::wstring other = L"S-1-5-21-101-102-103-1002";
    const std::wstring service = L"S-1-5-80-101-102-103-104-105";
    for (bool directory : { false, true })
    {
        auto descriptor = Parse(L"O:SYG:SY" + DataReadAccessSddl(owner, service, directory));
        CheckReadOnly(descriptor.get(), owner);
        CheckReadOnly(descriptor.get(), L"S-1-5-32-544");
        Require(!Allowed(descriptor.get(), other, FILE_READ_DATA), "another owner can read private workspace data");
        Require(!Allowed(descriptor.get(), L"S-1-5-80-201-202-203-204-205", FILE_READ_DATA), "another VA can read private workspace data");
        Require(Allowed(descriptor.get(), service, FILE_ALL_ACCESS) &&
            Allowed(descriptor.get(), L"S-1-5-18", FILE_ALL_ACCESS), "SYSTEM/VA write authority was lost");
        Require(Allowed(descriptor.get(), owner, FILE_TRAVERSE) == directory,
            "reader file execute right differs from directory traversal");
    }
    const auto root = fs::current_path() / (L"storage-readonly-test-" + NewId());
    fs::create_directory(root);
    struct Cleanup { fs::path path; ~Cleanup() { std::error_code error; fs::remove_all(path, error); } } cleanup{ root };
    const auto writer = TokenSid();
    const auto data = root / L"Data";
    const auto code = root / L"Code";
    fs::create_directory(data);
    fs::create_directory(code);
    const auto privateAcl = L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + writer + L")";
    SetDacl(data, privateAcl);
    SetDacl(code, privateAcl);
    WriteNew((data / L"storage.index").wstring(), "unchanged-data");
    fs::create_directory(data / L"nested");
    WriteNew((data / L"nested" / L"runtime.log").wstring(), "diagnostic");
    const auto before = HashPath((data / L"storage.index").wstring());
    const auto beforeSecurity = Security(data / L"storage.index");
    Require(!Allowed(beforeSecurity.get(), owner, FILE_GENERIC_READ), "fixture was not private before migration");
    // Non-deploying fixture: the current process is the sole VA-like writer.
    EnsureDataReadAccess(data.wstring(), owner, writer);
    EnsureDataReadAccess(data.wstring(), owner, writer);
    Require(HashPath((data / L"storage.index").wstring()) == before, "ACL migration changed data bytes");
    for (const auto& path : { data, data / L"storage.index", data / L"nested", data / L"nested" / L"runtime.log" })
    {
        auto descriptor = Security(path);
        CheckReadOnly(descriptor.get(), owner);
        CheckReadOnly(descriptor.get(), L"S-1-5-32-544");
    }
    WriteNew((data / L"blob.new").wstring(), "new-generation");
    ReplaceText((data / L"storage.index").wstring(), "committed-next-generation");
    for (const auto& path : { data / L"blob.new", data / L"storage.index" })
    {
        auto descriptor = Security(path);
        CheckReadOnly(descriptor.get(), owner);
        CheckReadOnly(descriptor.get(), L"S-1-5-32-544");
        Require(!Allowed(descriptor.get(), other, FILE_GENERIC_READ), "new generation exposed to another user");
    }
    auto codeSecurity = Security(code);
    Require(!Allowed(codeSecurity.get(), owner, FILE_GENERIC_READ), "Data migration changed Code privacy");

    const auto bad = root / L"LinkedData";
    fs::create_directory(bad);
    SetDacl(bad, privateAcl);
    const auto outside = root / L"outside.txt";
    WriteNew(outside.wstring(), "untouched");
    if (!CreateHardLinkW((bad / L"linked").c_str(), outside.c_str(), nullptr)) Fail("create hardlink test fixture");
    bool rejected = false;
    try { EnsureDataReadAccess(bad.wstring(), owner, writer); }
    catch (const Error&) { rejected = true; }
    Require(rejected, "hardlinked ACL target accepted");
    auto rejectedSecurity = Security(bad);
    Require(!Allowed(rejectedSecurity.get(), owner, FILE_GENERIC_READ), "failed validation partially changed Data ACL");
    fs::remove(bad / L"linked");
    Require(ReadText(outside.wstring()) == "untouched", "linked file changed");

    Junction(bad / L"junction", code);
    rejected = false;
    try { EnsureDataReadAccess(bad.wstring(), owner, writer); }
    catch (const Error&) { rejected = true; }
    Require(rejected, "junction ACL target accepted");
    auto untouchedCode = Security(code);
    Require(!Allowed(untouchedCode.get(), owner, FILE_GENERIC_READ), "ACL migration followed a junction into Code");
    fs::remove(bad / L"junction");

    WriteNew((bad / L"foreign-writer").wstring(), "invalid-acl");
    SetDacl(bad / L"foreign-writer", privateAcl + L"(A;;FW;;;" + other + L")");
    rejected = false;
    try { EnsureDataReadAccess(bad.wstring(), owner, writer); }
    catch (const Error&) { rejected = true; }
    Require(rejected, "preexisting foreign write authority accepted");
    rejectedSecurity = Security(bad);
    Require(!Allowed(rejectedSecurity.get(), owner, FILE_GENERIC_READ), "rejected ACL tree partially migrated");
}
