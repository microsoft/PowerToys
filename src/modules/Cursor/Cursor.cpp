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

void locate(int x_coord, int y_coord, int r, int g, int b)
{
    COLORREF color;
    HDC hDC;

    // Get the device context for the screen
    hDC = GetDC(NULL);
    if (hDC == NULL)
        return;

    color = RGB(255 - r, 255 - g, 255 - b);
    for (int x = x_coord - 100; x < x_coord + 100; x = x + 1)
    {
        SetPixel(hDC, x, y_coord, color);
    }

        for (int y = y_coord - 100; y < y_coord + 100; y = y + 1)
        {
            SetPixel(hDC, x_coord, y, color);

        }

    
    
    ReleaseDC(GetDesktopWindow(), hDC);
}



int main()

{
        int x_coord, y_coord, r, g, b;

        while(true) {
        
           
        if (GetKeyState(VK_CONTROL) & 0x8000) {
            
            cursorposition(x_coord, y_coord);
            getcolour(x_coord, y_coord, r, g, b);
            locate(x_coord, y_coord, r, g, b);
            
            cout << "x:" << x_coord << " y:" << y_coord << "\n";
            cout << "R:" << r << " G:" << g << " B:" << b << "\n \n";
              
        }
        else
            continue;
    }
}


