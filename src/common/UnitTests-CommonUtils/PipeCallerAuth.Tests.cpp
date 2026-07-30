#include "pch.h"
#include "TestHelpers.h"

#include <interop/pipe_caller_auth.h>

#include <string>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        std::wstring CurrentExePath()
        {
            wchar_t buf[MAX_PATH * 2] = {};
            GetModuleFileNameW(nullptr, buf, ARRAYSIZE(buf));
            return buf;
        }

        std::wstring DirOf(const std::wstring& p)
        {
            const auto pos = p.find_last_of(L"\\/");
            return pos == std::wstring::npos ? p : p.substr(0, pos);
        }

        std::wstring BaseOf(const std::wstring& p)
        {
            const auto pos = p.find_last_of(L"\\/");
            return pos == std::wstring::npos ? p : p.substr(pos + 1);
        }

        // Sets up a real connected named-pipe pair inside this process so AuthenticateClient can be
        // exercised end-to-end. The "client" is this test host, so its image path / PID are what the
        // policy is matched against.
        struct ConnectedPipe
        {
            HANDLE server = INVALID_HANDLE_VALUE;
            HANDLE client = INVALID_HANDLE_VALUE;
            ~ConnectedPipe()
            {
                if (server != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(server);
                }
                if (client != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(client);
                }
            }
        };

        bool MakeConnectedPipe(ConnectedPipe& out)
        {
            static LONG counter = 0;
            const std::wstring name = L"\\\\.\\pipe\\pt_auth_test_" +
                                      std::to_wstring(GetCurrentProcessId()) + L"_" +
                                      std::to_wstring(GetTickCount64()) + L"_" +
                                      std::to_wstring(InterlockedIncrement(&counter));
            HANDLE server = CreateNamedPipeW(name.c_str(),
                                             PIPE_ACCESS_DUPLEX,
                                             PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                                             1,
                                             4096,
                                             4096,
                                             0,
                                             nullptr);
            if (server == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            HANDLE client = INVALID_HANDLE_VALUE;
            std::thread connectThread([&]() {
                client = CreateFileW(name.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
            });
            const BOOL connected = ConnectNamedPipe(server, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
            connectThread.join();

            if (!connected || client == INVALID_HANDLE_VALUE)
            {
                CloseHandle(server);
                if (client != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(client);
                }
                return false;
            }
            out.server = server;
            out.client = client;
            return true;
        }
    }

    TEST_CLASS(PipeCallerAuthTests)
    {
    public:
        // A disabled policy is a pass-through (preserves the managed start(nullptr) server and tests).
        TEST_METHOD(DisabledPolicy_Accepts)
        {
            interop_auth::CallerPolicy policy;
            interop_auth::VerificationCache cache;
            const auto res = interop_auth::AuthenticateClient(nullptr, policy, cache);
            Assert::IsTrue(res.accepted);
        }

        TEST_METHOD(GetModuleVersion_KnownBinary_NonZero)
        {
            wchar_t sys[MAX_PATH] = {};
            GetSystemDirectoryW(sys, ARRAYSIZE(sys));
            const std::wstring kernel = std::wstring(sys) + L"\\kernel32.dll";
            Assert::IsTrue(interop_auth::GetModuleVersion(kernel) != 0ULL);
        }

        TEST_METHOD(GetModuleVersion_BogusPath_Zero)
        {
            Assert::AreEqual(0ULL, interop_auth::GetModuleVersion(L"Z:\\does\\not\\exist.exe"));
        }

        // Legitimate caller (this test host, matched by its own dir + basename) is accepted.
        TEST_METHOD(EnabledPolicy_MatchingCaller_Accepts)
        {
            ConnectedPipe cp;
            Assert::IsTrue(MakeConnectedPipe(cp), L"failed to set up connected pipe");

            const std::wstring exe = CurrentExePath();
            interop_auth::CallerPolicy policy;
            policy.enabled = true;
            policy.expectedDirectory = DirOf(exe);
            policy.allowedBasenames = { BaseOf(exe) };
            policy.expectedVersion = 0; // skip version match
            policy.requireMicrosoftSignature = false; // test host is not Microsoft-signed

            interop_auth::VerificationCache cache;
            const auto res = interop_auth::AuthenticateClient(cp.server, policy, cache);
            Assert::IsTrue(res.accepted, L"legitimate self caller should be accepted");
            Assert::AreEqual(GetCurrentProcessId(), res.pid);
        }

        // Reproduces the PoC path: a caller whose image is not on the allow-list is rejected with no
        // dispatch, and the required rejection log callback fires.
        TEST_METHOD(EnabledPolicy_WrongBasename_Rejects)
        {
            ConnectedPipe cp;
            Assert::IsTrue(MakeConnectedPipe(cp), L"failed to set up connected pipe");

            const std::wstring exe = CurrentExePath();
            interop_auth::CallerPolicy policy;
            policy.enabled = true;
            policy.expectedDirectory = DirOf(exe);
            policy.allowedBasenames = { L"definitely_not_the_test_host.exe" };
            policy.requireMicrosoftSignature = false;

            bool logged = false;
            policy.logReject = [&](const interop_auth::AuthResult&) { logged = true; };

            interop_auth::VerificationCache cache;
            const auto res = interop_auth::AuthenticateClient(cp.server, policy, cache);
            Assert::IsFalse(res.accepted, L"caller with non-allowlisted basename must be rejected");
            Assert::AreEqual(L"bad-basename", res.reasonCode);
            Assert::IsTrue(logged, L"rejection must invoke the log callback");
        }

        // A caller image outside the expected directory is rejected.
        TEST_METHOD(EnabledPolicy_WrongDirectory_Rejects)
        {
            ConnectedPipe cp;
            Assert::IsTrue(MakeConnectedPipe(cp), L"failed to set up connected pipe");

            const std::wstring exe = CurrentExePath();
            interop_auth::CallerPolicy policy;
            policy.enabled = true;
            policy.expectedDirectory = L"C:\\Windows\\System32"; // not where the test host lives
            policy.allowedBasenames = { BaseOf(exe) };
            policy.requireMicrosoftSignature = false;

            interop_auth::VerificationCache cache;
            const auto res = interop_auth::AuthenticateClient(cp.server, policy, cache);
            Assert::IsFalse(res.accepted);
            Assert::AreEqual(L"bad-directory", res.reasonCode);
        }

        // Each pipe server owns its own cache, so the same client process is evaluated independently
        // per policy — an accept verdict in one server's cache never bleeds into another server that
        // has a different (stricter) policy.
        TEST_METHOD(SeparateCaches_AreIndependent)
        {
            const std::wstring exe = CurrentExePath();

            interop_auth::CallerPolicy acceptPolicy;
            acceptPolicy.enabled = true;
            acceptPolicy.expectedDirectory = DirOf(exe);
            acceptPolicy.allowedBasenames = { BaseOf(exe) };
            acceptPolicy.requireMicrosoftSignature = false;

            interop_auth::CallerPolicy rejectPolicy = acceptPolicy;
            rejectPolicy.allowedBasenames = { L"not_the_test_host.exe" };

            interop_auth::VerificationCache cacheA; // e.g. the Settings server's cache
            interop_auth::VerificationCache cacheB; // e.g. the Quick Access server's cache

            ConnectedPipe cp1;
            Assert::IsTrue(MakeConnectedPipe(cp1), L"failed to set up connected pipe 1");
            const auto rA = interop_auth::AuthenticateClient(cp1.server, acceptPolicy, cacheA);
            Assert::IsTrue(rA.accepted, L"self caller accepted under the permissive policy");

            // Same client process (same pid + creation time), different server/cache/policy.
            ConnectedPipe cp2;
            Assert::IsTrue(MakeConnectedPipe(cp2), L"failed to set up connected pipe 2");
            const auto rB = interop_auth::AuthenticateClient(cp2.server, rejectPolicy, cacheB);
            Assert::IsFalse(rB.accepted, L"a separate server cache must not inherit the other's accept verdict");
            Assert::AreEqual(L"bad-basename", rB.reasonCode);
        }
    };
}
