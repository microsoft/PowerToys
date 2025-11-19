#pragma once

#include <atomic>
#include <string>
#include <thread>

#include <winrt/Windows.Data.Json.h>
#include <Windows.h>

class CmdPalRpcServer
{
public:
    CmdPalRpcServer();
    ~CmdPalRpcServer();

    CmdPalRpcServer(const CmdPalRpcServer&) = delete;
    CmdPalRpcServer& operator=(const CmdPalRpcServer&) = delete;

    void Start();
    void Stop();

private:
    void Run();
    void HandleClient(HANDLE pipe);
    std::string ProcessMessage(const std::string& message);
    std::string ProcessModuleRequest(const winrt::Windows::Data::Json::JsonObject& request, const std::wstring& id);
    std::string ListModulesResponse(const std::wstring& id);
    std::string BuildErrorResponse(const std::wstring& id, const std::wstring_view code, const std::wstring_view message);

    std::atomic_bool m_running{ false };
    std::thread m_worker;
};
