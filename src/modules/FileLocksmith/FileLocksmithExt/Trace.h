#pragma once

#include "pch.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void EnableFileLocksmith(_In_ bool enabled) noexcept;
    static void Invoked() noexcept;
    static void InvokedRet(_In_ HRESULT hr) noexcept;
    static void QueryContextMenuError(_In_ HRESULT hr) noexcept;
};
