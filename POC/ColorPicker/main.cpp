#include <iostream>
#include <windows.h>
#include "Utility.h"

int main()
{
    HDC window = GetWindowDC(NULL);
    while (true)
    {
        POINT cursorPosition;
        if (GetCursorPos(&cursorPosition))
        {
            COLORREF color = GetPixel(window, cursorPosition.x, cursorPosition.y);
            std::cout << "X: " << cursorPosition.x << "Y: " << cursorPosition.y << '\n';
            Utility::printColorRef(color);
        }
    }
}
