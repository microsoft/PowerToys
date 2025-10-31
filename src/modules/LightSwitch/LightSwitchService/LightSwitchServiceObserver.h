#pragma once

#include "SettingsObserver.h"

// The LightSwitchServiceObserver reacts when LightSwitchSettings changes.
class LightSwitchServiceObserver : public SettingsObserver
{
public:
    explicit LightSwitchServiceObserver(std::unordered_set<SettingId> observedSettings) :
        SettingsObserver(std::move(observedSettings))
    {
    }

    void SettingsUpdate(SettingId id) override;
    bool WantsToBeNotified(SettingId id) const noexcept override;
};
