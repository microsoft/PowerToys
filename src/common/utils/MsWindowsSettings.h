#pragma once

#include <Windows.h>
#include "../logger/logger.h"

inline bool GetAnimationsEnabled()
{
    BOOL enabled = 0;
    BOOL fResult;
    fResult = SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0);
    if (!fResult)
    {
        Logger::error("SystemParametersInfo SPI_GETCLIENTAREAANIMATION failed.");
    }
    return enabled;
}
