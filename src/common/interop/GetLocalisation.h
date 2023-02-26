#pragma once
#include "Generated Files/resource.h"
#include <../utils/resources.h>

std::wstring GetLocalisation(int key) {
    return GET_RESOURCE_STRING(key);
}