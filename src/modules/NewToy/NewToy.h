#pragma once
#include "Settings.h"

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) INewToy : public IUnknown
{
    IFACEMETHOD_(void, Run)
    () = 0;
    IFACEMETHOD_(void, Destroy)
    () = 0;
    IFACEMETHOD_(bool, OnKeyDown)
    (PKBDLLHOOKSTRUCT info, WPARAM keystate) = 0;
    IFACEMETHOD_(void, HotkeyChanged)
    () = 0;
};

struct NewToyCOM : public winrt::implements<NewToyCOM, INewToy>
{
public:
    NewToyCOM(HINSTANCE hinstance, ModuleSettings* settings) noexcept :
        m_hinstance(hinstance), m_settings(settings) {}
    // INewToy methods
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;
    IFACEMETHODIMP_(bool)
    OnKeyDown(PKBDLLHOOKSTRUCT info, WPARAM keystate) noexcept;
    IFACEMETHODIMP_(void)
    HotkeyChanged() noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:

    const HINSTANCE m_hinstance{};
    ModuleSettings* m_settings;
    mutable std::shared_mutex m_lock;
    HWND m_window{};
    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    bool isWindowShown = false;
    LPCWSTR titleText = L"Boring Toy";
    LPCWSTR windowText = L"Hello World, check out this boring power toy!";
    bool isSwapTriggered = false;
};

winrt::com_ptr<INewToy> MakeNewToy(HINSTANCE hinstance, ModuleSettings* settings) noexcept;