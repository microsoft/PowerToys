#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>

class Monitors
{
public:
    static Monitors& instance();

    void Identify();

    const std::vector<FancyZonesDataTypes::MonitorId>& Get() const noexcept;

private:
    Monitors(){};
    ~Monitors() = default;

    std::vector<FancyZonesDataTypes::MonitorId> m_monitors;
};
