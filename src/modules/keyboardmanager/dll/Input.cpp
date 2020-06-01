#include "pch.h"
#include "Input.h"

UINT Input::SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize)
{
    return SendInput(cInputs, pInputs, cbSize);
}
