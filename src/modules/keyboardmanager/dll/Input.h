#pragma once
#include "InputInterface.h"

// Class used to wrap keyboard input library methods
class Input :
    public InputInterface
{
public:
    // Function to simulate input
    UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize);
};
