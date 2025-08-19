#pragma once

#include <unordered_set>

#include "DarkModeSettings.h"
#include "SettingsConstants.h"

class SettingsObserver
{
public:
    SettingsObserver(std::unordered_set<SettingId> observedSettings) :
        m_observedSettings(std::move(observedSettings))
    {
        DarkModeSettings::instance().AddObserver(*this);
    }

    virtual ~SettingsObserver()
    {
        DarkModeSettings::instance().RemoveObserver(*this);
    }

    // Override this in your class to respond to updates
    virtual void SettingsUpdate(SettingId type) {}

    bool WantsToBeNotified(SettingId type) const noexcept
    {
        return m_observedSettings.contains(type);
    }

protected:
    std::unordered_set<SettingId> m_observedSettings;
};
