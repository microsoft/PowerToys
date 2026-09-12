// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "CliPipeServer.h"
#include "TestSupport.h"

#include <array>
#include <atomic>
#include <chrono>
#include <future>
#include <thread>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace light_switch_cli;
using namespace winrt::Windows::Data::Json;
using namespace std::chrono_literals;

namespace LightSwitchServiceUnitTests
{
    namespace
    {
        class TestHandle
        {
        public:
            explicit TestHandle(HANDLE value = nullptr) noexcept : m_value(value) {}
            ~TestHandle() { Reset(); }
            TestHandle(const TestHandle&) = delete;
            TestHandle& operator=(const TestHandle&) = delete;
            TestHandle(TestHandle&& other) noexcept : m_value(std::exchange(other.m_value, nullptr)) {}
            HANDLE Get() const noexcept { return m_value; }
            void Reset() noexcept
            {
                if (m_value && m_value != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(m_value);
                }
                m_value = nullptr;
            }

        private:
            HANDLE m_value;
        };

        CliPipeServer::Options TestOptions()
        {
            static std::atomic<unsigned int> sequence = 0;
            CliPipeServer::Options options;
            options.pipeName = L"\\\\.\\pipe\\PowerToys_LightSwitch_Cli_Test_" + std::to_wstring(GetCurrentProcessId()) +
                               L"_" + std::to_wstring(GetTickCount64()) + L"_" + std::to_wstring(++sequence);
            options.readTimeoutMs = 2000;
            options.writeTimeoutMs = 2000;
            return options;
        }

        TestHandle Connect(const std::wstring& name, DWORD identificationLevel = SECURITY_IDENTIFICATION)
        {
            const auto deadline = GetTickCount64() + 5000;
            while (true)
            {
                HANDLE pipe = CreateFileW(name.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | identificationLevel, nullptr);
                if (pipe != INVALID_HANDLE_VALUE)
                {
                    return TestHandle(pipe);
                }
                const auto error = GetLastError();
                if (error != ERROR_PIPE_BUSY || GetTickCount64() >= deadline)
                {
                    throw winrt::hresult_error(HRESULT_FROM_WIN32(error));
                }
                WaitNamedPipeW(name.c_str(), 50);
            }
        }

