#pragma once
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <sddl.h>
#include <aclapi.h>
#include <bcrypt.h>
#include <wincrypt.h>
#include <shlobj.h>
#include <winsvc.h>
#include <array>
#include <algorithm>
#include <cstdint>
#include <functional>
#include <map>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace PowerToys::ProtectedStorage
{
    constexpr DWORD IoTimeout = 10000;
    constexpr DWORD IdentityProcessAccess = PROCESS_QUERY_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE;
    constexpr DWORD StartTimeout = 25000;
    constexpr DWORD StopTimeout = 20000;
    constexpr size_t MaxText = 32768;
    constexpr uint64_t MaxPayload = 64ULL * 1024 * 1024;
    constexpr DWORD DecisionTimeout = 180000;
    constexpr wchar_t App[] = L"PowerToysProtectedStorage";
    constexpr DWORD ProtocolMagic = 0x3143534D;

    struct Error : std::runtime_error
    {
        DWORD code;
        explicit Error(const std::string& message, DWORD value = ERROR_INVALID_DATA) :
            std::runtime_error(message), code(value) {}
    };
    [[noreturn]] void Fail(const std::string& message, DWORD code = GetLastError());
    void Require(bool condition, const std::string& message);

    class Handle
    {
        HANDLE value = nullptr;

    public:
        Handle() = default;
        explicit Handle(HANDLE h) : value(h) {}
        ~Handle() { reset(); }
        Handle(const Handle&) = delete;
        Handle& operator=(const Handle&) = delete;
        Handle(Handle&& other) noexcept : value(other.release()) {}
        Handle& operator=(Handle&& other) noexcept
        {
            if (this != &other)
                reset(other.release());
            return *this;
        }
        explicit operator bool() const { return value && value != INVALID_HANDLE_VALUE; }
        HANDLE get() const { return value; }
        HANDLE release()
        {
            HANDLE h = value;
            value = nullptr;
            return h;
        }
        void reset(HANDLE h = nullptr)
        {
            if (*this)
                CloseHandle(value);
            value = h;
        }
    };

    class ServiceHandle
    {
        SC_HANDLE value = nullptr;

    public:
        explicit ServiceHandle(SC_HANDLE h = nullptr) : value(h) {}
        ~ServiceHandle()
        {
            if (value)
                CloseServiceHandle(value);
        }
        ServiceHandle(const ServiceHandle&) = delete;
        ServiceHandle& operator=(const ServiceHandle&) = delete;
        SC_HANDLE get() const { return value; }
        explicit operator bool() const { return value != nullptr; }
    };

    std::string Utf8(std::wstring_view text);
    std::wstring Wide(std::string_view text);
    std::string Json(std::string_view text);
    std::string Json(std::wstring_view text);
    std::wstring Quote(std::wstring_view text);
    std::wstring TokenSid(HANDLE process = GetCurrentProcess());
    std::wstring SidText(PSID sid);
    bool ValidOwner(std::wstring_view sid);
    std::wstring AccountSid(const std::wstring& name);
    uint64_t ProcessBirth(HANDLE process);
    std::wstring ProcessImage(HANDLE process);
    std::wstring ModulePath();
    std::wstring NewId();
    bool ValidId(std::wstring_view text);
    bool IsAbsoluteLocal(std::wstring_view path);
    std::wstring Parent(const std::wstring& path);
    std::vector<Handle> HoldDirectories(const std::wstring& path);
    void CheckAcl(HANDLE handle, const std::wstring& va, bool privatePath, bool requireProtected = true);
    Handle OpenRead(const std::wstring& path, uint64_t limit = MaxPayload);
    enum class TextObservation
    {
        AppendOnly,
        AtomicallyReplaced
    };
    Handle OpenObservation(const std::wstring& path, TextObservation kind);
    std::vector<BYTE> ReadAll(HANDLE file, uint64_t limit = MaxPayload);
    std::string ReadText(const std::wstring& path);
    std::string ReadObservation(const std::wstring& path, TextObservation kind);
    std::string Hash(HANDLE file);
    std::string HashPath(const std::wstring& path);
    std::string HashBytes(const BYTE* bytes, DWORD count);
    void CopyHeld(HANDLE source, const std::wstring& destination);
    void PublishPair(const std::wstring& sourceDirectory, const std::wstring& codeDirectory);
    void WriteNew(const std::wstring& path, std::string_view content);
    void ReplaceText(const std::wstring& path, std::string_view content);
    void AppendText(const std::wstring& path, std::string_view content);
    bool Exists(const std::wstring& path);
    void MakeDirectory(const std::wstring& path);
    uint64_t ParseVersion(std::string_view text);
    std::string VersionText(uint64_t version);
    uint64_t FileVersion(const std::wstring& path);
    uint64_t OwnVersion();
    std::map<std::string, std::string> ParseKeys(std::string_view text, const std::vector<std::string>& keys);
    constexpr bool IsHash(std::string_view text)
    {
        return text.size() == 64 && std::all_of(text.begin(), text.end(), [](char c) {
                   return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
               });
    }

    struct Paths
    {
        std::wstring owner, service, va, root, state, code, data, policy, installer, pipe;
        explicit Paths(const std::wstring& ownerSid);
        void Check() const;
        Handle Lock() const;
        bool Draining() const;
        bool AutoImportSuppressed() const;
    };
    void ValidatePurgeTombstone(std::string_view text, const std::wstring& owner);

    struct Policy
    {
        std::string signerPolicy;
        uint64_t minimum = 0;
    };
    Policy ParsePolicy(std::string_view text);
    Policy LoadPolicy(const Paths& paths);
    struct Bundle
    {
        std::vector<Handle> directories;
        Handle manifest, signature, bootstrap, runtime, catalog, catalogSignature;
        uint64_t version = 0;
        std::string bootstrapHash, runtimeHash, catalogHash;
    };
    Bundle ValidateBundle(const std::wstring& directory, const Policy& policy, uint64_t current);

    // Value 2 is retired: only the MSI-coordinated transaction protocol may update.
    enum class Command : DWORD
    {
        Status = 1,
        MsiPrepare = 3,
        MsiCommit = 4,
        MsiRollback = 5,
        MsiQuery = 6
    };
    struct Request
    {
        DWORD magic = ProtocolMagic;
        Command command = Command::Status;
        wchar_t operation[40]{};
        wchar_t bundle[4096]{};
    };
    void ValidateRequest(const Request& request);
    DWORD BoundedIo(HANDLE pipe, bool write, void* buffer, DWORD count, HANDLE cancel = nullptr, DWORD timeout = IoTimeout);
    std::string CompleteResponse(HANDLE activation, const std::function<void()>& respond, const std::function<void(std::string_view)>& logFailure);
    std::wstring PipeSddl(const std::wstring& owner, const std::wstring& va);
    std::wstring PipeCaller(HANDLE pipe);
    std::string Call(const Paths& paths, const Request& request, DWORD timeout = IoTimeout);
    std::map<std::string, std::string> ParseMsiResponse(std::string_view response, const std::wstring& operation);
    std::map<std::string, std::string> MsiCall(const Paths& paths, Command command, const std::wstring& operation, const std::wstring& bundle = {});
    void CheckOwnVa(const Paths& paths);
    PACL BuildIdentityQueryAcl(PACL previous, const std::wstring& va, const std::wstring& owner, DWORD access);
    void AllowIdentityQuery(const Paths& paths);

    struct Child
    {
        Handle process, thread;
        DWORD pid = 0;
    };
    Child Spawn(const std::wstring& executable, const std::wstring& arguments, const std::vector<HANDLE>& inherited, DWORD flags = 0);
    Handle InheritableDuplicate(HANDLE source, DWORD access = 0);
    std::wstring HandleText(HANDLE handle);
    HANDLE ParseHandle(std::wstring_view text);
    std::string ErrorJson(const std::exception& error);
    bool IsTerminal(std::string_view journal);
    std::wstring CurrentTransaction(const Paths& paths);
    std::string Journal(const std::wstring& transaction);
    void Record(const std::wstring& transaction, std::string_view phase, std::string_view detail);
    std::string Phase(std::string_view journal);

    struct Transaction
    {
        std::wstring owner, operation;
        uint64_t oldVersion = 0, newVersion = 0;
        std::string oldBootstrapHash, oldRuntimeHash, newBootstrapHash, newRuntimeHash;
        std::string oldCatalogHash, newCatalogHash;
    };
    void WriteTransaction(const std::wstring& directory, const Transaction& transaction);
    Transaction ReadTransaction(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation);
    Handle TransactionGate(const std::wstring& directory);
    void RecordOperation(const std::wstring& directory, std::string_view phase, std::string_view detail);
    std::wstring ResolveMsiOperation(const std::wstring& current, const std::wstring& owner, const std::wstring& operation, Command command);
    std::string AbsentMsiResponse(const std::wstring& operation, uint64_t currentVersion);
    std::string MsiResponse(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation);
    void DecideOperation(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation, Command command);
    Command WaitDecision(const std::wstring& directory, const std::wstring& owner, const std::wstring& operation, DWORD timeout = DecisionTimeout);
    void RequireNewOperation(const std::wstring& current, const std::wstring& owner, const std::wstring& operation);
    void VerifySameVersion(const Bundle& bundle, uint64_t current, const std::string& bootstrapHash, const std::string& runtimeHash);

    struct UpdateActions
    {
        std::function<void()> stop;
        std::function<void()> publishNew;
        std::function<void()> startNew;
        std::function<void()> verifyNew;
        std::function<void()> publishOld;
        std::function<void()> startOld;
        std::function<void()> verifyOld;
        std::function<Command()> decision;
    };
    int RunUpdate(const std::wstring& transaction, const UpdateActions& actions);
}
