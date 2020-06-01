#pragma once
#include "windows.h"

class InputInterface
{
public:
    virtual UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize) = 0;
};
