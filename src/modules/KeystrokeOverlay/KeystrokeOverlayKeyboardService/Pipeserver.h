// Pipeserver.h
// Administrates named pipes server for IPC communications.
#pragma once
#include <windows.h>
#include <string>
#include <vector>

class PipeServer
{
public:
    explicit PipeServer(const wchar_t *name = LR"(\\.\pipe\KeystrokeOverlayPipe)");
    ~PipeServer();                           // Destructor
    bool EnsureClient();                     // Accept client if none
    bool SendFrame(const std::string &json); // [uint32 length][utf8]

private:
    std::wstring _name;
    HANDLE _hPipe = INVALID_HANDLE_VALUE;
    bool CreateAndListen();
    void Close();
};
