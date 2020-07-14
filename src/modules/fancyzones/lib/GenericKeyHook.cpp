#include "pch.h"
#include "GenericKeyHook.h"

HHOOK ShiftKeyHook::hHook{};
std::function<void(bool)> ShiftKeyHook::callback{};

HHOOK CtrlKeyHook::hHook{};
std::function<void(bool)> CtrlKeyHook::callback{};
