#pragma once
#include <vector>
#include <unordered_set>
#include <string>
#include <Windows.h>
#include <UIAutomationClient.h>

struct TasklistButton
{
    wchar_t name[256];
    int x;
    int y;
    int width;
    int height;
    int keynum;
};
