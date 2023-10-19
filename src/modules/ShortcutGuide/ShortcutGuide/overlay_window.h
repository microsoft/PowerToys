#pragma once
#include "animation.h"
#include "d2d_svg.h"
#include "d2d_window.h"
#include "d2d_text.h"

#include <common/display/monitors.h>
#include <common/themes/windows_colors.h>
#include "tasklist_positions.h"

struct ScaleResult
{
    double scale;
    RECT rect;
};

class D2DOverlaySVG : public D2DSVG
{
public:
    D2DOverlaySVG& load(const std::wstring& filename, ID2D1DeviceContext5* d2d_dc);
    D2DOverlaySVG& resize(int x, int y, int width, int height, float fill, float max_scale = -1.0f);
    D2DOverlaySVG& find_thumbnail(const std::wstring& id);
    D2DOverlaySVG& find_window_group(const std::wstring& id);
    ScaleResult get_thumbnail_rect_and_scale(int x_offset, int y_offset, int window_cx, int window_cy, float fill);
    D2DOverlaySVG& toggle_window_group(bool active);
    winrt::com_ptr<ID2D1SvgElement> find_element(const std::wstring& id);
    D2D1_RECT_F get_maximize_label() const;
    D2D1_RECT_F get_minimize_label() const;
    D2D1_RECT_F get_snap_left() const;
    D2D1_RECT_F get_snap_right() const;

private:
    D2D1_POINT_2F thumbnail_top_left = {};
    D2D1_POINT_2F thumbnail_bottom_right = {};
    RECT thumbnail_scaled_rect = {};
    winrt::com_ptr<ID2D1SvgElement> window_group;
};

struct AnimateKeys
{
    Animation animation;
    D2D1_COLOR_F original;
    winrt::com_ptr<ID2D1SvgElement> button;
    int vk_code;
};

class D2DOverlayWindow : public D2DWindow
{
public:
    D2DOverlayWindow();
    void show(HWND window, bool snappable);
    ~D2DOverlayWindow();
    void apply_overlay_opacity(float opacity);
    void apply_press_time_for_global_windows_shortcuts(int press_time);
    void apply_press_time_for_taskbar_icon_shortcuts(int press_time);
    void set_theme(const std::wstring& theme);
    void quick_hide();

    HWND get_window_handle();
    void SetWindowCloseType(std::wstring wCloseType)
    {
        windowCloseType = wCloseType;
    }

private:
    std::wstring windowCloseType;
    bool show_thumbnail(const RECT& rect, double alpha);
    void hide_thumbnail();
    virtual void init() override;
    virtual void resize() override;
    virtual void render(ID2D1DeviceContext5* d2dd2d_device_context_dc) override;
    virtual void on_show() override;
    virtual void on_hide() override;
    float get_overlay_opacity();

    bool running = true;
    std::vector<AnimateKeys> key_animations;
    std::vector<MonitorInfo> monitors;
    Box total_screen;
    int monitor_dx = 0, monitor_dy = 0;
    D2DText text;
    WindowsColors colors;
    Animation background_animation;
    Animation global_windows_shortcuts_animation;
    Animation taskbar_icon_shortcuts_animation;
    bool global_windows_shortcuts_shown = false;
    bool taskbar_icon_shortcuts_shown = false;
    RECT window_rect = {};
    Tasklist tasklist;
    std::vector<TasklistButton> tasklist_buttons;
    std::thread tasklist_thread;
    bool tasklist_update = false;
    std::mutex tasklist_cv_mutex;
    std::condition_variable tasklist_cv;

    HTHUMBNAIL thumbnail = nullptr;
    HWND active_window = nullptr;
    bool active_window_snappable = false;
    D2DOverlaySVG landscape, portrait;
    D2DOverlaySVG* use_overlay = nullptr;
    D2DSVG no_active;
    std::vector<D2DSVG> arrows;
    std::chrono::steady_clock::time_point shown_start_time;
    float overlay_opacity = 0.9f;
    enum
    {
        Light,
        Dark,
        System
    } theme_setting = System;
    bool light_mode = true;
    UINT milliseconds_press_time_for_global_windows_shortcuts = 900;
    UINT milliseconds_press_time_for_taskbar_icon_shortcuts = 900;
};
