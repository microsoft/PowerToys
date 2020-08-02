#include "pch.h"
#include "Input.h"
#include <keyboardmanager/common/Helpers.h>

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

// Function to get the foreground process name
void Input::GetForegroundProcess(_Out_ std::wstring& foregroundProcess)
{
    foregroundProcess = KeyboardManagerHelper::GetCurrentApplication(false);
}
