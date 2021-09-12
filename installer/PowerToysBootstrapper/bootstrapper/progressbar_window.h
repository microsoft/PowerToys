#pragma once

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

void OpenProgressBarDialog(HINSTANCE hInstance, const int nProgressbarSteps, const wchar_t* title, const wchar_t* label);
void UpdateProgressBarDialog(const wchar_t* label);
void CloseProgressBarDialog();
