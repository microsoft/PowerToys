// Cursor.cpp : This file contains the 'main' function. Program execution begins and ends there.


#include <iostream>
#include <windows.h>
#include <cstdlib>
#include <cstdio>
#include <stdio.h>
#include <csignal>
#include <cmath>

#define PI 3.14


using namespace std;



void cursorposition(int &x_coord, int &y_coord)
{
    POINT cursor;
    if (GetCursorPos(&cursor))
    {
        x_coord = cursor.x;
        y_coord = cursor.y;
    } 
}

void getcolour(int x_coord, int y_coord, int &r, int&g, int&b)
{
    
    COLORREF color;
    HDC hDC;
    

    // Get the device context for the screen
    hDC = GetDC(NULL);
    if (hDC == NULL)
        return;

    // Get the current cursor position
    

    // Retrieve the color at that position
    color = GetPixel(hDC, x_coord, y_coord);
    if (color == CLR_INVALID)
        return;

    // Release the device context again
    ReleaseDC(GetDesktopWindow(), hDC);

    r = GetRValue(color);
    g = GetGValue(color); 
    b = GetBValue(color);
}

void draw_circle(int centre_x, int centre_y, int radius, COLORREF color, COLORREF oppositecolor, HDC hdc)
{
    int offset_y = 0;
    int offset_x = radius;
    int crit = 1 - radius;
    while (offset_y <= offset_x)
    {
        SetPixel(hdc, centre_x + offset_x, centre_y + offset_y, color); // octant 1
        SetPixel(hdc, centre_x + offset_y, centre_y + offset_x, color); // octant 2
        SetPixel(hdc, centre_x - offset_x, centre_y + offset_y, color); // octant 4
        SetPixel(hdc, centre_x - offset_y, centre_y + offset_x, color); // octant 3
        SetPixel(hdc, centre_x - offset_x, centre_y - offset_y, color); // octant 5
        SetPixel(hdc, centre_x - offset_y, centre_y - offset_x, color); // octant 6
        SetPixel(hdc, centre_x + offset_x, centre_y - offset_y, color); // octant 7
        SetPixel(hdc, centre_x + offset_y, centre_y - offset_x, color); // octant 8

        offset_y = offset_y + 1;
        if (crit <= 0)
        {
            crit = crit + 2 * offset_y + 1;
        }
        else
        {
            offset_x = offset_x - 1;
            crit = crit + 2 * (offset_y - offset_x) + 1;
        }
    }
}

void locate(int x_coord, int y_coord, int r, int g, int b)
{
    COLORREF color, oppositecolor;
    HDC hDC;
    

    // Get the device context for the screen
    hDC = GetDC(NULL);
    if (hDC == NULL)
        return;

    oppositecolor = RGB(255 - r, 255 - g, 255 - b);
    color = RGB(255 - r, 255 - g, 255 - b);

    for (int radius = 10; radius <= 100; radius += 4)
    {
        draw_circle(x_coord, y_coord, radius, color, oppositecolor, hDC);
    }
   
    ReleaseDC(GetDesktopWindow(), hDC);
    RECT rect;
    rect.left = x_coord - 500;
    rect.right = x_coord + 500;
    rect.top = y_coord - 500;
    rect.bottom = y_coord + 500;
    HWND screen = GetForegroundWindow();
    
    InvalidateRect(screen, NULL, true);
    RedrawWindow(screen, &rect, NULL, RDW_ERASE);
}

int main()

{
        int x_coord, y_coord, r, g, b;

        while(true) {
        
           
        if (GetKeyState(VK_CONTROL) & 0x8000) {
            
            cursorposition(x_coord, y_coord);
            getcolour(x_coord, y_coord, r, g, b);
            locate(x_coord, y_coord, r, g, b);
            //erase 
            getcolour(x_coord, y_coord, r, g, b);
            locate(x_coord, y_coord, 255-r, 255-g, 255-b);
            
            cout << "x:" << x_coord << " y:" << y_coord << "\n";
            cout << "R:" << r << " G:" << g << " B:" << b << "\n \n";
              
        }
        else
            continue;
    }
}


