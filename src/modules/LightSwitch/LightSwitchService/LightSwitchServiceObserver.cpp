#include "LightSwitchServiceObserver.h"
#include <logger.h>
#include "LightSwitchSettings.h"

// These are defined elsewhere in your service module (ServiceWorkerThread.cpp)
extern int g_lastUpdatedDay;
void ApplyThemeNow();

void LightSwitchServiceObserver::SettingsUpdate(SettingId id)
{
    Logger::info(L"[LightSwitchService] Setting changed: {}", static_cast<int>(id));
    ApplyThemeNow();
}

bool LightSwitchServiceObserver::WantsToBeNotified(SettingId id) const noexcept
{
    switch (id)
    {
    case SettingId::LightTime:
    case SettingId::DarkTime:
    case SettingId::ScheduleMode:
    case SettingId::Sunrise_Offset:
    case SettingId::Sunset_Offset:
        return true;
    default:
        return false;
    }
}
