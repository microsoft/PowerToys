#pragma once
#include "pch.h"
#include "common/dpi_aware.h"
#include "common/on_thread_executor.h"

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) INewToy : public IUnknown
{
    IFACEMETHOD_(void, Run)
    () = 0;
    IFACEMETHOD_(void, Destroy)
    () = 0;
    IFACEMETHOD_(bool, OnKeyDown)
    (PKBDLLHOOKSTRUCT info) = 0;
};

struct NewToyCOM : public winrt::implements<NewToyCOM, INewToy>
{
public:
    NewToyCOM(HINSTANCE hinstance) noexcept :
        m_hinstance(hinstance) {}
    // INewToy methods
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;
    IFACEMETHODIMP_(bool)
    OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:

    const HINSTANCE m_hinstance{};
    mutable std::shared_mutex m_lock;
    HWND m_window{};
    OnThreadExecutor m_dpiUnawareThread;
    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
};

winrt::com_ptr<INewToy> MakeNewToy(HINSTANCE hinstance) noexcept;