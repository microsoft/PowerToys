// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <WorkspacesLib/LauncherIpcServer.h>

#include <algorithm>
#include <array>
#include <cstring>
#include <filesystem>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        constexpr DWORD OperationTimeoutMs = 3000;
        constexpr DWORD StopTimeoutMs = 2000;
        constexpr DWORD FrameTimeoutWaitMs = 8000;

        DWORD Remaining(ULONGLONG deadline)
        {
            const auto now = GetTickCount64();
            return now >= deadline ? 0 : static_cast<DWORD>(deadline - now);
        }

        constexpr std::array<BYTE, sizeof(DWORD)> EncodeLength(DWORD length)
        {
            return { static_cast<BYTE>(length),
                     static_cast<BYTE>(length >> 8),
                     static_cast<BYTE>(length >> 16),
                     static_cast<BYTE>(length >> 24) };
        }

        std::vector<BYTE> MakeFrame(const std::string& payload)
        {
            const auto header = EncodeLength(static_cast<DWORD>(payload.size()));
            std::vector<BYTE> frame(header.begin(), header.end());
            frame.insert(frame.end(), payload.begin(), payload.end());
            return frame;
        }

        interop_auth::CallerPolicy CurrentProcessPolicy()
        {
            std::wstring image(32768, L'\0');
            DWORD length = static_cast<DWORD>(image.size());
            Assert::IsTrue(QueryFullProcessImageNameW(GetCurrentProcess(), 0, image.data(), &length) != FALSE);
            image.resize(length);
            const std::filesystem::path path(image);

            interop_auth::CallerPolicy policy;
            policy.enabled = true;
            policy.expectedClientPid = GetCurrentProcessId();
            policy.expectedDirectory = path.parent_path().wstring();
            policy.allowedBasenames = { path.filename().wstring() };
            policy.expectedVersion = interop_auth::GetModuleVersion(image);
            // The testhost need not be Microsoft-signed in either Debug or Release.
            policy.requireMicrosoftSignature = false;
            return policy;
        }

        struct CallbackState
        {
            wil::unique_event received{ CreateEventW(nullptr, FALSE, FALSE, nullptr) };
            wil::unique_event failed{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            wil::unique_event rejected{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            std::mutex mutex;
            std::vector<std::wstring> messages;
            std::vector<LauncherIpcFailure> failures;
            std::vector<interop_auth::AuthResult> rejections;

            CallbackState()
            {
                Assert::IsTrue(received && failed && rejected);
            }

            void Receive(const std::wstring& message)
            {
                {
                    std::scoped_lock lock(mutex);
                    messages.push_back(message);
                }
                SetEvent(received.get());
            }

            void Fail(LauncherIpcFailure failure)
            {
                {
                    std::scoped_lock lock(mutex);
                    failures.push_back(failure);
                }
                SetEvent(failed.get());
            }

            void Reject(const interop_auth::AuthResult& rejection)
            {
                {
                    std::scoped_lock lock(mutex);
                    rejections.push_back(rejection);
                }
                SetEvent(rejected.get());
            }

            std::vector<std::wstring> Messages()
            {
                std::scoped_lock lock(mutex);
                return messages;
            }

            std::vector<LauncherIpcFailure> Failures()
            {
                std::scoped_lock lock(mutex);
                return failures;
            }

            std::vector<interop_auth::AuthResult> Rejections()
            {
                std::scoped_lock lock(mutex);
                return rejections;
            }

            bool WaitForMessages(size_t count)
            {
                const auto deadline = GetTickCount64() + OperationTimeoutMs;
                const HANDLE events[] = { failed.get(), received.get() };
                for (;;)
                {
                    {
                        std::scoped_lock lock(mutex);
                        if (messages.size() >= count)
                        {
                            return true;
                        }
                        if (!failures.empty())
                        {
                            return false;
                        }
                    }
                    const auto timeout = Remaining(deadline);
                    if (timeout == 0 || WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, timeout) != WAIT_OBJECT_0 + 1)
                    {
                        return false;
                    }
                }
            }
        };

        class PipeClient
        {
            struct Connection
            {
                wil::unique_hfile pipe;
                PTP_IO io = nullptr;

                ~Connection()
                {
                    if (io)
                    {
                        CloseThreadpoolIo(io);
                    }
                }
            };

            struct Operation : OVERLAPPED
            {
                std::shared_ptr<Connection> connection;
                wil::unique_event completed{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
                std::vector<BYTE> buffer;
                std::shared_ptr<Operation> pending;
                DWORD error = ERROR_IO_PENDING;
                DWORD transferred = 0;

                Operation() :
                    OVERLAPPED{}
                {
                }
            };

            std::shared_ptr<Connection> m_connection;

            static void CALLBACK CompleteIo(PTP_CALLBACK_INSTANCE, void*, void* overlapped, ULONG result, ULONG_PTR transferred, PTP_IO)
            {
                auto* operation = static_cast<Operation*>(static_cast<OVERLAPPED*>(overlapped));
                auto owner = std::move(operation->pending);
                owner->error = result;
                owner->transferred = static_cast<DWORD>(transferred);
                SetEvent(owner->completed.get());
            }

            DWORD Transfer(const BYTE* outgoing, BYTE* incoming, DWORD size, DWORD& transferred, DWORD timeout)
            {
                transferred = 0;
                if (!m_connection)
                {
                    return ERROR_INVALID_HANDLE;
                }

                auto operation = std::make_shared<Operation>();
                if (!operation->completed)
                {
                    return GetLastError();
                }
                operation->connection = m_connection;
                operation->buffer.resize(size);
                const bool write = outgoing != nullptr;
                if (write)
                {
                    memcpy(operation->buffer.data(), outgoing, size);
                }

                // Completion owns the OVERLAPPED, buffer and pipe even if a timed-out caller
                // has already cancelled and left. No cancellation drain can hang the testhost.
                operation->pending = operation;
                StartThreadpoolIo(m_connection->io);
                const auto succeeded = write ?
                                           WriteFile(m_connection->pipe.get(), operation->buffer.data(), size, nullptr, operation.get()) :
                                           ReadFile(m_connection->pipe.get(), operation->buffer.data(), size, nullptr, operation.get());
                const auto error = succeeded ? ERROR_SUCCESS : GetLastError();
                if (!succeeded && error != ERROR_IO_PENDING)
                {
                    CancelThreadpoolIo(m_connection->io);
                    operation->pending.reset();
                    return error;
                }

                const auto wait = WaitForSingleObject(operation->completed.get(), timeout);
                if (wait != WAIT_OBJECT_0)
                {
                    const auto waitError = wait == WAIT_TIMEOUT ? ERROR_TIMEOUT : GetLastError();
                    CancelIoEx(m_connection->pipe.get(), operation.get());
                    return waitError;
                }

                transferred = operation->transferred;
                if (!write && operation->error == ERROR_SUCCESS)
                {
                    memcpy(incoming, operation->buffer.data(), transferred);
                }
                return operation->error;
            }

        public:
            PipeClient() = default;
            PipeClient(const PipeClient&) = delete;
            PipeClient& operator=(const PipeClient&) = delete;

            ~PipeClient()
            {
                Close();
            }

            bool Open(const std::wstring& name, DWORD& error)
            {
                Close();
                auto connection = std::make_shared<Connection>();
                const auto path = L"\\\\.\\pipe\\" + name;
                connection->pipe.reset(CreateFileW(path.c_str(),
                                                   GENERIC_READ | GENERIC_WRITE,
                                                   0,
                                                   nullptr,
                                                   OPEN_EXISTING,
                                                   FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION,
                                                   nullptr));
                if (!connection->pipe)
                {
                    error = GetLastError();
                    return false;
                }
                connection->io = CreateThreadpoolIo(connection->pipe.get(), CompleteIo, nullptr, nullptr);
                if (!connection->io)
                {
                    error = GetLastError();
                    return false;
                }
                m_connection = std::move(connection);
                error = ERROR_SUCCESS;
                return true;
            }

            void Close()
            {
                if (m_connection)
                {
                    CancelIoEx(m_connection->pipe.get(), nullptr);
                    m_connection.reset();
                }
            }

            DWORD Write(const BYTE* buffer, DWORD size)
            {
                const auto deadline = GetTickCount64() + OperationTimeoutMs;
                DWORD offset = 0;
                while (offset < size)
                {
                    DWORD transferred = 0;
                    const auto error = Transfer(buffer + offset, nullptr, size - offset, transferred, Remaining(deadline));
                    if (error != ERROR_SUCCESS || transferred == 0)
                    {
                        return error == ERROR_SUCCESS ? ERROR_NO_DATA : error;
                    }
                    offset += transferred;
                }
                return ERROR_SUCCESS;
            }

            DWORD Write(const std::vector<BYTE>& bytes)
            {
                return Write(bytes.data(), static_cast<DWORD>(bytes.size()));
            }

            DWORD Read(BYTE* buffer, DWORD size)
            {
                const auto deadline = GetTickCount64() + OperationTimeoutMs;
                DWORD offset = 0;
                while (offset < size)
                {
                    DWORD transferred = 0;
                    const auto error = Transfer(nullptr, buffer + offset, size - offset, transferred, Remaining(deadline));
                    if (error != ERROR_SUCCESS || transferred == 0)
                    {
                        return error == ERROR_SUCCESS ? ERROR_BROKEN_PIPE : error;
                    }
                    offset += transferred;
                }
                return ERROR_SUCCESS;
            }

            std::string ReadFrame()
            {
                std::array<BYTE, sizeof(DWORD)> header{};
                Assert::AreEqual<DWORD>(ERROR_SUCCESS, Read(header.data(), static_cast<DWORD>(header.size())));
                const DWORD length = static_cast<DWORD>(header[0]) |
                                     (static_cast<DWORD>(header[1]) << 8) |
                                     (static_cast<DWORD>(header[2]) << 16) |
                                     (static_cast<DWORD>(header[3]) << 24);
                Assert::IsTrue(length > 0 && length <= LauncherIpcServer::MaxMessageBytes);
                std::vector<BYTE> payload(length);
                Assert::AreEqual<DWORD>(ERROR_SUCCESS, Read(payload.data(), length));
                return std::string(payload.begin(), payload.end());
            }
        };

        struct StopState
        {
            std::shared_ptr<LauncherIpcServer> server;
            wil::unique_event completed{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            PTP_WORK work = nullptr;
            std::shared_ptr<StopState> pending;

            ~StopState()
            {
                if (work)
                {
                    CloseThreadpoolWork(work);
                }
            }

            static void CALLBACK StopServer(PTP_CALLBACK_INSTANCE instance, void* context, PTP_WORK)
            {
                auto state = std::move(static_cast<StopState*>(context)->pending);
                CallbackMayRunLong(instance);
                state->server->Stop();
                SetEvent(state->completed.get());
            }
        };

        class ServerSession
        {
            std::shared_ptr<StopState> m_stop = std::make_shared<StopState>();
            bool m_stopSubmitted = false;

        public:
            std::shared_ptr<CallbackState> callbacks = std::make_shared<CallbackState>();
            std::shared_ptr<LauncherIpcServer> server = std::make_shared<LauncherIpcServer>(
                [state = callbacks](const std::wstring& message) { state->Receive(message); },
                [state = callbacks](LauncherIpcFailure failure) { state->Fail(failure); });
            PipeClient client;

            ServerSession()
            {
                m_stop->server = server;
                Assert::IsTrue(static_cast<bool>(m_stop->completed));
                m_stop->work = CreateThreadpoolWork(StopState::StopServer, m_stop.get(), nullptr);
                Assert::IsTrue(m_stop->work != nullptr);
                Assert::IsTrue(server->Initialize());
            }

            ~ServerSession()
            {
                client.Close();
                Stop();
            }

            void OpenClient()
            {
                DWORD error = ERROR_SUCCESS;
                Assert::IsTrue(client.Open(server->Name(), error), L"Could not open the initialized local pipe.");
            }

            void Start(interop_auth::CallerPolicy policy)
            {
                policy.logReject = [state = callbacks](const interop_auth::AuthResult& result) { state->Reject(result); };
                wil::unique_handle peer;
                Assert::IsTrue(DuplicateHandle(GetCurrentProcess(), GetCurrentProcess(), GetCurrentProcess(), peer.put(), 0, FALSE, DUPLICATE_SAME_ACCESS) != FALSE);
                Assert::IsTrue(server->Start(peer.get(), policy));
                // Closing this handle also verifies that Start owns a separate process handle.
            }

            void Connect(bool beforeStart = false)
            {
                if (beforeStart)
                {
                    OpenClient();
                }
                Start(CurrentProcessPolicy());
                if (!beforeStart)
                {
                    OpenClient();
                }

                const auto deadline = GetTickCount64() + OperationTimeoutMs;
                while (!server->IsConnected() && Remaining(deadline) != 0)
                {
                    const auto wait = WaitForSingleObject(callbacks->failed.get(), (std::min)(Remaining(deadline), DWORD{ 10 }));
                    if (wait != WAIT_TIMEOUT)
                    {
                        break;
                    }
                }
                Assert::IsTrue(server->IsConnected(), L"The current-process pipe client was not authenticated.");
            }

            bool Stop()
            {
                if (!m_stopSubmitted)
                {
                    // Preallocated work makes teardown bounded without a future/thread destructor
                    // joining a regressed server. Its callback retains all captured state.
                    m_stop->pending = m_stop;
                    m_stopSubmitted = true;
                    SubmitThreadpoolWork(m_stop->work);
                }
                return WaitForSingleObject(m_stop->completed.get(), StopTimeoutMs) == WAIT_OBJECT_0;
            }

            void ExpectFailure(LauncherIpcFailure expected, DWORD timeout = OperationTimeoutMs)
            {
                Assert::AreEqual<DWORD>(WAIT_OBJECT_0, WaitForSingleObject(callbacks->failed.get(), timeout));
                Assert::IsTrue(Stop(), L"Stopping the failed pipe reader timed out.");
                const auto failures = callbacks->Failures();
                Assert::AreEqual<size_t>(1, failures.size());
                Assert::IsTrue(failures.front() == expected);
                Assert::IsFalse(server->IsConnected());
                Assert::IsFalse(server->Send(L"{\"after\":\"failure\"}"));
            }
        };

        void ExpectInvalidFrame(const std::vector<BYTE>& frame)
        {
            ServerSession session;
            session.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(frame));
            session.ExpectFailure(LauncherIpcFailure::InvalidMessage);
            Assert::IsTrue(session.callbacks->Messages().empty());
        }

        void ExpectDisconnectedFrame(const std::vector<BYTE>& prefix)
        {
            ServerSession session;
            session.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(prefix));
            session.client.Close();
            session.ExpectFailure(LauncherIpcFailure::Disconnected);
            Assert::IsTrue(session.callbacks->Messages().empty());
        }

        void ExpectTimedOutFrame(const std::vector<BYTE>& prefix)
        {
            ServerSession session;
            session.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(prefix));
            session.ExpectFailure(LauncherIpcFailure::Timeout, FrameTimeoutWaitMs);
            Assert::IsTrue(session.callbacks->Messages().empty());
        }
    }

    TEST_CLASS (LauncherIpcServerTests)
    {
    public:
        TEST_METHOD (AlreadyConnectedClientCanExchangeFrames)
        {
            ServerSession session;
            session.Connect(true);
            const std::string request = "{\"connected\":\"before-start\"}";
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame(request)));
            Assert::IsTrue(session.callbacks->WaitForMessages(1));
            Assert::IsTrue(session.server->Send(L"{\"reply\":1}"));
            Assert::AreEqual(std::string("{\"reply\":1}"), session.client.ReadFrame());
            Assert::IsTrue(session.Stop());
            const auto messages = session.callbacks->Messages();
            Assert::AreEqual<size_t>(1, messages.size());
            Assert::AreEqual(std::wstring(L"{\"connected\":\"before-start\"}"), messages.front());
            Assert::IsTrue(session.callbacks->Failures().empty());
        }

        TEST_METHOD (PersistentConnectionTransfersUtf8FramesBothWays)
        {
            ServerSession session;
            session.Connect();
            const std::array<std::pair<std::string, std::wstring>, 3> messages{
                std::make_pair("{\"sequence\":1}", L"{\"sequence\":1}"),
                std::make_pair("{\"text\":\"caf\xc3\xa9 \xf0\x9f\x9a\x80\"}", L"{\"text\":\"caf\u00e9 \U0001f680\"}"),
                std::make_pair("{\"sequence\":3}", L"{\"sequence\":3}")
            };
            for (size_t index = 0; index < messages.size(); ++index)
            {
                Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame(messages[index].first)));
                Assert::IsTrue(session.callbacks->WaitForMessages(index + 1));
                Assert::IsTrue(session.server->Send(messages[index].second));
                Assert::AreEqual(messages[index].first, session.client.ReadFrame());
                Assert::IsTrue(session.server->IsConnected());
            }
            Assert::IsTrue(session.Stop());
            const auto received = session.callbacks->Messages();
            Assert::AreEqual(messages.size(), received.size());
            for (size_t index = 0; index < messages.size(); ++index)
            {
                Assert::AreEqual(messages[index].second, received[index]);
            }
            Assert::IsTrue(session.callbacks->Failures().empty());
        }

        TEST_METHOD (SplitHeaderAndUtf8PayloadAreReassembled)
        {
            ServerSession session;
            session.Connect();
            const auto frame = MakeFrame("{\"text\":\"\xf0\x9f\x9a\x80\"}");
            DWORD offset = 0;
            // The last prefix ends after the leading byte of a four-byte UTF-8 character.
            for (const DWORD count : { 1u, 2u, 11u })
            {
                Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(frame.data() + offset, count));
                offset += count;
                Assert::AreEqual<DWORD>(WAIT_TIMEOUT, WaitForSingleObject(session.callbacks->received.get(), 25));
                Assert::IsTrue(session.callbacks->Messages().empty());
            }
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(frame.data() + offset, static_cast<DWORD>(frame.size()) - offset));
            Assert::IsTrue(session.callbacks->WaitForMessages(1));
            Assert::IsTrue(session.Stop());
            const auto received = session.callbacks->Messages();
            Assert::AreEqual<size_t>(1, received.size());
            Assert::AreEqual(std::wstring(L"{\"text\":\"\U0001f680\"}"), received.front());
            Assert::IsTrue(session.callbacks->Failures().empty());
        }

        TEST_METHOD (CoalescedFramesRemainDistinct)
        {
            ServerSession session;
            session.Connect();
            std::vector<BYTE> bytes;
            for (const auto payload : { "{\"sequence\":1}", "{\"sequence\":2}", "{\"sequence\":3}" })
            {
                const auto frame = MakeFrame(payload);
                bytes.insert(bytes.end(), frame.begin(), frame.end());
            }
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(bytes));
            Assert::IsTrue(session.callbacks->WaitForMessages(3));
            Assert::IsTrue(session.Stop());
            const auto received = session.callbacks->Messages();
            Assert::AreEqual<size_t>(3, received.size());
            Assert::AreEqual(std::wstring(L"{\"sequence\":1}"), received[0]);
            Assert::AreEqual(std::wstring(L"{\"sequence\":2}"), received[1]);
            Assert::AreEqual(std::wstring(L"{\"sequence\":3}"), received[2]);
            Assert::IsTrue(session.callbacks->Failures().empty());
        }

        TEST_METHOD (MaximumLengthFrameIsAccepted)
        {
            ServerSession session;
            session.Connect();
            std::string payload(LauncherIpcServer::MaxMessageBytes, 'a');
            payload.front() = '"';
            payload.back() = '"';
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame(payload)));
            Assert::IsTrue(session.callbacks->WaitForMessages(1));
            Assert::IsTrue(session.Stop());
            const auto received = session.callbacks->Messages();
            Assert::AreEqual<size_t>(1, received.size());
            Assert::IsTrue(std::wstring(payload.begin(), payload.end()) == received.front(), L"Maximum-length frame content changed.");
            Assert::IsTrue(session.callbacks->Failures().empty());
        }

        TEST_METHOD (ZeroLengthFrameIsRejected)
        {
            const auto header = EncodeLength(0);
            ExpectInvalidFrame(std::vector<BYTE>(header.begin(), header.end()));
        }

        TEST_METHOD (OversizedFrameIsRejectedWithoutWaitingForPayload)
        {
            const auto header = EncodeLength(LauncherIpcServer::MaxMessageBytes + 1);
            ExpectInvalidFrame(std::vector<BYTE>(header.begin(), header.end()));
        }

        TEST_METHOD (MalformedUtf8FramesAreRejected)
        {
            for (const auto invalid : { "\xc0\xaf", "\xe2\x82", "\x80", "\xed\xa0\x80" })
            {
                ExpectInvalidFrame(MakeFrame(std::string("{\"text\":\"") + invalid + "\"}"));
            }
        }

        TEST_METHOD (RawNulInFrameIsRejected)
        {
            std::string payload = "{\"text\":\"a";
            payload.push_back('\0');
            payload += "b\"}";
            ExpectInvalidFrame(MakeFrame(payload));
        }

        TEST_METHOD (DisconnectDuringHeaderDoesNotDispatch)
        {
            auto frame = MakeFrame("{\"incomplete\":true}");
            frame.resize(2);
            ExpectDisconnectedFrame(frame);
        }

        TEST_METHOD (DisconnectDuringPayloadDoesNotDispatch)
        {
            auto frame = MakeFrame("{\"incomplete\":true}");
            frame.resize(sizeof(DWORD) + 2);
            ExpectDisconnectedFrame(frame);
        }

        TEST_METHOD (PartialHeaderTimesOut)
        {
            auto frame = MakeFrame("{\"incomplete\":true}");
            frame.resize(1);
            ExpectTimedOutFrame(frame);
        }

        TEST_METHOD (PartialPayloadTimesOut)
        {
            auto frame = MakeFrame("{\"incomplete\":true}");
            frame.resize(sizeof(DWORD) + 2);
            ExpectTimedOutFrame(frame);
        }

        TEST_METHOD (WrongPidIsRejectedBeforeAnyMessageCallback)
        {
            ServerSession session;
            auto policy = CurrentProcessPolicy();
            policy.expectedClientPid = 0;
            session.OpenClient();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame("{\"untrusted\":true}")));
            session.Start(policy);

            Assert::AreEqual<DWORD>(WAIT_OBJECT_0, WaitForSingleObject(session.callbacks->rejected.get(), OperationTimeoutMs));
            BYTE byte = 0;
            const auto readError = session.client.Read(&byte, 1);
            Assert::IsTrue(session.Stop(), L"Do not wait for the server's full connection timeout after rejecting a peer.");
            Assert::IsTrue(readError == ERROR_BROKEN_PIPE || readError == ERROR_PIPE_NOT_CONNECTED || readError == ERROR_NO_DATA);
            const auto rejections = session.callbacks->Rejections();
            Assert::AreEqual<size_t>(1, rejections.size());
            Assert::IsFalse(rejections.front().accepted);
            Assert::AreEqual<DWORD>(GetCurrentProcessId(), rejections.front().pid);
            Assert::AreEqual(std::wstring(L"pid-mismatch"), std::wstring(rejections.front().reasonCode));
            Assert::IsTrue(session.callbacks->Messages().empty());
            Assert::IsFalse(session.server->IsConnected());
            Assert::IsFalse(session.server->Send(L"{\"not\":\"authenticated\"}"));
        }

        TEST_METHOD (StopUnblocksIdleReadWithoutFailure)
        {
            ServerSession session;
            session.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame("{\"ready\":true}")));
            Assert::IsTrue(session.callbacks->WaitForMessages(1));
            Assert::AreEqual<DWORD>(WAIT_TIMEOUT, WaitForSingleObject(session.callbacks->failed.get(), 50));
            Assert::IsTrue(session.Stop(), L"Stop must cancel the idle read while the client remains open.");
            session.server->Stop();
            Assert::IsFalse(session.server->IsConnected());
            Assert::IsFalse(session.server->Send(L"{\"after\":\"stop\"}"));
            Assert::IsTrue(session.callbacks->Failures().empty());
            Assert::AreEqual<size_t>(1, session.callbacks->Messages().size());
        }

        TEST_METHOD (StopUnblocksPendingConnectionWithoutFailure)
        {
            ServerSession session;
            session.Start(CurrentProcessPolicy());
            Assert::AreEqual<DWORD>(WAIT_TIMEOUT, WaitForSingleObject(session.callbacks->failed.get(), 50));
            Assert::IsTrue(session.Stop(), L"Stop must not wait for the ten-second connection deadline.");
            Assert::IsFalse(session.server->IsConnected());
            Assert::IsTrue(session.callbacks->Failures().empty());
            Assert::IsTrue(session.callbacks->Messages().empty());
        }

        TEST_METHOD (DisconnectedConnectionCannotBeReused)
        {
            ServerSession session;
            session.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, session.client.Write(MakeFrame("{\"original\":true}")));
            Assert::IsTrue(session.callbacks->WaitForMessages(1));
            session.client.Close();
            Assert::AreEqual<DWORD>(WAIT_OBJECT_0, WaitForSingleObject(session.callbacks->failed.get(), OperationTimeoutMs));
            Assert::IsFalse(session.server->IsConnected());
            Assert::IsFalse(session.server->Send(L"{\"stale\":true}"));
            ResetEvent(session.callbacks->received.get());

            PipeClient replacement;
            DWORD error = ERROR_SUCCESS;
            if (replacement.Open(session.server->Name(), error))
            {
                replacement.Write(MakeFrame("{\"replacement\":true}"));
                Assert::AreEqual<DWORD>(WAIT_TIMEOUT, WaitForSingleObject(session.callbacks->received.get(), 50));
            }
            else
            {
                Assert::IsTrue(error == ERROR_PIPE_BUSY || error == ERROR_FILE_NOT_FOUND || error == ERROR_PIPE_NOT_CONNECTED);
            }
            session.ExpectFailure(LauncherIpcFailure::Disconnected);
            const auto received = session.callbacks->Messages();
            Assert::AreEqual<size_t>(1, received.size());
            Assert::AreEqual(std::wstring(L"{\"original\":true}"), received.front());
        }

        TEST_METHOD (SeparateServersHaveUniqueIsolatedConnections)
        {
            ServerSession first;
            ServerSession second;
            Assert::IsTrue(first.server->Name().find(L"PowerToys.Workspaces.Launcher.") == 0);
            Assert::IsTrue(second.server->Name().find(L"PowerToys.Workspaces.Launcher.") == 0);
            Assert::AreNotEqual(first.server->Name(), second.server->Name());
            first.Connect();
            second.Connect();
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, first.client.Write(MakeFrame("{\"server\":1}")));
            Assert::IsTrue(first.callbacks->WaitForMessages(1));
            Assert::IsTrue(first.Stop());
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, second.client.Write(MakeFrame("{\"server\":2}")));
            Assert::IsTrue(second.callbacks->WaitForMessages(1));
            Assert::IsTrue(second.server->Send(L"{\"reply\":2}"));
            Assert::AreEqual(std::string("{\"reply\":2}"), second.client.ReadFrame());
            Assert::IsTrue(second.Stop());
            const auto firstMessages = first.callbacks->Messages();
            const auto secondMessages = second.callbacks->Messages();
            Assert::AreEqual<size_t>(1, firstMessages.size());
            Assert::AreEqual<size_t>(1, secondMessages.size());
            Assert::AreEqual(std::wstring(L"{\"server\":1}"), firstMessages.front());
            Assert::AreEqual(std::wstring(L"{\"server\":2}"), secondMessages.front());
            Assert::IsTrue(first.callbacks->Failures().empty());
            Assert::IsTrue(second.callbacks->Failures().empty());
        }

        TEST_METHOD (InvalidOutgoingMessagesFailClosed)
        {
            const std::array<std::wstring, 5> invalid{
                std::wstring{},
                std::wstring(L"a\0b", 3),
                std::wstring(1, static_cast<wchar_t>(0xd800)),
                std::wstring(LauncherIpcServer::MaxMessageBytes + 1, L'a'),
                std::wstring(LauncherIpcServer::MaxMessageBytes / 3 + 1, L'\u0800')
            };
            for (const auto& message : invalid)
            {
                ServerSession session;
                session.Connect();
                Assert::IsFalse(session.server->Send(message));
                session.ExpectFailure(LauncherIpcFailure::InvalidMessage);
                Assert::IsTrue(session.callbacks->Messages().empty());
            }
        }
    };
}
