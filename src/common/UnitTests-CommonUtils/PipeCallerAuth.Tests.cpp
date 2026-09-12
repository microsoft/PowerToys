#include "pch.h"
#include "TestHelpers.h"

#include <interop/pipe_caller_auth.h>
#include <interop/pipe_caller_auth_chain_policy.h>

#include <array>
#include <ncrypt.h>
#include <string>
#include <thread>
#include <vector>
#include <wil/resource.h>

#pragma comment(lib, "ncrypt.lib")

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        struct PolicyChainFixture
        {
            PolicyChainFixture()
            {
                wil::unique_ncrypt_prov provider;
                wil::unique_ncrypt_key key;
                Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptOpenStorageProvider(provider.put(), MS_KEY_STORAGE_PROVIDER, 0));
                Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptCreatePersistedKey(provider.get(), key.put(), NCRYPT_RSA_ALGORITHM, nullptr, 0, 0));
                Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptFinalizeKey(key.get(), NCRYPT_SILENT_FLAG));

                DWORD nameSize = 0;
                constexpr auto subjectName = L"CN=PowerToys chain policy test";
                Assert::IsTrue(CertStrToNameW(X509_ASN_ENCODING, subjectName, CERT_X500_NAME_STR, nullptr, nullptr, &nameSize, nullptr) != FALSE);
                std::vector<BYTE> name(nameSize);
                Assert::IsTrue(CertStrToNameW(X509_ASN_ENCODING, subjectName, CERT_X500_NAME_STR, nullptr, name.data(), &nameSize, nullptr) != FALSE);
                CERT_NAME_BLOB subject{ nameSize, name.data() };
                char algorithmOid[] = szOID_RSA_SHA256RSA;
                CRYPT_ALGORITHM_IDENTIFIER algorithm{ algorithmOid, {} };
                m_certificate.reset(CertCreateSelfSignCertificate(key.get(), &subject, CERT_CREATE_SELFSIGN_NO_KEY_INFO, nullptr, &algorithm, nullptr, nullptr, nullptr));
                Assert::IsTrue(static_cast<bool>(m_certificate));

                // Policy consumes precomputed trust statuses. This fixture models those
                // statuses with an ephemeral certificate; it never installs a trusted root.
                for (size_t index = 0; index < m_elements.size(); ++index)
                {
                    m_elements[index].cbSize = sizeof(CERT_CHAIN_ELEMENT);
                    m_elements[index].pCertContext = m_certificate.get();
                    m_revocation[index].cbSize = sizeof(CERT_REVOCATION_INFO);
                    m_elements[index].pRevocationInfo = &m_revocation[index];
                    m_elementPointers[index] = &m_elements[index];
                }
                m_elements.back().TrustStatus.dwInfoStatus = CERT_TRUST_IS_SELF_SIGNED;
                m_simple.cbSize = sizeof(m_simple);
                m_simple.cElement = static_cast<DWORD>(m_elements.size());
                m_simple.rgpElement = m_elementPointers.data();
                m_simplePointer = &m_simple;
                chain.cbSize = sizeof(chain);
                chain.cChain = 1;
                chain.rgpChain = &m_simplePointer;
            }

            void SetErrors(size_t index, DWORD errors)
            {
                for (auto& element : m_elements)
                {
                    element.TrustStatus.dwErrorStatus = 0;
                }
                for (auto& revocation : m_revocation)
                {
                    revocation.dwRevocationResult = 0;
                }
                m_elements[index].TrustStatus.dwErrorStatus = errors;
                m_revocation[index].dwRevocationResult = errors & CERT_TRUST_IS_REVOKED ? CRYPT_E_REVOKED : CRYPT_E_REVOCATION_OFFLINE;
                m_simple.TrustStatus.dwErrorStatus = errors;
                chain.TrustStatus.dwErrorStatus = errors;
            }

            DWORD PolicyError(DWORD flags) const
            {
                CERT_CHAIN_POLICY_PARA parameters{};
                parameters.cbSize = sizeof(parameters);
                parameters.dwFlags = flags;
                CERT_CHAIN_POLICY_STATUS status{};
                status.cbSize = sizeof(status);
                Assert::IsTrue(CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_AUTHENTICODE, &chain, &parameters, &status) != FALSE);
                return status.dwError;
            }

            CERT_CHAIN_CONTEXT chain{};

        private:
            wil::unique_cert_context m_certificate;
            std::array<CERT_CHAIN_ELEMENT, 3> m_elements{};
            std::array<CERT_REVOCATION_INFO, 3> m_revocation{};
            std::array<PCERT_CHAIN_ELEMENT, 3> m_elementPointers{};
            CERT_SIMPLE_CHAIN m_simple{};
            PCERT_SIMPLE_CHAIN m_simplePointer{};
        };

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
        TEST_METHOD(MachineSignerPolicy_AcceptsCleanChain)
        {
            PolicyChainFixture fixture;
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), fixture.PolicyError(0));
            Assert::IsTrue(interop_auth::details::VerifyMachineSignerChainPolicy(&fixture.chain));
        }

        TEST_METHOD(MachineSignerPolicy_AcceptsUnavailableRevocationAtEachChainLevel)
        {
            PolicyChainFixture fixture;
            for (size_t level = 0; level < 3; ++level)
            {
                for (DWORD errors : { CERT_TRUST_REVOCATION_STATUS_UNKNOWN, CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION })
                {
                    fixture.SetErrors(level, errors);
                    if (level == 0)
                    {
                        Assert::AreNotEqual(static_cast<DWORD>(ERROR_SUCCESS), fixture.PolicyError(0));
                    }
                    Assert::IsTrue(interop_auth::details::VerifyMachineSignerChainPolicy(&fixture.chain));
                }
            }
        }

        TEST_METHOD(MachineSignerPolicy_StillRejectsKnownRevocation)
        {
            PolicyChainFixture fixture;
            for (size_t level = 0; level < 3; ++level)
            {
                for (DWORD errors : { CERT_TRUST_IS_REVOKED, CERT_TRUST_IS_REVOKED | CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION })
                {
                    fixture.SetErrors(level, errors);
                    Assert::AreNotEqual(static_cast<DWORD>(ERROR_SUCCESS), fixture.PolicyError(CERT_CHAIN_POLICY_IGNORE_ALL_REV_UNKNOWN_FLAGS));
                    Assert::IsFalse(interop_auth::details::VerifyMachineSignerChainPolicy(&fixture.chain));
                }
            }
        }

        TEST_METHOD(MachineSignerPolicy_StillRejectsOtherTrustFailures)
        {
            PolicyChainFixture fixture;
            for (DWORD error : { CERT_TRUST_IS_UNTRUSTED_ROOT, CERT_TRUST_IS_PARTIAL_CHAIN, CERT_TRUST_IS_EXPLICIT_DISTRUST,
                                 CERT_TRUST_IS_NOT_SIGNATURE_VALID, CERT_TRUST_IS_NOT_TIME_VALID, CERT_TRUST_IS_NOT_VALID_FOR_USAGE,
                                 CERT_TRUST_INVALID_BASIC_CONSTRAINTS })
            {
                fixture.SetErrors(0, error | CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION);
                Assert::IsFalse(interop_auth::details::VerifyMachineSignerChainPolicy(&fixture.chain));
            }
            Assert::IsFalse(interop_auth::details::VerifyMachineSignerChainPolicy(nullptr));
        }

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
