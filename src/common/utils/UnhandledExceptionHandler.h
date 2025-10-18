#pragma once

#include <Windows.h>

LONG WINAPI UnhandledExceptionHandler(PEXCEPTION_POINTERS info);
void AbortHandler(int signal_number);
void InitSymbols();
void InitUnhandledExceptionHandler();
