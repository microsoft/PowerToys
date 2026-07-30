#pragma once

#include <Windows.h>

#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <vector>

// Per-connection authentication of a named-pipe client for the Runner control channel.
//
// The Runner is the *server* for the privileged Settings/Quick Access command pipes. Because a
// same-user attacker shares the connecting user's SID, integrity level, and logon session, the pipe
// DACL cannot distinguish the legitimate Settings/Quick Access child from an attacker. The only usable
// discriminator is the *binary identity* of the connecting process, so we authenticate it before any
// message is dispatched (fail-closed). See the design doc for the full rationale.
namespace interop_auth
{
    struct AuthResult
    {
        bool accepted = false;
        DWORD pid = 0;
        std::wstring imagePath;      // canonical image path of the connecting process (for logging)
        const wchar_t* reasonCode = L""; // static string; safe to copy/store
    };

    struct CallerPolicy
    {
        // When false the gate is a no-op (preserves the managed start(nullptr) server and tests).
        bool enabled = false;

        // Optional exact-PID pin (v1: unset — off).
        std::optional<DWORD> expectedClientPid;

        // Canonical directory the caller image must live under (runner-relative, e.g.
        // <module folder>\WinUI3Apps). Derived at runtime, not hardcoded, so it adapts to Debug/Release.
        std::wstring expectedDirectory;

        // Allowed image basenames, e.g. { L"PowerToys.Settings.exe" }.
        std::vector<std::wstring> allowedBasenames;

        // Runner's own file version; caller must match exactly (anti-downgrade). 0 disables the check.
        unsigned long long expectedVersion = 0;

        // Require a machine-root-anchored Microsoft Authenticode signature. Compiled out in Debug builds
        // (local binaries are unsigned) while directory/basename/version stay enforced.
        bool requireMicrosoftSignature = true;

        // Optional sink invoked once per rejected process instance (deduped via the per-process cache).
        // The Runner supplies a lambda that logs via its own Logger; interop itself has no logger.
        std::function<void(const AuthResult&)> logReject;
    };

    // Per-server verification cache. Each pipe server owns one instance, so cached verdicts are
    // physically partitioned by policy and the key is simply (pid, process-creation-time) — there is no
    // shared cross-server state and therefore no need to fold the policy into the cache key. Opaque
    // (pimpl) so the cache internals stay in the .cpp. Thread-safe.
    class VerificationCache
    {
    public:
        VerificationCache();
        ~VerificationCache();
        VerificationCache(const VerificationCache&) = delete;
        VerificationCache& operator=(const VerificationCache&) = delete;

        // On a fresh (within-TTL) hit, fills out.accepted/imagePath/reasonCode and returns true.
        bool TryGet(DWORD pid, unsigned long long createTime, AuthResult& out);
        // Stores/refreshes the verdict for this process instance (evicts expired/oldest entries).
        void Put(DWORD pid, unsigned long long createTime, const AuthResult& verdict);

    private:
        struct Impl;
        std::unique_ptr<Impl> impl;
    };

    // Authenticates the client connected on `pipe` against `policy`, using `cache` (owned by the caller,
    // typically one per pipe server) to avoid re-verifying every message. Never throws.
    AuthResult AuthenticateClient(HANDLE pipe, const CallerPolicy& policy, VerificationCache& cache);

    // File version packed as (VersionMS << 32) | VersionLS. Returns 0 on failure.
    unsigned long long GetModuleVersion(const std::wstring& path);

    // Version of the current process's own module (e.g. the Runner). Returns 0 on failure.
    unsigned long long GetOwnModuleVersion();
}
