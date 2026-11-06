#include "pch.h"
#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// Utility.cpp also references settings-dialog state and callbacks. Window tests
// use its real geometry and DPI helpers; entering a dialog callback is an error.
DWORD g_ThemeOverride = 2;

[[noreturn]] LRESULT GroupBoxSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}

[[noreturn]] LRESULT StaticTextSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}

[[noreturn]] LRESULT HotkeyControlSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}

[[noreturn]] LRESULT EditControlSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}

[[noreturn]] LRESULT CheckboxSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}

[[noreturn]] LRESULT SliderSubclassProc( HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR )
{
    Assert::Fail( L"Unexpected settings-dialog callback in a window test." );
}
