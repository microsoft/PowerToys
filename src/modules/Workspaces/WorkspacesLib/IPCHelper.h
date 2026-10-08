#pragma once

#include <mutex>
#include <common/interop/two_way_pipe_message_ipc.h>

namespace IPCHelperStrings
{
    static std::wstring LauncherUIPipeName(L"\\\\.\\pipe\\powertoys_workspaces_launcher_ui_");
    static std::wstring UIPipeName(L"\\\\.\\pipe\\powertoys_workspaces_ui_");

    static std::wstring LauncherArrangerPipeName(L"\\\\.\\pipe\\powertoys_workspaces_launcher_arranger_");
    static std::wstring WindowArrangerPipeName(L"\\\\.\\pipe\\powertoys_workspaces_window_arranger_");
}

class IPCHelper
{
public:
    IPCHelper(const std::wstring& currentPipeName, const std::wstring receiverPipeName, std::function<void(const std::wstring&)> messageCallback, const interop_auth::CallerPolicy& peerPolicy = {});
    ~IPCHelper();

    void send(const std::wstring& message) const;
    void SetCallback(std::function<void(const std::wstring&)> messageCallback);
    static interop_auth::CallerPolicy ModulePeer(const std::wstring& executable, std::optional<DWORD> pid = std::nullopt);

private:
    void receive(const std::wstring& msg);

    std::unique_ptr<TwoWayPipeMessageIPC> ipc;
    std::mutex ipcMutex;
    std::function<void(const std::wstring&)> callback;
    std::mutex callbackMutex;
};
