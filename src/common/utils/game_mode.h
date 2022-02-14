#pragma once
#include <shellapi.h>

inline bool detect_game_mode()
{
    QUERY_USER_NOTIFICATION_STATE notification_state;
    if (SHQueryUserNotificationState(&notification_state) != S_OK)
    {
        return false;
    }
    return (notification_state == QUNS_RUNNING_D3D_FULL_SCREEN);
}
