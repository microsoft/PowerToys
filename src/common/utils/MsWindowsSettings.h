#pragma once

inline bool GetAnimationsEnabled()
{
    BOOL enabled = 0;
    SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0);
    return enabled;
}