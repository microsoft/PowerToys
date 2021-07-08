#pragma once

#include <optional>
#include <string_view>
#include <array>

struct alignas(16) CameraSettingsUpdateChannel
{
    bool useOverlayImage = false;
    bool cameraInUse = false;

    std::optional<uint32_t> overlayImageSize;
    std::optional<std::array<wchar_t, 256>> sourceCameraName;

    bool newOverlayImagePosted = false;

    static std::wstring_view endpoint();
};

namespace CameraOverlayImageChannel
{
    std::wstring_view endpoint();
}
