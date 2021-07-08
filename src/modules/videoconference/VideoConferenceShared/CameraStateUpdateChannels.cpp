#include "CameraStateUpdateChannels.h"

#include "naming.h"

std::wstring_view CameraOverlayImageChannel::endpoint()
{
    static const std::wstring endpoint = ObtainStableGlobalNameForKernelObject(L"PowerToysVideoConferenceCameraOverlayImageChannelSharedMemory", true);
    return endpoint;
}

std::wstring_view CameraSettingsUpdateChannel::endpoint()
{
    static const std::wstring endpoint = ObtainStableGlobalNameForKernelObject(L"PowerToysVideoConferenceSettingsChannelSharedMemory", true);
    return endpoint;
}
