#pragma once
#include "windows.h"

// Interface used to wrap keyboard input library methods
class InputInterface
{
public:
    // Function to simulate input
    virtual UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize) = 0;
};
