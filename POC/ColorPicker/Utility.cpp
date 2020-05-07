#include <iostream>
#include "Utility.h"

namespace Utility
{
    /// Print the given color's RGB values to standard output for debugging.
    void Utility::printColorRef(COLORREF color)
    {
        int red = static_cast<int>(GetRValue(color));
        int green = static_cast<int>(GetGValue(color));
        int blue = static_cast<int>(GetBValue(color));
        std::cout << "Red: " << red << '\n'
            << "Green: " << green << '\n'
            << "Blue: " << blue << '\n'
            << std::endl;
    }
}