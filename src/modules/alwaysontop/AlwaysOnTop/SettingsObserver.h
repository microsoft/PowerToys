#pragma once

#include <unordered_set>

#include <Settings.h>
#include <SettingsConstants.h>

class SettingsObserver
{
public:
    SettingsObserver(std::unordered_set<SettingId> observedSettings) :
        m_observedSettings(std::move(observedSettings))
    {
        AlwaysOnTopSettings::instance().AddObserver(*this);
    }

    virtual ~SettingsObserver()
    {
        AlwaysOnTopSettings::instance().RemoveObserver(*this);
    }

    virtual void SettingsUpdate(SettingId type) {}

    bool WantsToBeNotified(SettingId type) const noexcept 
    {
        return m_observedSettings.contains(type);
    }

protected:
    std::unordered_set<SettingId> m_observedSettings;
};