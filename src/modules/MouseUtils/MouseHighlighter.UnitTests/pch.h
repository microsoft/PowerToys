// pch.h: precompiled header for the Input Highlighter keystroke unit tests.
#ifndef PCH_H
#define PCH_H

#include <Windows.h>

#include <array>
#include <string>
#include <vector>

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#endif // PCH_H
