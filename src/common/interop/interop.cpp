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
// TODO: move to a separate library in common
#include "../../modules/videoconference/VideoConferenceShared/MicrophoneDevice.h"
#include "../../modules/videoconference/VideoConferenceShared/VideoCaptureDeviceList.h"
#pragma managed(pop)

#include <common/version/version.h>


using namespace System;
using namespace System::Runtime::InteropServices;
using System::Collections::Generic::List;

// https://learn.microsoft.com/cpp/dotnet/how-to-wrap-native-class-for-use-by-csharp?view=vs-2019
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

        static List<String ^> ^ GetAllActiveMicrophoneDeviceNames() {
            auto names = gcnew List<String ^>();
            for (const auto& device : MicrophoneDevice::getAllActive())
            {
                names->Add(gcnew String(device->name().data()));
            }
            return names;
        }

            static List<String ^> ^
            GetAllVideoCaptureDeviceNames() {
                auto names = gcnew List<String ^>();
                VideoCaptureDeviceList vcdl;
                vcdl.EnumerateDevices();

                for (UINT32 i = 0; i < vcdl.Count(); ++i)
                {
                    auto name = gcnew String(vcdl.GetDeviceName(i).data());
                    if (name != L"PowerToys VideoConference Mute")
                    {
                        names->Add(name);
                    }
                }
                return names;
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

        static String ^ PowerLauncherCentralizedHookSharedEvent() {
            return gcnew String(CommonSharedConstants::POWER_LAUNCHER_CENTRALIZED_HOOK_SHARED_EVENT);
        }

        static String ^ RunSendSettingsTelemetryEvent() {
            return gcnew String(CommonSharedConstants::RUN_SEND_SETTINGS_TELEMETRY_EVENT);
        }

        static String ^ RunExitEvent() {
            return gcnew String(CommonSharedConstants::RUN_EXIT_EVENT);
        }

        static String ^ FZEExitEvent() {
            return gcnew String(CommonSharedConstants::FZE_EXIT_EVENT);
        }

        static String ^ FZEToggleEvent() {
          return gcnew String(CommonSharedConstants::FANCY_ZONES_EDITOR_TOGGLE_EVENT);
        }

        static String ^ ColorPickerSendSettingsTelemetryEvent() {
            return gcnew String(CommonSharedConstants::COLOR_PICKER_SEND_SETTINGS_TELEMETRY_EVENT);
        }

        static String ^ ShowColorPickerSharedEvent() {
            return gcnew String(CommonSharedConstants::SHOW_COLOR_PICKER_SHARED_EVENT);
        }

        static String ^ ShowPowerOCRSharedEvent() {
            return gcnew String(CommonSharedConstants::SHOW_POWEROCR_SHARED_EVENT);
        }

        static String ^ AwakeExitEvent() {
            return gcnew String(CommonSharedConstants::AWAKE_EXIT_EVENT);
        }

        static String ^ PowerAccentExitEvent() {
            return gcnew String(CommonSharedConstants::POWERACCENT_EXIT_EVENT);
        }

        static String ^ ShortcutGuideTriggerEvent() {
            return gcnew String(CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT);
        }

        static String
            ^ MeasureToolTriggerEvent() {
                  return gcnew String(CommonSharedConstants::MEASURE_TOOL_TRIGGER_EVENT);
              }
        static String ^ GcodePreviewResizeEvent() {
            return gcnew String(CommonSharedConstants::GCODE_PREVIEW_RESIZE_EVENT);
        }

        static String ^ DevFilesPreviewResizeEvent() {
            return gcnew String(CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT);
        }

        static String ^ MarkdownPreviewResizeEvent() {
            return gcnew String(CommonSharedConstants::MARKDOWN_PREVIEW_RESIZE_EVENT);
        }

        static String ^ PdfPreviewResizeEvent() {
            return gcnew String(CommonSharedConstants::PDF_PREVIEW_RESIZE_EVENT);
        }

        static String ^ SvgPreviewResizeEvent() {
            return gcnew String(CommonSharedConstants::SVG_PREVIEW_RESIZE_EVENT);
        }
    };
}
