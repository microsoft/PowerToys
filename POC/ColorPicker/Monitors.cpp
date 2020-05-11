#include <Windows.h>
#include <iostream>

BOOL MonitorEnumProc(
    HMONITOR handle,
    HDC monitorDC,
    LPRECT monitorVirtualSize,
    LPARAM passedDataFromCall
)
{
    return 1;
}

void run() {
	HDC window = GetWindowDC(NULL);
    EnumDisplayMonitors(window, NULL, &MonitorEnumProc, {});
}