#include "pch.h"
#include <Windows.h>
#include <common/utils/window.h>

#include "shortcut_guide.h"
#include "target_state.h"

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();

    instance = new OverlayWindow();
    instance->enable();

    run_message_loop();
    if (instance)
    {
        delete instance;
    }
}
