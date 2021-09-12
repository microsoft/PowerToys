#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

HRESULT GetIconIndexFromPath(_In_ PCWSTR path, _Out_ int* index);
HBITMAP CreateBitmapFromIcon(_In_ HICON hIcon, _In_opt_ UINT width = 0, _In_opt_ UINT height = 0);