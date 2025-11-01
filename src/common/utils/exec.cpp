#include "pch.h"
#include "exec.h"

#include <array>
#include <string_view>

std::optional<std::string> exec_and_read_output(const std::wstring_view command, DWORD timeout_ms)
{
    SECURITY_ATTRIBUTES saAttr{ sizeof(saAttr) };
    saAttr.bInheritHandle = false;

    constexpr size_t bufferSize = 4096;
    // We must use a named pipe for async I/O
    char pipename[MAX_PATH + 1];
    if (!GetTempFileNameA(R"(\\.\pipe\)", "tmp", 1, pipename))
    {
        return std::nullopt;
    }

    wil::unique_handle readPipe{
        CreateNamedPipeA(pipename, PIPE_ACCESS_INBOUND | FILE_FLAG_OVERLAPPED, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE, PIPE_UNLIMITED_INSTANCES, bufferSize, bufferSize, 0, &saAttr)
    };

    saAttr.bInheritHandle = true;
    wil::unique_handle writePipe{
        CreateFileA(pipename, GENERIC_WRITE, 0, &saAttr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr)
    };

    if (!readPipe || !writePipe)
    {
        return std::nullopt;
    }

    PROCESS_INFORMATION piProcInfo{};
    STARTUPINFOW siStartInfo{ sizeof(siStartInfo) };

    siStartInfo.hStdError = writePipe.get();
    siStartInfo.hStdOutput = writePipe.get();
    siStartInfo.dwFlags |= STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    siStartInfo.wShowWindow = SW_HIDE;

    std::wstring cmdLine{ command };
    if (!CreateProcessW(nullptr,
                        cmdLine.data(),
                        nullptr,
                        nullptr,
                        true,
                        NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE,
                        nullptr,
                        nullptr,
                        &siStartInfo,
                        &piProcInfo))
    {
        return std::nullopt;
    }
    // Child process inherited the write end of the pipe, we can close it now
    writePipe.reset();

    auto closeProcessHandles = wil::scope_exit([&] {
        CloseHandle(piProcInfo.hThread);
        CloseHandle(piProcInfo.hProcess);
    });

    std::string childOutput;
    bool processExited = false;
    for (;;)
    {
        char buffer[bufferSize];
        DWORD gotBytes = 0;
        wil::unique_handle ioEvent{ CreateEventW(nullptr, true, false, nullptr) };
        OVERLAPPED overlapped{ .hEvent = ioEvent.get() };
        ReadFile(readPipe.get(), buffer, sizeof(buffer), nullptr, &overlapped);

        const std::array<HANDLE, 2> handlesToWait = { overlapped.hEvent, piProcInfo.hProcess };
        switch (WaitForMultipleObjects(1 + !processExited, handlesToWait.data(), false, timeout_ms))
        {
        case WAIT_OBJECT_0 + 1:
            if (!processExited)
            {
                // When the process exits, we can reduce timeout and read the rest of the output w/o possibly big timeout
                timeout_ms = 1000;
                processExited = true;
                closeProcessHandles.reset();
            }
            [[fallthrough]];
        case WAIT_OBJECT_0:
            if (GetOverlappedResultEx(readPipe.get(), &overlapped, &gotBytes, timeout_ms, true))
            {
                childOutput += std::string_view{ buffer, gotBytes };
                break;
            }
            [[fallthrough]];
        default:
            goto exit;
        }
    }
exit:
    CancelIo(readPipe.get());
    return childOutput;
}
