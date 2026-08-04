#include "pch.h"

#include <interop/two_way_pipe_message_ipc.h>

#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        std::wstring UniquePipeName()
        {
            static LONG counter = 0;
            return L"\\\\.\\pipe\\pt_ipc_test_" +
                   std::to_wstring(GetCurrentProcessId()) + L"_" +
                   std::to_wstring(GetTickCount64()) + L"_" +
                   std::to_wstring(InterlockedIncrement(&counter));
        }

        struct OccupiedPipe
        {
            std::wstring name = UniquePipeName();
            HANDLE server = INVALID_HANDLE_VALUE;
            HANDLE client = INVALID_HANDLE_VALUE;

            ~OccupiedPipe()
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

            bool Create()
            {
                server = CreateNamedPipeW(name.c_str(),
                                          PIPE_ACCESS_DUPLEX,
                                          PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                                          PIPE_UNLIMITED_INSTANCES,
                                          4096,
                                          4096,
                                          0,
                                          nullptr);
                if (server == INVALID_HANDLE_VALUE)
                {
                    return false;
                }

                std::thread connectThread([&]() {
                    client = CreateFileW(name.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
                });
                const BOOL connected = ConnectNamedPipe(server, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
                connectThread.join();
                return connected && client != INVALID_HANDLE_VALUE;
            }
        };
    }

    TEST_CLASS(TwoWayPipeMessageIPCTests)
    {
    public:
        TEST_METHOD(ServerDoesNotJoinAnExistingPipeName)
        {
            OccupiedPipe occupiedPipe;
            Assert::IsTrue(occupiedPipe.Create(), L"failed to occupy the pipe name");

            TwoWayPipeMessageIPC server(occupiedPipe.name, UniquePipeName(), nullptr);
            server.start(nullptr);

            // The existing instance is busy. A server that wrongly creates a second instance makes
            // WaitNamedPipe succeed; FILE_FLAG_FIRST_PIPE_INSTANCE must instead make its first
            // CreateNamedPipe call fail and leave no available instance.
            const BOOL available = WaitNamedPipeW(occupiedPipe.name.c_str(), 2000);
            server.end();

            Assert::IsFalse(available, L"the server must not join an existing pipe name");
        }
    };
}
