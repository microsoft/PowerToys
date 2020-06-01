#pragma once
#include "InputInterface.h"
class Input :
    public InputInterface
{
public:
    UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize);
};
