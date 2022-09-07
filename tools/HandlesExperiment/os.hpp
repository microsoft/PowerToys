#ifndef UNICODE
#define UNICODE
#endif

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <winternl.h>

#define STATUS_INFO_LENGTH_MISMATCH ((LONG)0xC00000004)
