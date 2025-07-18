#pragma once
#include <robmikh.common/DesktopWindow.h>
#include "CropAndLockWindow.h"

struct ScreenshotCropAndLockWindow : robmikh::common::desktop::DesktopWindow<ScreenshotCropAndLockWindow>, CropAndLockWindow
{
    static const std::wstring ClassName;
    ScreenshotCropAndLockWindow(std::wstring const& titleString, int width, int height);
    ~ScreenshotCropAndLockWindow() override;
    LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);

    HWND Handle() override { return m_window; }
    void CropAndLock(HWND windowToCrop, RECT cropRect) override;
    void OnClosed(std::function<void(HWND)> callback) override { m_closedCallback = callback; }

private:
    static void RegisterWindowClass();

    void Hide();

private:
    std::unique_ptr<void, decltype(&DeleteObject)> m_bitmap{ nullptr, &DeleteObject };
    RECT m_destRect = {};
    RECT m_sourceRect = {};

    bool m_captured = false;
    bool m_destroyed = false;
    std::function<void(HWND)> m_closedCallback;
};