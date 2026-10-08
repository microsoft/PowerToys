#include "pch.h"
#include "IPCHelper.h"

#include <common/logger/logger.h>
#include <wil/resource.h>
#include <filesystem>

IPCHelper::IPCHelper(const std::wstring& currentPipeName, const std::wstring receiverPipeName, std::function<void(const std::wstring&)> messageCallback, const interop_auth::CallerPolicy& peerPolicy) :
    callback(messageCallback)
{
    wil::unique_handle token;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()))
    {
        Logger::error("Failed to get process token");
        throw std::runtime_error("Failed to obtain IPC token");
    }

    std::unique_lock lock{ ipcMutex };
    ipc = make_unique<TwoWayPipeMessageIPC>(currentPipeName, receiverPipeName, std::bind(&IPCHelper::receive, this, std::placeholders::_1));
    ipc->start(token.get(), peerPolicy);
}

IPCHelper::~IPCHelper()
{
    std::unique_lock lock{ ipcMutex };
    if (ipc)
    {
        ipc->end();
        ipc = nullptr;
    }
}

void IPCHelper::send(const std::wstring& message) const
{
    ipc->send(message);
}

void IPCHelper::receive(const std::wstring& msg)
{
    std::function<void(const std::wstring&)> handler;
    {
        std::lock_guard lock(callbackMutex);
        handler = callback;
    }
    if (handler)
    {
        handler(msg);
    }
}

void IPCHelper::SetCallback(std::function<void(const std::wstring&)> messageCallback)
{
    std::lock_guard lock(callbackMutex);
    callback = std::move(messageCallback);
}

interop_auth::CallerPolicy IPCHelper::ModulePeer(const std::wstring& executable, std::optional<DWORD> pid)
{
    wchar_t path[32768]{};
    const auto length = GetModuleFileNameW(nullptr, path, ARRAYSIZE(path));
    if (!length || length == ARRAYSIZE(path))
        throw std::runtime_error("Cannot resolve module identity");
    interop_auth::CallerPolicy policy;
    policy.enabled = true;
    policy.expectedClientPid = pid;
    policy.expectedDirectory = std::filesystem::path(path).parent_path().wstring();
    policy.allowedBasenames = { executable };
    policy.expectedVersion = interop_auth::GetOwnModuleVersion();
    if (!policy.expectedVersion)
        throw std::runtime_error("Module version is unavailable");
    policy.logReject = [](const interop_auth::AuthResult& result) {
        Logger::error(L"Workspaces CLI: IPC peer rejected: {}", result.reasonCode);
    };
    return policy;
}
