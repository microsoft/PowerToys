#include <iostream>
#include <windows.h>

void printColorRef(COLORREF);

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
            printColorRef(color);
        }
    }
}

void printColorRef(COLORREF color)
{
    int red = static_cast<int>(GetRValue(color));
    int green = static_cast<int>(GetGValue(color));
    int blue = static_cast<int>(GetBValue(color));
    std::cout << "Red: " << red << '\n'
              << "Green: " << green << '\n'
              << "Blue: " << blue << '\n'
              << std::endl;
}
