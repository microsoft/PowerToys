#pragma once

#include <Windows.h>

#include <functional>
#include <map>
#include <mutex>
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

    // Per-server verification cache. Header-only (all members inline) so it introduces NO out-of-line
    // symbols: the common transport header is also compiled by the Workspaces duplicate transport, which
    // does not link the auth translation unit, so an out-of-line ctor/dtor here would break its link.
    // Each pipe server owns one instance, so cached verdicts are physically partitioned by policy and the
    // key is simply (pid, process-creation-time). Thread-safe.
    class VerificationCache
    {
    public:
        // On a fresh (within-TTL) hit, fills out.accepted/imagePath/reasonCode and returns true.
        bool TryGet(DWORD pid, unsigned long long createTime, AuthResult& out)
        {
            std::scoped_lock lock(m_mutex);
            const auto it = m_map.find(Key{ pid, createTime });
            if (it != m_map.end() && (GetTickCount64() - it->second.tick) <= kTtlMs)
            {
                out.accepted = it->second.accepted;
                out.imagePath = it->second.imagePath;
                out.reasonCode = it->second.reason;
                return true;
            }
            return false;
        }

        // Stores/refreshes the verdict for this process instance (evicts expired/oldest entries first).
        void Put(DWORD pid, unsigned long long createTime, const AuthResult& verdict)
        {
            std::scoped_lock lock(m_mutex);
            evictLocked();
            m_map[Key{ pid, createTime }] =
                Entry{ verdict.accepted, verdict.imagePath, verdict.reasonCode, GetTickCount64() };
        }

    private:
        struct Key
        {
            DWORD pid = 0;
            unsigned long long createTime = 0;
            bool operator<(const Key& other) const
            {
                return pid < other.pid || (pid == other.pid && createTime < other.createTime);
            }
        };

        struct Entry
        {
            bool accepted = false;
            std::wstring imagePath;
            const wchar_t* reason = L"";
            unsigned long long tick = 0;
        };

        void evictLocked()
        {
            const unsigned long long now = GetTickCount64();
            for (auto it = m_map.begin(); it != m_map.end();)
            {
                if (now - it->second.tick > kTtlMs)
                {
                    it = m_map.erase(it);
                }
                else
                {
                    ++it;
                }
            }
            while (m_map.size() >= kCap)
            {
                auto oldest = m_map.begin();
                for (auto it = m_map.begin(); it != m_map.end(); ++it)
                {
                    if (it->second.tick < oldest->second.tick)
                    {
                        oldest = it;
                    }
                }
                m_map.erase(oldest);
            }
        }

        static constexpr unsigned long long kTtlMs = 60'000; // per-process verdict lifetime
        static constexpr size_t kCap = 64;                   // small LRU cap (few legit clients)
        std::mutex m_mutex;
        std::map<Key, Entry> m_map;
    };

    // Authenticates the client connected on `pipe` against `policy`, using `cache` (owned by the caller,
    // typically one per pipe server) to avoid re-verifying every message. Never throws.
    AuthResult AuthenticateClient(HANDLE pipe, const CallerPolicy& policy, VerificationCache& cache);

    // File version packed as (VersionMS << 32) | VersionLS. Returns 0 on failure.
    unsigned long long GetModuleVersion(const std::wstring& path);

    // Version of the current process's own module (e.g. the Runner). Returns 0 on failure.
    unsigned long long GetOwnModuleVersion();
}
