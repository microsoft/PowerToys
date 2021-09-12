#include "pch.h"

#include <msclr/marshal.h>
#include <msclr/marshal_cppstd.h>
#include <functional>
#include "keyboard_layout.h"
#include "two_way_pipe_message_ipc.h"
#include "shared_constants.h"

// We cannot use C++/WinRT APIs when compiled with /clr (we'll get a runtime crash). os-detect API is used
// in both native C++ and C++/CX.
// We also cannot compile it as a library, since we use different cppruntime linkage in C++/CX and native C++.
// Therefore the simplest way is to compile these functions as native using the pragmas below.
#pragma managed(push, off)
#include "../utils/os-detect.h"
#pragma managed(pop)

#include <common/version/version.h>

using namespace System;
using namespace System::Runtime::InteropServices;

// https://docs.microsoft.com/en-us/cpp/dotnet/how-to-wrap-native-class-for-use-by-csharp?view=vs-2019
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
        static String ^ GetProductVersion() {
            return gcnew String(get_product_version().c_str());
        }
    };

public
    ref class Constants
    {
    public:
        literal int VK_WIN_BOTH = CommonSharedConstants::VK_WIN_BOTH;

        static String ^ AppDataPath() {
            auto localPath = Environment::GetFolderPath(Environment::SpecialFolder::LocalApplicationData);
            auto powerToysPath = gcnew String(CommonSharedConstants::APPDATA_PATH);
            return System::IO::Path::Combine(localPath, powerToysPath);
        }

        static String ^ PowerLauncherSharedEvent() {
            return gcnew String(CommonSharedConstants::POWER_LAUNCHER_SHARED_EVENT);
        }

        static String ^ RunSendSettingsTelemetryEvent() {
            return gcnew String(CommonSharedConstants::RUN_SEND_SETTINGS_TELEMETRY_EVENT);
        }

        static String ^ RunExitEvent() {
            return gcnew String(CommonSharedConstants::RUN_EXIT_EVENT);
        }

        static String ^ ColorPickerSendSettingsTelemetryEvent() {
            return gcnew String(CommonSharedConstants::COLOR_PICKER_SEND_SETTINGS_TELEMETRY_EVENT);
        }

        static String ^ ShowColorPickerSharedEvent() {
            return gcnew String(CommonSharedConstants::SHOW_COLOR_PICKER_SHARED_EVENT);
        }
    };
}
