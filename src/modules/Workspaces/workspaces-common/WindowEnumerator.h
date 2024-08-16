#pragma once

#include <functional>
#include <vector>
#include <Windows.h>

class WindowEnumerator
{
public:
	static std::vector<HWND> Enumerate(const std::function<bool(HWND)>& filter)
	{
		WindowEnumerator inst;
		inst.m_filter = filter;
		EnumWindows(Callback, reinterpret_cast<LPARAM>(&inst));
		return inst.m_windows;
	}

private:
	WindowEnumerator() = default;
	~WindowEnumerator() = default;

	static BOOL CALLBACK Callback(HWND window, LPARAM data)
	{
		WindowEnumerator* inst = reinterpret_cast<WindowEnumerator*>(data);
		if (inst->m_filter(window))
		{
			inst->m_windows.push_back(window);
		}

		return TRUE;
	}

	std::vector<HWND> m_windows;
	std::function<bool(HWND)> m_filter = [](HWND) { return true; };
};