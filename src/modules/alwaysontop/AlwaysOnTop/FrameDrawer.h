#pragma once

#include <mutex>
#include <d2d1.h>
#include <dwrite.h>

class FrameDrawer
{
public:
    static std::unique_ptr<FrameDrawer> Create(HWND window);

    FrameDrawer(HWND window);
    FrameDrawer(FrameDrawer&& other);
    ~FrameDrawer();

    bool Init();

    void Show();
    void Hide();
    void SetBorderRect(RECT windowRect, COLORREF color, float thickness);

private:
    struct DrawableRect
    {
        D2D1_RECT_F rect;
        D2D1_COLOR_F borderColor;
        float thickness;
    };

    enum struct RenderResult
    {
        Ok,
        Failed,
    };

    static ID2D1Factory* GetD2DFactory();
    static IDWriteFactory* GetWriteFactory();
    static D2D1_COLOR_F ConvertColor(COLORREF color);
    static D2D1_RECT_F ConvertRect(RECT rect);
    RenderResult Render();
    void RenderLoop();

    HWND m_window = nullptr;
    ID2D1HwndRenderTarget* m_renderTarget = nullptr;

    std::mutex m_mutex;
    DrawableRect m_sceneRect;

    std::atomic<bool> m_abortThread = false;
    std::thread m_renderThread;
};