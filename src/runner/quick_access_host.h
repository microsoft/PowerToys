#pragma once

#include <Windows.h>
#include <optional>

namespace QuickAccessHost
{
    void start();
    void show(const POINT& position);
    void stop();
    bool is_running();
}
