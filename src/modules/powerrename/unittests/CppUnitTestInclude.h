#pragma once
// Headers for CppUnitTest

// Visual Studio 18's CppUnitTest headers use SAL annotations without
// including their compatibility definitions themselves.
#ifndef _When_
#define _When_(expr, annos)
#endif
#ifndef _Analysis_NoReturn_
#define _Analysis_NoReturn_
#endif

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)
