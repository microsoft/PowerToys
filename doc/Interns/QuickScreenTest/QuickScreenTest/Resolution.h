#pragma once

#include "Monitor.h"

struct Resolution
{
    int width;
    int height;
    int frequency;

    Resolution(int _width, int _height, int _frequency)
    {
        width = _width;
        height = _height;
        frequency = _frequency;
    }
};

std::vector<Resolution> getAllPossibleMonitorResolutions(Monitor* monitor);
Resolution getCurrentMonitorResolution(Monitor* monitor);
bool setMonitorResolution(Monitor* monitor, Resolution resolution);

