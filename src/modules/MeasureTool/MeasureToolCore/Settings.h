#pragma once

#include <array>
#include <cinttypes>

struct Settings
{
    uint8_t pixelTolerance = 5;
    bool continuousCapture = false;
    bool drawFeetOnCross = true;
    std::array<uint8_t, 3> lineColor = {255, 69, 0};

    static Settings LoadFromFile();
};