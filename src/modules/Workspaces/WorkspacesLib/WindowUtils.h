#pragma once

#include <functional>

#include <WorkspacesLib/AppUtils.h>
#include <wtypes.h>

namespace Utils
{
    std::wstring GetAUMIDFromWindow(HWND hWnd);
    std::wstring GetAUMIDFromProcessId(DWORD processId);
};
