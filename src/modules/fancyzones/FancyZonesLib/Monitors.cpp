#include "pch.h"
#include "Monitors.h"

#include <FancyZonesLib/MonitorUtils.h>

#include <common/logger/logger.h>

Monitors& Monitors::instance()
{
    static Monitors self;
    return self;
}

void Monitors::Identify()
{
    Logger::info(L"Identifying monitors");

    m_monitors = MonitorUtils::Display::GetDisplays();
    auto monitors = MonitorUtils::WMI::GetHardwareMonitorIds();

    for (const auto& monitor : monitors)
    {
        for (auto& display : m_monitors)
        {
            if (monitor.deviceId.id == display.deviceId.id)
            {
                display.serialNumber = monitor.serialNumber;
            }
        }
    }
}

const std::vector<FancyZonesDataTypes::MonitorId>& Monitors::Get() const noexcept
{
    return m_monitors;
}
