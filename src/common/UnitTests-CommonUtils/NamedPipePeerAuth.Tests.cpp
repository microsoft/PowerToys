#include "pch.h"

#include <common/utils/named_pipe_peer_auth.h>
#include <common/utils/process_path.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CommonUtilsUnitTests
{
    TEST_CLASS (NamedPipePeerAuthTests)
    {
    private:
        struct ConnectedPipe
        {
            HANDLE server = INVALID_HANDLE_VALUE;
            HANDLE client = INVALID_HANDLE_VALUE;

            ~ConnectedPipe()
            {
                if (client != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(client);
                }
                if (server != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(server);
                }
            }
        };

        static ConnectedPipe create_connected_pipe()
        {
            const auto pipeName = std::format(
                L"\\\\.\\pipe\\powertoys_peer_auth_test_{}_{}",
                GetCurrentProcessId(),
                GetTickCount64());

            ConnectedPipe pipe;
            pipe.server = CreateNamedPipeW(
                pipeName.c_str(),
                PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                1,
                1024,
                1024,
                0,
                nullptr);
            if (pipe.server == INVALID_HANDLE_VALUE)
            {
                return pipe;
            }

            pipe.client = CreateFileW(
                pipeName.c_str(),
                GENERIC_READ | GENERIC_WRITE,
                0,
                nullptr,
                OPEN_EXISTING,
                0,
                nullptr);
            if (pipe.client == INVALID_HANDLE_VALUE)
            {
                return pipe;
            }

            if (!ConnectNamedPipe(pipe.server, nullptr) &&
                GetLastError() != ERROR_PIPE_CONNECTED)
            {
                CloseHandle(pipe.client);
                pipe.client = INVALID_HANDLE_VALUE;
            }
            return pipe;
        }

        static named_pipe_peer_auth::Policy self_policy()
        {
            const auto executablePath = get_module_filename(nullptr);
            return {
                std::filesystem::path(executablePath).filename().wstring(),
                std::filesystem::path(executablePath).parent_path().wstring(),
                executablePath,
                named_pipe_peer_auth::Validation::PowerToysPeer,
            };
        }

    public:
        TEST_METHOD (PowerToysPeer_AcceptsExpectedClientAndServer)
        {
            const auto pipe = create_connected_pipe();
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.server);
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.client);

#ifdef _DEBUG
            const auto policy = self_policy();
            Assert::IsTrue(named_pipe_peer_auth::authenticate(
                pipe.server,
                named_pipe_peer_auth::Peer::Client,
                policy));
            Assert::IsTrue(named_pipe_peer_auth::authenticate(
                pipe.client,
                named_pipe_peer_auth::Peer::Server,
                policy));
#endif
        }

        TEST_METHOD (PowerToysPeer_RejectsUnexpectedProcessName)
        {
            const auto pipe = create_connected_pipe();
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.server);
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.client);

            auto policy = self_policy();
            policy.expectedProcessName = L"not-the-test-process.exe";
            Assert::IsFalse(named_pipe_peer_auth::authenticate(
                pipe.server,
                named_pipe_peer_auth::Peer::Client,
                policy));
            Assert::IsFalse(named_pipe_peer_auth::authenticate(
                pipe.client,
                named_pipe_peer_auth::Peer::Server,
                policy));
        }

        TEST_METHOD (TrustedSignedProcess_AcceptsResolvedPeerInDebug)
        {
            const auto pipe = create_connected_pipe();
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.server);
            Assert::AreNotEqual(INVALID_HANDLE_VALUE, pipe.client);

#ifdef _DEBUG
            const named_pipe_peer_auth::Policy policy{
                {},
                {},
                {},
                named_pipe_peer_auth::Validation::TrustedSignedProcess,
            };
            Assert::IsTrue(named_pipe_peer_auth::authenticate(
                pipe.server,
                named_pipe_peer_auth::Peer::Client,
                policy));
#endif
        }
    };
}