        template<typename BeginOperation>
        DWORD ClientIo(HANDLE pipe, BeginOperation beginOperation)
        {
            TestHandle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            winrt::check_bool(event.Get() != nullptr);
            OVERLAPPED operation{};
            operation.hEvent = event.Get();
            DWORD count = 0;
            if (!beginOperation(operation))
            {
                const auto error = GetLastError();
                if (error != ERROR_IO_PENDING)
                {
                    throw winrt::hresult_error(HRESULT_FROM_WIN32(error));
                }
                if (WaitForSingleObject(event.Get(), 5000) != WAIT_OBJECT_0)
                {
                    CancelIoEx(pipe, &operation);
                    GetOverlappedResult(pipe, &operation, &count, TRUE);
                    throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_TIMEOUT));
                }
            }
            winrt::check_bool(GetOverlappedResult(pipe, &operation, &count, FALSE));
            return count;
        }

        void WriteBytes(HANDLE pipe, const void* data, DWORD size)
        {
            DWORD offset = 0;
            while (offset < size)
            {
                const auto count = ClientIo(pipe, [&](OVERLAPPED& operation) {
                    return WriteFile(pipe, static_cast<const BYTE*>(data) + offset, size - offset, nullptr, &operation);
                });
                Assert::IsTrue(count > 0);
                offset += count;
            }
        }

        void WriteRequest(HANDLE pipe, std::wstring_view request)
        {
            std::wstring framed(request);
            framed.push_back(L'\n');
            WriteBytes(pipe, framed.data(), static_cast<DWORD>(framed.size() * sizeof(wchar_t)));
        }

        JsonObject ReadResponse(HANDLE pipe)
        {
            std::wstring text;
            std::optional<BYTE> firstByte;
            while (text.size() <= MaxMessageCharacters)
            {
                std::array<BYTE, 4096> bytes{};
                const auto count = ClientIo(pipe, [&](OVERLAPPED& operation) {
                    return ReadFile(pipe, bytes.data(), static_cast<DWORD>(bytes.size()), nullptr, &operation);
                });
                Assert::IsTrue(count > 0);
                for (DWORD index = 0; index < count; ++index)
                {
                    if (!firstByte)
                    {
                        firstByte = bytes[index];
                        continue;
                    }
                    const auto value = static_cast<wchar_t>(*firstByte | (static_cast<unsigned int>(bytes[index]) << 8));
                    firstByte.reset();
                    if (value == L'\n')
                    {
                        Assert::AreEqual(count, index + 1);
                        Assert::IsTrue(text.empty() || text.front() != L'\xfeff');
                        return JsonObject::Parse(text);
                    }
                    text.push_back(value);
                }
            }
            throw std::runtime_error("The server response exceeded the protocol limit.");
        }

        JsonObject Exchange(const std::wstring& name, std::wstring_view request)
        {
            auto client = Connect(name);
            WriteRequest(client.Get(), request);
            return ReadResponse(client.Get());
        }

        void AssertError(const JsonObject& response, std::wstring_view code)
        {
            Assert::IsFalse(response.GetNamedBoolean(L"success"));
            Assert::AreEqual(std::wstring(code), std::wstring(response.GetNamedObject(L"error").GetNamedString(L"code")));
        }

        constexpr std::wstring_view StatusRequest = L"{\"version\":1,\"command\":\"status\"}";
    }

    TEST_CLASS (CliPipeServerTests)
    {
    public:
        TEST_METHOD (DispatchesOneRequestAndRetainsThePipeNameAcrossConnections)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request& request) {
                Assert::IsTrue(request.command == Command::Status);
                ++calls;
                return MakeSuccess(TestState());
            },
                                 options);
            Assert::IsTrue(server.Start());
            for (int index = 0; index < 3; ++index)
            {
                Assert::IsTrue(Exchange(options.pipeName, StatusRequest).GetNamedBoolean(L"success"));
                TestHandle rogue(CreateNamedPipeW(options.pipeName.c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE, PIPE_TYPE_BYTE, 1, 0, 0, 0, nullptr));
                Assert::IsTrue(rogue.Get() == INVALID_HANDLE_VALUE);
            }
            server.Stop();
            Assert::AreEqual(3, calls.load());
        }

        TEST_METHOD (SecondServerCannotAcquireAnOwnedName)
        {
            Apartment apartment;
            const auto options = TestOptions();
            const auto handler = [](const Request&) { return MakeSuccess(TestState()); };
            CliPipeServer first(handler, options);
            CliPipeServer second(handler, options);
            Assert::IsTrue(first.Start());
            Assert::IsFalse(second.Start());
            Assert::IsTrue(Exchange(options.pipeName, StatusRequest).GetNamedBoolean(L"success"));
        }

        TEST_METHOD (HandlerRunsAsTheServiceAfterClientIdentification)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<bool> ranWithoutImpersonation = false;
            CliPipeServer server([&](const Request&) {
                HANDLE token = nullptr;
                const BOOL opened = OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token);
                const auto error = GetLastError();
                TestHandle cleanup(token);
                ranWithoutImpersonation = !opened && error == ERROR_NO_TOKEN;
                return MakeSuccess(TestState());
            },
                                 options);
            Assert::IsTrue(server.Start());
            Assert::IsTrue(Exchange(options.pipeName, StatusRequest).GetNamedBoolean(L"success"));
            server.Stop();
            Assert::IsTrue(ranWithoutImpersonation.load());
        }

        TEST_METHOD (AnonymousClientsAreRejectedAndDoNotAffectTheNextClient)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<int> calls = 0;
            std::atomic<bool> ranWithoutImpersonation = false;
            CliPipeServer server([&](const Request&) {
                ++calls;
                HANDLE token = nullptr;
                const BOOL opened = OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token);
                const auto error = GetLastError();
                TestHandle cleanup(token);
                ranWithoutImpersonation = !opened && error == ERROR_NO_TOKEN;
                return MakeSuccess(TestState());
            },
                                 options);
            Assert::IsTrue(server.Start());
            auto anonymous = Connect(options.pipeName, SECURITY_ANONYMOUS);
            WriteRequest(anonymous.Get(), StatusRequest);
            AssertError(ReadResponse(anonymous.Get()), L"SERVICE_UNAVAILABLE");
            anonymous.Reset();
            Assert::AreEqual(0, calls.load());
            Assert::IsTrue(Exchange(options.pipeName, StatusRequest).GetNamedBoolean(L"success"));
            server.Stop();
            Assert::AreEqual(1, calls.load());
            Assert::IsTrue(ranWithoutImpersonation.load());
        }

        TEST_METHOD (HandlesUtf16CodeUnitsSplitAcrossWrites)
        {
            Apartment apartment;
            const auto options = TestOptions();
            CliPipeServer server([](const Request&) { return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            auto client = Connect(options.pipeName);
            std::wstring framed(StatusRequest);
            framed.push_back(L'\n');
            const auto bytes = reinterpret_cast<const BYTE*>(framed.data());
            const auto size = framed.size() * sizeof(wchar_t);
            for (size_t index = 0; index < size; ++index)
            {
                WriteBytes(client.Get(), bytes + index, 1);
            }
            Assert::IsTrue(ReadResponse(client.Get()).GetNamedBoolean(L"success"));
        }

        TEST_METHOD (MalformedAndOversizedRequestsNeverInvokeTheHandler)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request&) { ++calls; return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            AssertError(Exchange(options.pipeName, L"not json"), L"PROTOCOL_ERROR");
            AssertError(Exchange(options.pipeName, std::wstring(MaxMessageCharacters + 1, L' ')), L"PROTOCOL_ERROR");
            server.Stop();
            Assert::AreEqual(0, calls.load());
        }

        TEST_METHOD (TwoFramesNeverInvokeTheHandlerMoreThanOnce)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request&) { ++calls; return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            const auto response = Exchange(options.pipeName, std::wstring(StatusRequest) + L"\n" + std::wstring(StatusRequest));
            // Byte-stream reads may split even one client write. Trailing data already read
            // with the first frame is rejected; later data can never be a second command.
            if (!response.GetNamedBoolean(L"success"))
            {
                AssertError(response, L"PROTOCOL_ERROR");
            }
            server.Stop();
            Assert::IsTrue(calls.load() <= 1);
        }

        TEST_METHOD (AdditionalRequestsOnAnExistingConnectionAreNeverExecuted)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request&) { ++calls; return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            auto client = Connect(options.pipeName);
            WriteRequest(client.Get(), StatusRequest);
            Assert::IsTrue(ReadResponse(client.Get()).GetNamedBoolean(L"success"));
            WriteRequest(client.Get(), StatusRequest);
            client.Reset();
            server.Stop();
            Assert::AreEqual(1, calls.load());
        }

        TEST_METHOD (ResponseIsNotDiscardedBeforeTheClientReadsIt)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::promise<void> invoked;
            auto invocation = invoked.get_future();
            CliPipeServer server([&](const Request&) { invoked.set_value(); return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            auto client = Connect(options.pipeName);
            WriteRequest(client.Get(), StatusRequest);
            Assert::IsTrue(invocation.wait_for(2s) == std::future_status::ready);
            // A server that disconnects immediately after WriteFile discards its unread reply.
            std::this_thread::sleep_for(50ms);
            Assert::IsTrue(ReadResponse(client.Get()).GetNamedBoolean(L"success"));
        }

        TEST_METHOD (ReadTimeoutIsReportedAndTheNextClientCanConnect)
        {
            Apartment apartment;
            auto options = TestOptions();
            options.readTimeoutMs = 100;
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request&) { ++calls; return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            auto stalled = Connect(options.pipeName);
            const BYTE incompleteCodeUnit = '{';
            WriteBytes(stalled.Get(), &incompleteCodeUnit, 1);
            AssertError(ReadResponse(stalled.Get()), L"TIMEOUT");
            stalled.Reset();
            Assert::IsTrue(Exchange(options.pipeName, StatusRequest).GetNamedBoolean(L"success"));
            server.Stop();
            Assert::AreEqual(1, calls.load());
        }

        TEST_METHOD (HandlerExceptionsAndOversizedResponsesBecomeErrors)
        {
            Apartment apartment;
            const auto options = TestOptions();
            CliPipeServer server([](const Request& request) -> JsonObject {
                if (request.command == Command::Status)
                {
                    throw std::runtime_error("An injected handler failure");
                }
                auto state = TestState();
                state.SetNamedValue(L"extra", JsonValue::CreateStringValue(std::wstring(MaxMessageCharacters, L'x')));
                return MakeSuccess(state);
            },
                                 options);
            Assert::IsTrue(server.Start());
            AssertError(Exchange(options.pipeName, StatusRequest), L"EXECUTION_FAILED");
            AssertError(Exchange(options.pipeName, L"{\"version\":1,\"command\":\"light\"}"), L"PROTOCOL_ERROR");
        }

        TEST_METHOD (StopCancelsAcceptAndPartialReads)
        {
            Apartment apartment;
            auto options = TestOptions();
            options.readTimeoutMs = 10000;
            std::atomic<int> calls = 0;
            CliPipeServer server([&](const Request&) { ++calls; return MakeSuccess(TestState()); }, options);
            Assert::IsTrue(server.Start());
            auto start = GetTickCount64();
            server.Stop();
            Assert::IsTrue(GetTickCount64() - start < 2000);
            Assert::IsTrue(server.Start());
            auto client = Connect(options.pipeName);
            const BYTE incompleteCodeUnit = '{';
            WriteBytes(client.Get(), &incompleteCodeUnit, 1);
            start = GetTickCount64();
            server.Stop();
            Assert::IsTrue(GetTickCount64() - start < 2000);
            Assert::AreEqual(0, calls.load());
        }

        TEST_METHOD (StopWaitsForAnActiveHandlerBeforeReleasingState)
        {
            Apartment apartment;
            const auto options = TestOptions();
            std::promise<void> entered;
            auto invocation = entered.get_future();
            std::promise<void> release;
            auto released = release.get_future().share();
            std::atomic<bool> finished = false;
            CliPipeServer server([&](const Request&) {
                entered.set_value();
                released.wait_for(5s);
                finished = true;
                return MakeSuccess(TestState());
            },
                                 options);
            Assert::IsTrue(server.Start());
            auto client = Connect(options.pipeName);
            WriteRequest(client.Get(), StatusRequest);
            const auto handlerStarted = invocation.wait_for(2s) == std::future_status::ready;
            auto stopped = std::async(std::launch::async, [&]() { server.Stop(); });
            const auto waitedForHandler = stopped.wait_for(50ms) == std::future_status::timeout;
            release.set_value();
            const auto stoppedCleanly = stopped.wait_for(2s) == std::future_status::ready;
            Assert::IsTrue(handlerStarted);
            Assert::IsTrue(waitedForHandler);
            Assert::IsTrue(stoppedCleanly);
            Assert::IsTrue(finished.load());
        }
    };
}
