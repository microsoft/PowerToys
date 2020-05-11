// Cursor.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <windows.h>
#include <cstdlib>
#include <cstdio>
#include <stdio.h>
#include <csignal>

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


int main()

{
        int x_coord, y_coord;

        while(true) {
        
        if (GetKeyState(VK_CONTROL) & 0x8000) {
            
            cursorposition(x_coord, y_coord);
            
            cout << "x:" << x_coord << " y:" << y_coord << "\n";
        }
        else
            continue;
    }
}


