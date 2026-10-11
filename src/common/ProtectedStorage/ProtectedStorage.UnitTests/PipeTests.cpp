#include "PipeTests.h"
#include "../ProtectedStorage.Common/Protocol.h"
#include <iostream>
#include <thread>

using namespace PowerToys::ProtectedStorage;

namespace
{
    constexpr DWORD Timeout = 3000;
    void Connect(HANDLE pipe)
    {
        Handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event) Fail("test connection event");
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        if (ConnectNamedPipe(pipe, &operation))
            return;
        const auto error = GetLastError();
        if (error == ERROR_PIPE_CONNECTED)
            return;
        Require(error == ERROR_IO_PENDING, "test pipe connection");
        const auto wait = WaitForSingleObject(event.get(), Timeout);
        DWORD ignored = 0;
        if (wait != WAIT_OBJECT_0)
        {
            CancelIoEx(pipe, &operation);
            GetOverlappedResult(pipe, &operation, &ignored, TRUE);
            throw Error("test connect timed out", wait);
        }
        if (!GetOverlappedResult(pipe, &operation, &ignored, FALSE))
            Fail("complete test connection");
    }
    Handle Server(const std::wstring& endpoint)
    {
        Handle pipe(CreateNamedPipeW(endpoint.c_str(),
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            1, 65536, 65536, 0, nullptr));
        if (!pipe) Fail("create isolated test pipe");
        return pipe;
    }
}

int RunPipeTestServer(int argc, wchar_t** argv)
{
    Require(argc == 4 && std::wstring_view(argv[1]) == L"--pipe-test-server", "test server arguments");
    const std::wstring suffix(argv[2]), mode(argv[3]);
    Require(suffix.starts_with(L"PTPS.PipeTests.") && suffix.find_first_of(L"\\/") == suffix.npos,
        "isolated test pipe namespace");
    Require(mode == L"success" || mode == L"error", "test response mode");
    auto pipe = Server(L"\\\\.\\pipe\\" + suffix);
    std::cout << "ready\n" << std::flush;
    Connect(pipe.get());
    const auto request = ReadFrame(pipe.get(), nullptr, Timeout);
    Frame response{ request.command, request.requestId,
        mode == L"error" ? ErrorMetadata(StorageError(ErrorCode::Unauthorized, ERROR_ACCESS_DENIED)) :
                          Value(Value::Object{ { "errorCode", "None" }, { "protocolMajor", uint64_t{ 1 } } }), {} };
    WriteFrame(pipe.get(), response, nullptr, Timeout);
    std::cout << "written\n" << std::flush;
    WaitForClientDisconnect(pipe.get(), nullptr, Timeout);
    if (!DisconnectNamedPipe(pipe.get())) Fail("disconnect test pipe");
    return 0;
}

void RunPipeCompletionTests()
{
    for (bool cancel : { false, true })
    {
        const auto endpoint = L"\\\\.\\pipe\\PTPS.PipeTests." + NewId();
        auto server = Server(endpoint);
        Handle client(CreateFileW(endpoint.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr,
            OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr));
        if (!client) Fail("open test client");
        Connect(server.get());
        Handle stop(CreateEventW(nullptr, TRUE, cancel, nullptr));
        if (!stop) Fail("test cancellation event");
        const auto began = GetTickCount64();
        DWORD failure = 0;
        try
        {
            WaitForClientDisconnect(server.get(), stop.get(), 100);
        }
        catch (const Error& error)
        {
            failure = error.code;
        }
        const DWORD expected = cancel ? ERROR_OPERATION_ABORTED : ERROR_TIMEOUT;
        Require(failure == expected,
            "stalled response peer must time out or cancel");
        Require(GetTickCount64() - began < 2000, "response completion deadline");
    }
    const auto endpoint = L"\\\\.\\pipe\\PTPS.PipeTests." + NewId();
    auto server = Server(endpoint);
    Handle client(CreateFileW(endpoint.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr,
        OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr));
    if (!client) Fail("open closing test client");
    Connect(server.get());
    client.reset();
    WaitForClientDisconnect(server.get(), nullptr, Timeout);

    const auto cancelledEndpoint = L"\\\\.\\pipe\\PTPS.PipeTests." + NewId();
    auto cancelledServer = Server(cancelledEndpoint);
    Handle stalledClient(CreateFileW(cancelledEndpoint.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr,
        OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr));
    if (!stalledClient) Fail("open in-flight cancellation client");
    Connect(cancelledServer.get());
    Handle stop(CreateEventW(nullptr, TRUE, FALSE, nullptr));
    if (!stop) Fail("in-flight cancellation event");
    std::thread cancel([&] { Sleep(50); SetEvent(stop.get()); });
    DWORD cancellation = 0;
    try { WaitForClientDisconnect(cancelledServer.get(), stop.get(), Timeout); }
    catch (const Error& error) { cancellation = error.code; }
    cancel.join();
    Require(cancellation == ERROR_OPERATION_ABORTED, "service stop interrupts response completion");

    BYTE extra = 1;
    BoundedIo(stalledClient.get(), true, &extra, 1, nullptr, Timeout);
    bool rejected = false;
    try { WaitForClientDisconnect(cancelledServer.get(), nullptr, Timeout); }
    catch (const StorageError& error) { rejected = error.errorCode == ErrorCode::InvalidPayload; }
    Require(rejected, "one-request pipe rejects extra client data");
}
