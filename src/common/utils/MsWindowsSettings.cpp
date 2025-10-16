#include "pch.h"
#include "MsWindowsSettings.h"

bool GetAnimationsEnabled()
{
    BOOL enabled = 0;
    const auto result = SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0);
    if (!result)
    {
        Logger::error("SystemParametersInfo SPI_GETCLIENTAREAANIMATION failed.");
    }
    return enabled != 0;
}
