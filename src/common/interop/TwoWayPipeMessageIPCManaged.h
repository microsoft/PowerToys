#pragma once
#include "TwoWayPipeMessageIPCManaged.g.h"
#include "two_way_pipe_message_ipc.h"

namespace winrt::interop::implementation
{
    struct TwoWayPipeMessageIPCManaged : TwoWayPipeMessageIPCManagedT<TwoWayPipeMessageIPCManaged>
    {
        TwoWayPipeMessageIPCManaged() = default;

        TwoWayPipeMessageIPCManaged(hstring const& inputPipeName, hstring const& outputPipeName, winrt::interop::TwoWayPipeIPCReadCallback const& _callback);
        void Send(hstring const& msg);
        void Start();
        void End();
        void Close();

    private:
        TwoWayPipeMessageIPC* _pipe;
        TwoWayPipeIPCReadCallback _callback;
        std::function<void(const std::wstring& msg)> _internalReadCallback;
    };
}
namespace winrt::interop::factory_implementation
{
    struct TwoWayPipeMessageIPCManaged : TwoWayPipeMessageIPCManagedT<TwoWayPipeMessageIPCManaged, implementation::TwoWayPipeMessageIPCManaged>
    {
    };
}
