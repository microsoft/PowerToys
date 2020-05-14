#include <iostream>
#include "Resolution.h"
#include <vector>
#include <windows.h>

int main()
{
    std::vector<MonitorDisplayDevice> monitorDisplayDevices = getAllMonitorDisplayDevices();
    setDisplayResolution(monitorDisplayDevices.at(0).displayAdapterName, Resolution(1920, 1080));
    std::cout << "Hello World!\n";
    

}
