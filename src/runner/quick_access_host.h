#pragma once

#include <Windows.h>
#include <optional>

namespace QuickAccessHost
{
    void start();
    void show();
    void hide();
    bool is_visible();
    void stop();
    bool is_running();
}
