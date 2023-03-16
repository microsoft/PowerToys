#pragma once

#include "targetver.h"

#include <atlbase.h>

// Headers for CppUnitTest

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)
