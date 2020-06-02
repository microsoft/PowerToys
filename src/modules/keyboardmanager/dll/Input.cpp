#include "pch.h"
#include "Input.h"

// Function to simulate input
UINT Input::SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize)
{
    return SendInput(cInputs, pInputs, cbSize);
}
