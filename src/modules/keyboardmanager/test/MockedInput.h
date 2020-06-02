#pragma once
#include <keyboardmanager/dll/InputInterface.h>

class MockedInput :
    public InputInterface
{
public:
    UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize);
};
