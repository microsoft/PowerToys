#pragma once

#include <mutex>
#include <common/interop/two_way_pipe_message_ipc.h>

namespace IPCHelperStrings
{
    static std::wstring LauncherPipeName(L"\\\\.\\pipe\\powertoys_workspaces_launcher_");
    static std::wstring WindowArrangerPipeName(L"\\\\.\\pipe\\powertoys_workspaces_window_arranger_");
    static std::wstring UIPipeName(L"\\\\.\\pipe\\powertoys_workspaces_ui_");
}

class IPCHelper
{
public:
    IPCHelper(const std::wstring& currentPipeName, const std::wstring receiverPipeName, std::function<void(const std::wstring&)> messageCallback);
    ~IPCHelper();

    void send(const std::wstring& message) const;

private:
    void receive(const std::wstring& msg);

    std::unique_ptr<TwoWayPipeMessageIPC> ipc;
    std::mutex ipcMutex;
    std::function<void(const std::wstring&)> callback;
};
