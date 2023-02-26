#pragma once
#include "Generated Files/resource.h"
#ifndef GET_RESOURCE_STRING
#include <utils/resources.h>
#endif // !WIN32_LEAN_AND_MEAN



std::wstring GetLocalisation(int key) {
    return GET_RESOURCE_STRING(key);
}