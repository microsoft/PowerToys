#include "pch.h"
#include "Input.h"

// Function to simulate input
UINT Input::SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize)
{
    return SendInput(cInputs, pInputs, cbSize);
}

// Function to get the state of a particular key
bool Input::GetVirtualKeyState(int key)
{
    return (GetAsyncKeyState(key) & 0x8000);
}
