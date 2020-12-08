#pragma once

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

void open_progressbar_window(HINSTANCE hInstance, const int n_progressbar_steps, const wchar_t* title, const wchar_t* init_label);
void tick_progressbar_window(const wchar_t* new_status);
void close_progressbar_window();