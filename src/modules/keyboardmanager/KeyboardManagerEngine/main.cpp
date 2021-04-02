#include "pch.h"
#include <common/utils/window.h>
#include "KeyboardManager.h"

using namespace winrt;
using namespace Windows::Foundation;


int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    init_apartment();
    auto kbm = KeyboardManager();
    kbm.start_lowlevel_keyboard_hook();
    run_message_loop();
    kbm.stop_lowlevel_keyboard_hook();
}