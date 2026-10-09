#include "pch.h"
#include "ScalingUtils.h"

#include <common/Display/dpi_aware.h>
#include <common/utils/winapi_error.h>

float ScalingUtils::ScalingFactor(HWND window) noexcept
{
    UINT dpi = 96;
    auto res = DPIAware::GetScreenDPIForWindow(window, dpi);

    if (res != S_OK)
    {
        Logger::error(L"Failed to get DPI: {}", get_last_error_or_default(res));
        return 1.0f;
    }

    return dpi / 96.0f;
}
