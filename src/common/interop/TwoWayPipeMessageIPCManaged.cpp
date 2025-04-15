#include "pch.h"
#include "TwoWayPipeMessageIPCManaged.h"
#include "TwoWayPipeMessageIPCManaged.g.cpp"
#include "two_way_pipe_message_ipc_impl.h"
#include <functional>

namespace winrt::PowerToys::Interop::implementation
{
    TwoWayPipeMessageIPCManaged::TwoWayPipeMessageIPCManaged(hstring const& inputPipeName, hstring const& outputPipeName, winrt::PowerToys::Interop::TwoWayPipeIPCReadCallback const& _callback)
    {
        this->_callback = _callback;
        if (_callback != nullptr)
        {
            _internalReadCallback = [this](const std::wstring& msg) {
                this->_callback(msg);
            };
        }
        _pipe = new TwoWayPipeMessageIPC(std::wstring{ inputPipeName }, std::wstring{ outputPipeName }, _internalReadCallback);
    }

    void TwoWayPipeMessageIPCManaged::Send(hstring const& msg)
    {
        _pipe->send(std::wstring{ msg });
    }
    void TwoWayPipeMessageIPCManaged::Start()
    {
        _pipe->start(nullptr);
    }
    void TwoWayPipeMessageIPCManaged::End()
    {
        _pipe->end();
    }
    void TwoWayPipeMessageIPCManaged::Close()
    {
        delete _pipe;
    }
}
