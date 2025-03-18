#include "pch.h"
#include "IPCHelper.h"

#include <common/logger/logger.h>

IPCHelper::IPCHelper(const std::wstring& currentPipeName, const std::wstring receiverPipeName, std::function<void(const std::wstring&)> messageCallback) :
    callback(messageCallback)
{
    HANDLE hToken = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken))
    {
        Logger::error("Failed to get process token");
        return;
    }

    std::unique_lock lock{ ipcMutex };
    ipc = make_unique<TwoWayPipeMessageIPC>(currentPipeName, receiverPipeName, std::bind(&IPCHelper::receive, this, std::placeholders::_1));
    ipc->start(hToken);
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
    if (callback)
    {
        callback(msg);
    }
}
