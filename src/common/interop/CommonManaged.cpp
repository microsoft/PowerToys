#include "pch.h"
#include "CommonManaged.h"
#include "CommonManaged.g.cpp"
#include <common/version/version.h>
#include "../../modules/videoconference/VideoConferenceShared/MicrophoneDevice.h"
#include "../../modules/videoconference/VideoConferenceShared/VideoCaptureDeviceList.h"

namespace winrt::PowerToys::Interop::implementation
{
    hstring CommonManaged::GetProductVersion()
    {
        return hstring{ get_product_version() };
    }
    winrt::Windows::Foundation::Collections::IVector<hstring> CommonManaged::GetAllActiveMicrophoneDeviceNames()
    {
        auto names = std::vector<winrt::hstring>();
        for (const auto& device : MicrophoneDevice::getAllActive())
        {
            names.push_back(device->name().data());
        }
        return winrt::multi_threaded_vector(std::move(names));
    }
    winrt::Windows::Foundation::Collections::IVector<hstring> CommonManaged::GetAllVideoCaptureDeviceNames()
    {
        auto names = std::vector<winrt::hstring>();
        VideoCaptureDeviceList vcdl;
        vcdl.EnumerateDevices();

        for (UINT32 i = 0; i < vcdl.Count(); ++i)
        {
            auto name = vcdl.GetDeviceName(i).data();
            if (name != L"PowerToys VideoConference Mute")
            {
                names.push_back(name);
            }
        }
        return winrt::multi_threaded_vector(std::move(names));
    }
}
