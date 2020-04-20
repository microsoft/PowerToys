#pragma once

#include <common\keyboard_layout.h>
#include <common\two_way_pipe_message_ipc.h>
#include <msclr\marshal.h>
#include <msclr\marshal_cppstd.h>

using namespace System;

//https://docs.microsoft.com/en-us/cpp/dotnet/how-to-wrap-native-class-for-use-by-csharp?view=vs-2019
namespace interop
{
public ref class LayoutMapManaged
    {
    public:
        LayoutMapManaged() :
            _map(new LayoutMap) {}

        ~LayoutMapManaged()
        {
            delete _map;
        }

        String ^ GetKeyName(DWORD key) 
        {
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

    public ref class TwoWayPipeMessageIPCManaged
    {
    public:
        TwoWayPipeMessageIPCManaged(String^ inputPipeName, String^ outputPipeName)
        {
            _pipe = new TwoWayPipeMessageIPC(
                msclr::interop::marshal_as<std::wstring>(inputPipeName),
                msclr::interop::marshal_as<std::wstring>(outputPipeName),
                nullptr);
        }

        ~TwoWayPipeMessageIPCManaged()
        {
            delete _pipe;
        }

        void Send(String^ msg)
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
        TwoWayPipeMessageIPC* _pipe;
    };
}

