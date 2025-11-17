// PipeServer.cpp
// Administrates named pipes server for IPC communications.
#include "PipeServer.h" // relies on named pipes api through windows.h

PipeServer::PipeServer(const wchar_t *name) : _name(name) {} // Constructor
PipeServer::~PipeServer() { Close(); }                       // Destructor, calls Close()

// Check PipeServer.md for docs
bool PipeServer::CreateAndListen()
{
    Close(); // close instance if already open
    _hPipe = CreateNamedPipeW(
        _name.c_str(),
        PIPE_ACCESS_OUTBOUND | FILE_FLAG_FIRST_PIPE_INSTANCE,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1, 1 << 16, 1 << 16, 0, nullptr);
    if (_hPipe == INVALID_HANDLE_VALUE)
        return false;
    BOOL ok = ConnectNamedPipe(_hPipe, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
    return ok == TRUE;
}

// If no client is connected, wait for one.
// Allows for worker thread to ensure a connection is present before sending stuff.
bool PipeServer::EnsureClient()
{
    if (_hPipe != INVALID_HANDLE_VALUE)
        return true;
    return CreateAndListen();
}

// Sends a length-prefixed JSON frame through the pipe.
bool PipeServer::SendFrame(const std::string &json)
{
    // if no client, try to create and listen
    if (_hPipe == INVALID_HANDLE_VALUE && !CreateAndListen())
        return false;

    DWORD len = static_cast<DWORD>(json.size()); // gets size of payload (DWORD type for this case)

    // prevent giant payloads (over 8 MiB this case)
    if (len > 8 * 1024 * 1024)
        return false;

    DWORD wrote = 0;

    if (!WriteFile(_hPipe, &len, sizeof(len), &wrote, nullptr) || wrote != sizeof(len)) // write length prefix
    {
        Close(); // on failed write, close (and reset) pipe
        return false;
    }
    if (!WriteFile(_hPipe, json.data(), len, &wrote, nullptr) || wrote != len) // write payload
    {
        Close(); // on failed write, close (and reset) pipe
        return false;
    }
    return true;
}

// Ensures any open handle is properly closed
void PipeServer::Close()
{
    if (_hPipe != INVALID_HANDLE_VALUE)
    {
        FlushFileBuffers(_hPipe);
        DisconnectNamedPipe(_hPipe);
        CloseHandle(_hPipe);
        _hPipe = INVALID_HANDLE_VALUE;
    }
}
