#pragma once

#include <functional>
#include <vector>
#include <Windows.h>

class MonitorEnumerator
{
public:
	static std::vector<std::pair<HMONITOR, MONITORINFOEX>> Enumerate()
	{
		MonitorEnumerator inst;
		EnumDisplayMonitors(NULL, NULL, Callback, reinterpret_cast<LPARAM>(&inst));
		return inst.m_monitors;
	}

private:
	MonitorEnumerator() = default;
	~MonitorEnumerator() = default;

	static BOOL CALLBACK Callback(HMONITOR monitor, HDC /*hdc*/, LPRECT /*pRect*/, LPARAM param)
	{
		MonitorEnumerator* inst = reinterpret_cast<MonitorEnumerator*>(param);
		MONITORINFOEX mi;
		mi.cbSize = sizeof(mi);
		if (GetMonitorInfo(monitor, &mi))
		{
			inst->m_monitors.push_back({monitor, mi});
		}

		return TRUE;
	}

	std::vector<std::pair<HMONITOR, MONITORINFOEX>> m_monitors;
};