#pragma once

#include <msclr\marshal.h>
#include <msclr\marshal_cppstd.h>
#include <functional>
#include "..\keyboard_layout.h"
#include "..\two_way_pipe_message_ipc.h"
#include "..\common.h"
#include "..\shared_constants.h"

using namespace System;
using namespace System::Runtime::InteropServices;

//https://docs.microsoft.com/en-us/cpp/dotnet/how-to-wrap-native-class-for-use-by-csharp?view=vs-2019
namespace interop
{
public
    ref class LayoutMapManaged
    {
    public:
        LayoutMapManaged() :
            _map(new LayoutMap) {}

        ~LayoutMapManaged()
        {
            delete _map;
        }

        String ^ GetKeyName(DWORD key) {
            return gcnew String(_map->GetKeyName(key).c_str());
        }

            void Updatelayout()
        {
            _map->UpdateLayout();
        }

    protected:
        !LayoutMapManaged()
        {
            delete _map;
        }

    private:
        LayoutMap* _map;
    };

public
    ref class TwoWayPipeMessageIPCManaged
    {
    public:
        delegate void ReadCallback(String ^ message);

        TwoWayPipeMessageIPCManaged(String ^ inputPipeName, String ^ outputPipeName, ReadCallback ^ callback)
        {
            _wrapperCallback = gcnew InternalReadCallback(this, &TwoWayPipeMessageIPCManaged::ReadCallbackHelper);
            _callback = callback;
            
            TwoWayPipeMessageIPC::callback_function cb = nullptr;
            if (callback != nullptr)
            {
                cb = (TwoWayPipeMessageIPC::callback_function)(void*)Marshal::GetFunctionPointerForDelegate(_wrapperCallback);
            }
            _pipe = new TwoWayPipeMessageIPC(
                msclr::interop::marshal_as<std::wstring>(inputPipeName),
                msclr::interop::marshal_as<std::wstring>(outputPipeName),
                cb);
        }

        ~TwoWayPipeMessageIPCManaged()
        {
            delete _pipe;
        }

        void Send(String ^ msg)
        {
            _pipe->send(msclr::interop::marshal_as<std::wstring>(msg));
        }

        void Start()
        {
            _pipe->start(nullptr);
        }

        void End()
        {
            _pipe->end();
        }

    protected:
        !TwoWayPipeMessageIPCManaged()
        {
            delete _pipe;
        }

    private:
        delegate void InternalReadCallback(const std::wstring& msg);

        TwoWayPipeMessageIPC* _pipe;
        ReadCallback ^ _callback;
        InternalReadCallback ^ _wrapperCallback;

        void ReadCallbackHelper(const std::wstring& msg)
        {
            _callback(gcnew String(msg.c_str()));
        }
    };

    public
    ref class CommonManaged
    {
    public:
        static String^ GetProductVersion() 
        {
            return gcnew String(get_product_version().c_str());
        } 
    };

    public
    ref class Constants
    {
    public:
        literal int VK_WIN_BOTH = CommonSharedConstants::VK_WIN_BOTH;
    };
}
