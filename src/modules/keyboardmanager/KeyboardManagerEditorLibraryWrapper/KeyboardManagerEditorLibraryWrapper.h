#pragma once

#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/Input.h>
#include <keyboardmanager/common/MappingConfiguration.h>

struct KeyNamePair
{
    int keyCode;
    wchar_t keyName[64];
};

extern "C" __declspec(dllexport) bool CheckIfRemappingsAreValid();
extern "C" __declspec(dllexport) int GetKeyboardKeysList(bool isShortcut, KeyNamePair* keyList, int maxCount);
