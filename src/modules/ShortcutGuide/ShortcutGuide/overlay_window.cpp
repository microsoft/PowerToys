#include "pch.h"
#include "overlay_window.h"
#include <common/display/monitors.h>
#include "tasklist_positions.h"
#include "start_visible.h"
#include <common/utils/resources.h>
#include <common/utils/window.h>
#include <common/utils/MsWindowsSettings.h>

#include "shortcut_guide.h"
#include "trace.h"
#include "Generated Files/resource.h"

namespace
{
    // Gets position of given window.
    std::optional<RECT> get_window_pos(HWND hwnd)
    {
        RECT window;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &window, sizeof(window)) == S_OK)
        {
            return window;
        }
        else
        {
            return {};
        }
    }

    enum WindowState
    {
        UNKNOWN,
        MINIMIZED,
        MAXIMIZED,
        SNAPPED_TOP_LEFT,
        SNAPPED_LEFT,
        SNAPPED_BOTTOM_LEFT,
        SNAPPED_TOP_RIGHT,
        SNAPPED_RIGHT,
        SNAPPED_BOTTOM_RIGHT,
        RESTORED
    };

    inline WindowState get_window_state(HWND hwnd)
    {
        WINDOWPLACEMENT placement;
        placement.length = sizeof(WINDOWPLACEMENT);

        if (GetWindowPlacement(hwnd, &placement) == 0)
        {
            return UNKNOWN;
        }

        if (placement.showCmd == SW_MINIMIZE || placement.showCmd == SW_SHOWMINIMIZED || IsIconic(hwnd))
        {
            return MINIMIZED;
        }

        if (placement.showCmd == SW_MAXIMIZE || placement.showCmd == SW_SHOWMAXIMIZED)
        {
            return MAXIMIZED;
        }

        auto rectp = get_window_pos(hwnd);
        if (!rectp)
        {
            return UNKNOWN;
        }

        auto rect = *rectp;
        MONITORINFO monitor;
        monitor.cbSize = sizeof(MONITORINFO);
        auto h_monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        GetMonitorInfo(h_monitor, &monitor);
        bool top_left = monitor.rcWork.top == rect.top && monitor.rcWork.left == rect.left;
        bool bottom_left = monitor.rcWork.bottom == rect.bottom && monitor.rcWork.left == rect.left;
        bool top_right = monitor.rcWork.top == rect.top && monitor.rcWork.right == rect.right;
        bool bottom_right = monitor.rcWork.bottom == rect.bottom && monitor.rcWork.right == rect.right;

        if (top_left && bottom_left)
            return SNAPPED_LEFT;
        if (top_left)
            return SNAPPED_TOP_LEFT;
        if (bottom_left)
            return SNAPPED_BOTTOM_LEFT;
        if (top_right && bottom_right)
            return SNAPPED_RIGHT;
        if (top_right)
            return SNAPPED_TOP_RIGHT;
        if (bottom_right)
            return SNAPPED_BOTTOM_RIGHT;

        return RESTORED;
    }

}

D2DOverlaySVG& D2DOverlaySVG::load(const std::wstring& filename, ID2D1DeviceContext5* d2d_dc)
{
    D2DSVG::load(filename, d2d_dc);
    window_group = nullptr;
    thumbnail_top_left = {};
    thumbnail_bottom_right = {};
    thumbnail_scaled_rect = {};
    return *this;
}

D2DOverlaySVG& D2DOverlaySVG::resize(int x, int y, int width, int height, float fill, float max_scale)
{
    D2DSVG::resize(x, y, width, height, fill, max_scale);
    if (thumbnail_bottom_right.x != 0 && thumbnail_bottom_right.y != 0)
    {
        auto scaled_top_left = transform.TransformPoint(thumbnail_top_left);
        auto scanled_bottom_right = transform.TransformPoint(thumbnail_bottom_right);
        thumbnail_scaled_rect.left = static_cast<int>(scaled_top_left.x);
        thumbnail_scaled_rect.top = static_cast<int>(scaled_top_left.y);
        thumbnail_scaled_rect.right = static_cast<int>(scanled_bottom_right.x);
        thumbnail_scaled_rect.bottom = static_cast<int>(scanled_bottom_right.y);
    }
    return *this;
}

D2DOverlaySVG& D2DOverlaySVG::find_thumbnail(const std::wstring& id)
{
    winrt::com_ptr<ID2D1SvgElement> thumbnail_box;
    winrt::check_hresult(svg->FindElementById(id.c_str(), thumbnail_box.put()));
    winrt::check_hresult(thumbnail_box->GetAttributeValue(L"x", &thumbnail_top_left.x));
    winrt::check_hresult(thumbnail_box->GetAttributeValue(L"y", &thumbnail_top_left.y));
    winrt::check_hresult(thumbnail_box->GetAttributeValue(L"width", &thumbnail_bottom_right.x));
    thumbnail_bottom_right.x += thumbnail_top_left.x;
    winrt::check_hresult(thumbnail_box->GetAttributeValue(L"height", &thumbnail_bottom_right.y));
    thumbnail_bottom_right.y += thumbnail_top_left.y;
    return *this;
}

D2DOverlaySVG& D2DOverlaySVG::find_window_group(const std::wstring& id)
{
    window_group = nullptr;
    winrt::check_hresult(svg->FindElementById(id.c_str(), window_group.put()));
    return *this;
}

ScaleResult D2DOverlaySVG::get_thumbnail_rect_and_scale(int x_offset, int y_offset, int window_cx, int window_cy, float fill)
{
    if (thumbnail_bottom_right.x == 0 && thumbnail_bottom_right.y == 0)
    {
        return {};
    }
    int thumbnail_scaled_rect_width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
    int thumbnail_scaled_rect_heigh = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
    if (thumbnail_scaled_rect_heigh == 0 || thumbnail_scaled_rect_width == 0 ||
        window_cx == 0 || window_cy == 0)
    {
        return {};
    }
    float scale_h = fill * thumbnail_scaled_rect_width / window_cx;
    float scale_v = fill * thumbnail_scaled_rect_heigh / window_cy;
    float use_scale = std::min(scale_h, scale_v);
    RECT thumb_rect;
    thumb_rect.left = thumbnail_scaled_rect.left + static_cast<int>(thumbnail_scaled_rect_width - use_scale * window_cx) / 2 + x_offset;
    thumb_rect.right = thumbnail_scaled_rect.right - static_cast<int>(thumbnail_scaled_rect_width - use_scale * window_cx) / 2 + x_offset;
    thumb_rect.top = thumbnail_scaled_rect.top + static_cast<int>(thumbnail_scaled_rect_heigh - use_scale * window_cy) / 2 + y_offset;
    thumb_rect.bottom = thumbnail_scaled_rect.bottom - static_cast<int>(thumbnail_scaled_rect_heigh - use_scale * window_cy) / 2 + y_offset;
    ScaleResult result;
    result.scale = use_scale;
    result.rect = thumb_rect;
    return result;
}

winrt::com_ptr<ID2D1SvgElement> D2DOverlaySVG::find_element(const std::wstring& id)
{
    winrt::com_ptr<ID2D1SvgElement> element;
    winrt::check_hresult(svg->FindElementById(id.c_str(), element.put()));
    return element;
}

D2DOverlaySVG& D2DOverlaySVG::toggle_window_group(bool active)
{
    if (window_group)
    {
        window_group->SetAttributeValue(L"fill-opacity", active ? 1.0f : 0.3f);
    }
    return *this;
}

D2D1_RECT_F D2DOverlaySVG::get_maximize_label() const
{
    D2D1_RECT_F result;
    auto height = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
    auto width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
    if (width >= height)
    {
        result.top = thumbnail_scaled_rect.bottom + height * 0.210f;
        result.bottom = thumbnail_scaled_rect.bottom + height * 0.310f;
        result.left = thumbnail_scaled_rect.left + width * 0.009f;
        result.right = thumbnail_scaled_rect.right + width * 0.009f;
    }
    else
    {
        result.top = thumbnail_scaled_rect.top + height * 0.323f;
        result.bottom = thumbnail_scaled_rect.top + height * 0.398f;
        result.left = static_cast<float>(thumbnail_scaled_rect.right);
        result.right = thumbnail_scaled_rect.right + width * 1.45f;
    }
    return result;
}
D2D1_RECT_F D2DOverlaySVG::get_minimize_label() const
{
    D2D1_RECT_F result;
    auto height = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
    auto width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
    if (width >= height)
    {
        result.top = thumbnail_scaled_rect.bottom + height * 0.8f;
        result.bottom = thumbnail_scaled_rect.bottom + height * 0.9f;
        result.left = thumbnail_scaled_rect.left + width * 0.009f;
        result.right = thumbnail_scaled_rect.right + width * 0.009f;
    }
    else
    {
        result.top = thumbnail_scaled_rect.top + height * 0.725f;
        result.bottom = thumbnail_scaled_rect.top + height * 0.800f;
        result.left =static_cast<float>(thumbnail_scaled_rect.right);
        result.right = thumbnail_scaled_rect.right + width * 1.45f;
    }
    return result;
}
D2D1_RECT_F D2DOverlaySVG::get_snap_left() const
{
    D2D1_RECT_F result;
    auto height = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
    auto width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
    if (width >= height)
    {
        result.top = thumbnail_scaled_rect.bottom + height * 0.5f;
        result.bottom = thumbnail_scaled_rect.bottom + height * 0.6f;
        result.left = thumbnail_scaled_rect.left + width * 0.009f;
        result.right = thumbnail_scaled_rect.left + width * 0.339f;
    }
    else
    {
        result.top = thumbnail_scaled_rect.top + height * 0.523f;
        result.bottom = thumbnail_scaled_rect.top + height * 0.598f;
        result.left = static_cast<float>(thumbnail_scaled_rect.right);
        result.right = thumbnail_scaled_rect.right + width * 0.450f;
    }
    return result;
}
D2D1_RECT_F D2DOverlaySVG::get_snap_right() const
{
    D2D1_RECT_F result;
    auto height = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
    auto width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
    if (width >= height)
    {
        result.top = thumbnail_scaled_rect.bottom + height * 0.5f;
        result.bottom = thumbnail_scaled_rect.bottom + height * 0.6f;
        result.left = thumbnail_scaled_rect.left + width * 0.679f;
        result.right = thumbnail_scaled_rect.right + width * 1.009f;
    }
    else
    {
        result.top = thumbnail_scaled_rect.top + height * 0.523f;
        result.bottom = thumbnail_scaled_rect.top + height * 0.598f;
        result.left = static_cast<float>(thumbnail_scaled_rect.right + width);
        result.right = thumbnail_scaled_rect.right + width * 1.45f;
    }
    return result;
}

D2DOverlayWindow::D2DOverlayWindow() :
    total_screen({}),
    D2DWindow()
{
    BOOL isEnabledAnimations = GetAnimationsEnabled();
    background_animation = isEnabledAnimations? 0.3f : 0.f;
    global_windows_shortcuts_animation = isEnabledAnimations ? 0.3f : 0.f;
    taskbar_icon_shortcuts_animation = isEnabledAnimations ? 0.3f : 0.f;
    tasklist_thread = std::thread([&] {
        while (running)
        {
            // Removing <std::mutex> causes C3538 on std::unique_lock lock(mutex); in show(..)
            std::unique_lock<std::mutex> task_list_lock(tasklist_cv_mutex);
            tasklist_cv.wait(task_list_lock, [&] { return !running || tasklist_update; });
            if (!running)
                return;
            task_list_lock.unlock();
            while (running && tasklist_update)
            {
                std::vector<TasklistButton> buttons;
                if (tasklist.update_buttons(buttons))
                {
                    std::unique_lock lock(mutex);
                    tasklist_buttons.swap(buttons);
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(500));
            }
        }
    });
}

void D2DOverlayWindow::show(HWND window, bool snappable)
{
    std::unique_lock lock(mutex);
    hidden = false;
    tasklist_buttons.clear();
    active_window = window;
    active_window_snappable = snappable;
    auto old_bck = colors.start_color_menu;
    auto colors_updated = colors.update();
    auto new_light_mode = (theme_setting == Light) || (theme_setting == System && colors.light_mode);
    if (initialized && (colors_updated || light_mode != new_light_mode))
    {
        // update background colors
        landscape.recolor(old_bck, colors.start_color_menu);
        portrait.recolor(old_bck, colors.start_color_menu);
        for (auto& arrow : arrows)
        {
            arrow.recolor(old_bck, colors.start_color_menu);
        }
        light_mode = new_light_mode;
        if (light_mode)
        {
            landscape.recolor(0xDDDDDD, 0x222222);
            portrait.recolor(0xDDDDDD, 0x222222);
            for (auto& arrow : arrows)
            {
                arrow.recolor(0xDDDDDD, 0x222222);
            }
        }
        else
        {
            landscape.recolor(0x222222, 0xDDDDDD);
            portrait.recolor(0x222222, 0xDDDDDD);
            for (auto& arrow : arrows)
            {
                arrow.recolor(0x222222, 0xDDDDDD);
            }
        }
    }
    monitors = MonitorInfo::GetMonitors(true);
    // calculate the rect covering all the screens
    total_screen = monitors[0].GetScreenSize(true);
    for (auto& monitor : monitors)
    {
        const auto monitorSize = monitor.GetScreenSize(true);
        total_screen.rect.left = std::min(total_screen.left(), monitorSize.left());
        total_screen.rect.top = std::min(total_screen.top(), monitorSize.top());
        total_screen.rect.right = std::max(total_screen.right(), monitorSize.right());
        total_screen.rect.bottom = std::max(total_screen.bottom(), monitorSize.bottom());
    }
    // make sure top-right corner of all the monitor rects is (0,0)
    monitor_dx = -total_screen.left();
    monitor_dy = -total_screen.top();
    total_screen.rect.left += monitor_dx;
    total_screen.rect.right += monitor_dx;
    total_screen.rect.top += monitor_dy;
    total_screen.rect.bottom += monitor_dy;
    tasklist.update();
    if (window)
    {
        // Ignore errors, if this fails we will just not show the thumbnail
        DwmRegisterThumbnail(hwnd, window, &thumbnail);
    }

    background_animation.reset();

    if (milliseconds_press_time_for_global_windows_shortcuts < milliseconds_press_time_for_taskbar_icon_shortcuts)
    {
        global_windows_shortcuts_shown = true;
        taskbar_icon_shortcuts_shown = false;
        global_windows_shortcuts_animation.reset();
    }
    else if (milliseconds_press_time_for_global_windows_shortcuts > milliseconds_press_time_for_taskbar_icon_shortcuts)
    {
        global_windows_shortcuts_shown = false;
        taskbar_icon_shortcuts_shown = true;
        taskbar_icon_shortcuts_animation.reset();
    }
    else
    {
        global_windows_shortcuts_shown = true;
        taskbar_icon_shortcuts_shown = true;
        global_windows_shortcuts_animation.reset();
        taskbar_icon_shortcuts_animation.reset();
    }

    auto primary_size = MonitorInfo::GetPrimaryMonitor().GetScreenSize(false);
    shown_start_time = std::chrono::steady_clock::now();
    lock.unlock();
    D2DWindow::show(primary_size.left(), primary_size.top(), primary_size.width(), primary_size.height());
    // Check if taskbar is auto-hidden. If so, don't display the number arrows
    APPBARDATA param = {};
    param.cbSize = sizeof(APPBARDATA);
    if (static_cast<UINT>(SHAppBarMessage(ABM_GETSTATE, &param)) != ABS_AUTOHIDE)
    {
        tasklist_cv_mutex.lock();
        tasklist_update = true;
        tasklist_cv_mutex.unlock();
        tasklist_cv.notify_one();
    }
}

void D2DOverlayWindow::on_show()
{
    // show override does everything
}

void D2DOverlayWindow::on_hide()
{
    Logger::trace("D2DOverlayWindow::on_hide()");
    tasklist_cv_mutex.lock();
    tasklist_update = false;
    tasklist_cv_mutex.unlock();
    tasklist_cv.notify_one();
    if (thumbnail)
    {
        DwmUnregisterThumbnail(thumbnail);
    }
    std::chrono::steady_clock::time_point shown_end_time = std::chrono::steady_clock::now();
    // Trace the event only if the overlay window was visible.
    if (shown_start_time.time_since_epoch().count() > 0)
    {
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(shown_end_time - shown_start_time).count();
        Logger::trace(L"Duration: {}. Close Type: {}", duration, windowCloseType);
        Trace::SendGuideSession(duration, windowCloseType.c_str());
        shown_start_time = {};
    }
}

D2DOverlayWindow::~D2DOverlayWindow()
{
    tasklist_cv_mutex.lock();
    running = false;
    tasklist_cv_mutex.unlock();
    tasklist_cv.notify_one();
    tasklist_thread.join();
}

void D2DOverlayWindow::apply_overlay_opacity(float opacity)
{
    if (opacity <= 0.0f)
    {
        opacity = 0.0f;
    }
    if (opacity >= 1.0f)
    {
        opacity = 1.0f;
    }
    overlay_opacity = opacity;
}

void D2DOverlayWindow::apply_press_time_for_global_windows_shortcuts(int press_time)
{
    milliseconds_press_time_for_global_windows_shortcuts = std::max(press_time, 0);
}

void D2DOverlayWindow::apply_press_time_for_taskbar_icon_shortcuts(int press_time)
{
    milliseconds_press_time_for_taskbar_icon_shortcuts = std::max(press_time, 0);
}

void D2DOverlayWindow::set_theme(const std::wstring& theme)
{
    if (theme == L"light")
    {
        theme_setting = Light;
    }
    else if (theme == L"dark")
    {
        theme_setting = Dark;
    }
    else
    {
        theme_setting = System;
    }
}

/* Hide the window but do not call on_hide(). Use this to quickly hide the window when needed.
   Note, that a proper hide should be made after this before showing the window again.
*/
void D2DOverlayWindow::quick_hide()
{
    ShowWindow(hwnd, SW_HIDE);
    if (thumbnail)
    {
        DwmUnregisterThumbnail(thumbnail);
    }
}

HWND D2DOverlayWindow::get_window_handle()
{
    return hwnd;
}

float D2DOverlayWindow::get_overlay_opacity()
{
    return overlay_opacity;
}

void D2DOverlayWindow::init()
{
    colors.update();
    landscape.load(L"Assets\\ShortcutGuide\\overlay.svg", d2d_dc.get())
        .find_thumbnail(L"monitorRect")
        .find_window_group(L"WindowControlsGroup")
        .recolor(0x2582FB, colors.start_color_menu);
    portrait.load(L"Assets\\ShortcutGuide\\overlay_portrait.svg", d2d_dc.get())
        .find_thumbnail(L"monitorRect")
        .find_window_group(L"WindowControlsGroup")
        .recolor(0x2582FB, colors.start_color_menu);
    no_active.load(L"Assets\\ShortcutGuide\\no_active_window.svg", d2d_dc.get());
    arrows.resize(10);
    for (unsigned i = 0; i < arrows.size(); ++i)
    {
        arrows[i].load(L"Assets\\ShortcutGuide\\" + std::to_wstring((i + 1) % 10) + L".svg", d2d_dc.get()).recolor(0x2582FB, colors.start_color_menu);
    }
    light_mode = (theme_setting == Light) || (theme_setting == System && colors.light_mode);
    if (light_mode)
    {
        landscape.recolor(0x2E17FC, 0x000000);
        portrait.recolor(0x2E17FC, 0x000000);
        for (auto& arrow : arrows)
        {
            arrow.recolor(0x222222, 0x000000);
        }
    }
    else
    {
        landscape.recolor(0x2E17FC, 0xFFFFFF);
        portrait.recolor(0x2E17FC, 0xFFFFFF);
        for (auto& arrow : arrows)
        {
            arrow.recolor(0x222222, 0xFFFFFF);
        }
    }
}

void D2DOverlayWindow::resize()
{
    window_rect = *get_window_pos(hwnd);
    float no_active_scale, font;
    if (window_width >= window_height)
    { // portrait is broke right now
        use_overlay = &landscape;
        no_active_scale = 0.3f;
        font = 12.0f;
    }
    else
    {
        use_overlay = &portrait;
        no_active_scale = 0.5f;
        font = 13.0f;
    }
    use_overlay->resize(0, 0, window_width, window_height, 0.8f);
    auto thumb_no_active_rect = use_overlay->get_thumbnail_rect_and_scale(0, 0, no_active.width(), no_active.height(), no_active_scale).rect;
    no_active.resize(thumb_no_active_rect.left,
                     thumb_no_active_rect.top,
                     thumb_no_active_rect.right - thumb_no_active_rect.left,
                     thumb_no_active_rect.bottom - thumb_no_active_rect.top,
                     1.0f);
    text.resize(font, use_overlay->get_scale());
}

void render_arrow(D2DSVG& arrow, TasklistButton& button, RECT window, float max_scale, ID2D1DeviceContext5* d2d_dc, int x_offset, int y_offset)
{
    int dx = 0, dy = 0;
    // Calculate taskbar orientation
    arrow.toggle_element(L"left", false);
    arrow.toggle_element(L"right", false);
    arrow.toggle_element(L"top", false);
    arrow.toggle_element(L"bottom", false);
    if (button.x <= window.left)
    { // taskbar on left
        dx = 1;
        arrow.toggle_element(L"left", true);
    }
    if (button.x >= window.right)
    { // taskbar on right
        dx = -1;
        arrow.toggle_element(L"right", true);
    }
    if (button.y <= window.top)
    { // taskbar on top
        dy = 1;
        arrow.toggle_element(L"top", true);
    }
    if (button.y >= window.bottom)
    { // taskbar on bottom
        dy = -1;
        arrow.toggle_element(L"bottom", true);
    }
    double arrow_ratio = static_cast<double>(arrow.height()) / arrow.width();
    if (dy != 0)
    {
        // assume button is 25% wider than taller, +10% to make room for each of the arrows that are hidden
        auto render_arrow_width = static_cast<int>(button.height * 1.25f * 1.2f);
        auto render_arrow_height = static_cast<int>(render_arrow_width * arrow_ratio);
        arrow.resize((button.x + (button.width - render_arrow_width) / 2) + x_offset,
                     (dy == -1 ? button.y - render_arrow_height : 0) + y_offset,
                     render_arrow_width,
                     render_arrow_height,
                     0.95f,
                     max_scale)
            .render(d2d_dc);
    }
    else
    {
        // same as above - make room for the hidden arrow
        auto render_arrow_height = static_cast<int>(button.height * 1.2f);
        auto render_arrow_width = static_cast<int>(render_arrow_height / arrow_ratio);
        arrow.resize((dx == -1 ? button.x - render_arrow_width : 0) + x_offset,
                     (button.y + (button.height - render_arrow_height) / 2) + y_offset,
                     render_arrow_width,
                     render_arrow_height,
                     0.95f,
                     max_scale)
            .render(d2d_dc);
    }
}

bool D2DOverlayWindow::show_thumbnail(const RECT& rect, double alpha)
{
    if (!thumbnail)
    {
        return false;
    }
    DWM_THUMBNAIL_PROPERTIES thumb_properties;
    thumb_properties.dwFlags = DWM_TNP_SOURCECLIENTAREAONLY | DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY;
    thumb_properties.fSourceClientAreaOnly = FALSE;
    thumb_properties.fVisible = TRUE;
    thumb_properties.opacity = static_cast<BYTE>(255 * alpha);
    thumb_properties.rcDestination = rect;
    if (DwmUpdateThumbnailProperties(thumbnail, &thumb_properties) != S_OK)
    {
        return false;
    }
    return true;
}

void D2DOverlayWindow::hide_thumbnail()
{
    DWM_THUMBNAIL_PROPERTIES thumb_properties;
    thumb_properties.dwFlags = DWM_TNP_VISIBLE;
    thumb_properties.fVisible = FALSE;
    DwmUpdateThumbnailProperties(thumbnail, &thumb_properties);
}

void D2DOverlayWindow::render(ID2D1DeviceContext5* d2d_device_context)
{
    if (!hidden && !overlay_window_instance->overlay_visible())
    {
        hide();
        return;
    }

    d2d_device_context->Clear();
    int taskbar_icon_shortcuts_x_offset = 0, taskbar_icon_shortcuts_y_offset = 0;

    double current_background_anim_value = background_animation.value(Animation::AnimFunctions::LINEAR);
    double current_global_windows_shortcuts_anim_value = global_windows_shortcuts_animation.value(Animation::AnimFunctions::LINEAR);
    double pos_global_windows_shortcuts_anim_value = 1 - global_windows_shortcuts_animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
    double pos_taskbar_icon_shortcuts_anim_value = 1 - taskbar_icon_shortcuts_animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);

    // Draw background
    SetLayeredWindowAttributes(hwnd, 0, static_cast<byte>(255 * current_background_anim_value), LWA_ALPHA);
    winrt::com_ptr<ID2D1SolidColorBrush> brush;
    float brush_opacity = get_overlay_opacity();
    D2D1_COLOR_F brushColor = light_mode ? D2D1::ColorF(1.0f, 1.0f, 1.0f, brush_opacity) : D2D1::ColorF(0, 0, 0, brush_opacity);
    winrt::check_hresult(d2d_device_context->CreateSolidColorBrush(brushColor, brush.put()));
    D2D1_RECT_F background_rect = {};
    background_rect.bottom = static_cast<float>(window_height);
    background_rect.right = static_cast<float>(window_width);
    d2d_device_context->SetTransform(D2D1::Matrix3x2F::Identity());
    d2d_device_context->FillRectangle(background_rect, brush.get());

    // Draw the taskbar shortcuts (the arrows with numbers)
    if (taskbar_icon_shortcuts_shown)
    {
        if (!tasklist_buttons.empty())
        {
            if (tasklist_buttons[0].x <= window_rect.left)
            {
                // taskbar on left
                taskbar_icon_shortcuts_x_offset = static_cast<int>(-pos_taskbar_icon_shortcuts_anim_value * use_overlay->width() * use_overlay->get_scale());
            }
            if (tasklist_buttons[0].x >= window_rect.right)
            {
                // taskbar on right
                taskbar_icon_shortcuts_x_offset = static_cast<int>(pos_taskbar_icon_shortcuts_anim_value * use_overlay->width() * use_overlay->get_scale());
            }
            if (tasklist_buttons[0].y <= window_rect.top)
            {
                // taskbar on top
                taskbar_icon_shortcuts_y_offset = static_cast<int>(-pos_taskbar_icon_shortcuts_anim_value * use_overlay->height() * use_overlay->get_scale());
            }
            if (tasklist_buttons[0].y >= window_rect.bottom)
            {
                // taskbar on bottom
                taskbar_icon_shortcuts_y_offset = static_cast<int>(pos_taskbar_icon_shortcuts_anim_value * use_overlay->height() * use_overlay->get_scale());
            }
            for (auto&& button : tasklist_buttons)
            {
                if (static_cast<size_t>(button.keynum) - 1 >= arrows.size())
                {
                    continue;
                }
                render_arrow(arrows[static_cast<size_t>(button.keynum) - 1], button, window_rect, use_overlay->get_scale(), d2d_device_context, taskbar_icon_shortcuts_x_offset, taskbar_icon_shortcuts_y_offset);
            }
        }
    }
    else
    {
        auto time_since_start = std::chrono::high_resolution_clock::now() - shown_start_time;
        if (time_since_start.count() / 1000000 > milliseconds_press_time_for_taskbar_icon_shortcuts - milliseconds_press_time_for_global_windows_shortcuts)
        {
            taskbar_icon_shortcuts_shown = true;
            taskbar_icon_shortcuts_animation.reset();
        }
    }

    if (global_windows_shortcuts_shown)
    {
        // Thumbnail logic:
        auto window_state = get_window_state(active_window);
        auto thumb_window = get_window_pos(active_window);
        if (!thumb_window.has_value())
        {
            thumb_window = RECT();
        }

        bool miniature_shown = active_window != nullptr && thumbnail != nullptr && thumb_window && window_state != MINIMIZED;
        RECT client_rect;
        if (thumb_window && GetClientRect(active_window, &client_rect))
        {
            int dx = ((thumb_window->right - thumb_window->left) - (client_rect.right - client_rect.left)) / 2;
            int dy = ((thumb_window->bottom - thumb_window->top) - (client_rect.bottom - client_rect.top)) / 2;
            thumb_window->left += dx;
            thumb_window->right -= dx;
            thumb_window->top += dy;
            thumb_window->bottom -= dy;
        }
        if (miniature_shown && thumb_window->right - thumb_window->left <= 0 || thumb_window->bottom - thumb_window->top <= 0)
        {
            miniature_shown = false;
        }
        bool render_monitors = true;
        auto total_monitor_with_screen = total_screen;
        if (thumb_window)
        {
            total_monitor_with_screen.rect.left = std::min(total_monitor_with_screen.rect.left, thumb_window->left + monitor_dx);
            total_monitor_with_screen.rect.top = std::min(total_monitor_with_screen.rect.top, thumb_window->top + monitor_dy);
            total_monitor_with_screen.rect.right = std::max(total_monitor_with_screen.rect.right, thumb_window->right + monitor_dx);
            total_monitor_with_screen.rect.bottom = std::max(total_monitor_with_screen.rect.bottom, thumb_window->bottom + monitor_dy);
        }
        // Only allow the new rect being slight bigger.
        if (total_monitor_with_screen.width() - total_screen.width() > (thumb_window->right - thumb_window->left) / 2 ||
            total_monitor_with_screen.height() - total_screen.height() > (thumb_window->bottom - thumb_window->top) / 2)
        {
            render_monitors = false;
        }
        if (window_state == MINIMIZED)
        {
            total_monitor_with_screen = total_screen;
        }
        auto rect_and_scale = use_overlay->get_thumbnail_rect_and_scale(0, 0, total_monitor_with_screen.width(), total_monitor_with_screen.height(), 1);
        if (miniature_shown)
        {
            RECT thumbnail_pos;
            if (render_monitors)
            {
                thumbnail_pos.left = static_cast<int>((thumb_window->left + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
                thumbnail_pos.top = static_cast<int>((thumb_window->top + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
                thumbnail_pos.right = static_cast<int>((thumb_window->right + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
                thumbnail_pos.bottom = static_cast<int>((thumb_window->bottom + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
            }
            else
            {
                thumbnail_pos = use_overlay->get_thumbnail_rect_and_scale(0, 0, thumb_window->right - thumb_window->left, thumb_window->bottom - thumb_window->top, 1).rect;
            }
            // If the animation is done show the thumbnail
            //   we cannot animate the thumbnail, the animation lags behind
            miniature_shown = show_thumbnail(thumbnail_pos, current_global_windows_shortcuts_anim_value);
        }
        else
        {
            hide_thumbnail();
        }
        if (window_state == MINIMIZED)
        {
            render_monitors = true;
        }
        // render the monitors
        if (render_monitors)
        {
            brushColor = D2D1::ColorF(colors.start_color_menu, miniature_shown ? static_cast<float>(current_global_windows_shortcuts_anim_value * 0.9) : static_cast<float>(current_global_windows_shortcuts_anim_value * 0.3));
            brush = nullptr;
            winrt::check_hresult(d2d_device_context->CreateSolidColorBrush(brushColor, brush.put()));
            for (auto& monitor : monitors)
            {
                D2D1_RECT_F monitor_rect;
                const auto monitor_size = monitor.GetScreenSize(true);
                monitor_rect.left = static_cast<float>((monitor_size.left() + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
                monitor_rect.top = static_cast<float>((monitor_size.top() + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
                monitor_rect.right = static_cast<float>((monitor_size.right() + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
                monitor_rect.bottom = static_cast<float>((monitor_size.bottom() + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
                d2d_device_context->SetTransform(D2D1::Matrix3x2F::Identity());
                d2d_device_context->FillRectangle(monitor_rect, brush.get());
            }
        }
        // Finalize the overlay - dimm the buttons if no thumbnail is present and show "No active window"
        use_overlay->toggle_window_group(miniature_shown || window_state == MINIMIZED);
        if (!miniature_shown && window_state != MINIMIZED)
        {
            no_active.render(d2d_device_context);
            window_state = UNKNOWN;
        }

        // Set the animation - move the draw window according to animation step
        int global_windows_shortcuts_y_offset = static_cast<int>(pos_global_windows_shortcuts_anim_value * use_overlay->height() * use_overlay->get_scale());
        auto popIn = D2D1::Matrix3x2F::Translation(0, static_cast<float>(global_windows_shortcuts_y_offset));
        d2d_device_context->SetTransform(popIn);

        // Animate keys
        for (unsigned id = 0; id < key_animations.size();)
        {
            auto& animation = key_animations[id];
            D2D1_COLOR_F color;
            auto value = static_cast<float>(animation.animation.value(Animation::AnimFunctions::EASE_OUT_EXPO));
            color.a = 1.0f;
            color.r = animation.original.r + (1.0f - animation.original.r) * value;
            color.g = animation.original.g + (1.0f - animation.original.g) * value;
            color.b = animation.original.b + (1.0f - animation.original.b) * value;
            animation.button->SetAttributeValue(L"fill", color);
            if (animation.animation.done())
            {
                if (value == 1)
                {
                    animation.animation.reset(0.05, 1, 0);
                    animation.animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
                }
                else
                {
                    key_animations.erase(key_animations.begin() + id);
                    continue;
                }
            }
            ++id;
        }
        // Finally: render the overlay...
        use_overlay->render(d2d_device_context);
        // ... window arrows texts ...
        std::wstring left, right, up, down;
        bool left_disabled = false;
        bool right_disabled = false;
        bool up_disabled = false;
        bool down_disabled = false;
        switch (window_state)
        {
        case MINIMIZED:
            left = GET_RESOURCE_STRING(IDS_NO_ACTION);
            left_disabled = true;
            right = GET_RESOURCE_STRING(IDS_NO_ACTION);
            right_disabled = true;
            up = GET_RESOURCE_STRING(IDS_RESTORE);
            down = GET_RESOURCE_STRING(IDS_NO_ACTION);
            down_disabled = true;
            break;
        case MAXIMIZED:
            left = GET_RESOURCE_STRING(IDS_SNAP_LEFT);
            right = GET_RESOURCE_STRING(IDS_SNAP_RIGHT);
            up = GET_RESOURCE_STRING(IDS_NO_ACTION);
            up_disabled = true;
            down = GET_RESOURCE_STRING(IDS_RESTORE);
            break;
        case SNAPPED_TOP_LEFT:
            left = GET_RESOURCE_STRING(IDS_SNAP_UPPER_RIGHT);
            right = GET_RESOURCE_STRING(IDS_SNAP_UPPER_RIGHT);
            up = GET_RESOURCE_STRING(IDS_MAXIMIZE);
            down = GET_RESOURCE_STRING(IDS_SNAP_LEFT);
            break;
        case SNAPPED_LEFT:
            left = GET_RESOURCE_STRING(IDS_SNAP_RIGHT);
            right = GET_RESOURCE_STRING(IDS_RESTORE);
            up = GET_RESOURCE_STRING(IDS_SNAP_UPPER_LEFT);
            down = GET_RESOURCE_STRING(IDS_SNAP_LOWER_LEFT);
            break;
        case SNAPPED_BOTTOM_LEFT:
            left = GET_RESOURCE_STRING(IDS_SNAP_LOWER_RIGHT);
            right = GET_RESOURCE_STRING(IDS_SNAP_LOWER_RIGHT);
            up = GET_RESOURCE_STRING(IDS_SNAP_LEFT);
            down = GET_RESOURCE_STRING(IDS_MINIMIZE);
            break;
        case SNAPPED_TOP_RIGHT:
            left = GET_RESOURCE_STRING(IDS_SNAP_UPPER_LEFT);
            right = GET_RESOURCE_STRING(IDS_SNAP_UPPER_LEFT);
            up = GET_RESOURCE_STRING(IDS_MAXIMIZE);
            down = GET_RESOURCE_STRING(IDS_SNAP_RIGHT);
            break;
        case SNAPPED_RIGHT:
            left = GET_RESOURCE_STRING(IDS_RESTORE);
            right = GET_RESOURCE_STRING(IDS_SNAP_LEFT);
            up = GET_RESOURCE_STRING(IDS_SNAP_UPPER_RIGHT);
            down = GET_RESOURCE_STRING(IDS_SNAP_LOWER_RIGHT);
            break;
        case SNAPPED_BOTTOM_RIGHT:
            left = GET_RESOURCE_STRING(IDS_SNAP_LOWER_LEFT);
            right = GET_RESOURCE_STRING(IDS_SNAP_LOWER_LEFT);
            up = GET_RESOURCE_STRING(IDS_SNAP_RIGHT);
            down = GET_RESOURCE_STRING(IDS_MINIMIZE);
            break;
        case RESTORED:
            left = GET_RESOURCE_STRING(IDS_SNAP_LEFT);
            right = GET_RESOURCE_STRING(IDS_SNAP_RIGHT);
            up = GET_RESOURCE_STRING(IDS_MAXIMIZE);
            down = GET_RESOURCE_STRING(IDS_MINIMIZE);
            break;
        default:
            left = GET_RESOURCE_STRING(IDS_NO_ACTION);
            left_disabled = true;
            right = GET_RESOURCE_STRING(IDS_NO_ACTION);
            right_disabled = true;
            up = GET_RESOURCE_STRING(IDS_NO_ACTION);
            up_disabled = true;
            down = GET_RESOURCE_STRING(IDS_NO_ACTION);
            down_disabled = true;
        }
        auto text_color = D2D1::ColorF(light_mode ? 0x222222 : 0xDDDDDD, active_window_snappable && (miniature_shown || window_state == MINIMIZED) ? 1.0f : 0.3f);
        use_overlay->find_element(L"KeyUpGroup")->SetAttributeValue(L"fill-opacity", up_disabled ? 0.3f : 1.0f);
        text.set_alignment_center().write(d2d_device_context, text_color, use_overlay->get_maximize_label(), up);
        use_overlay->find_element(L"KeyDownGroup")->SetAttributeValue(L"fill-opacity", down_disabled ? 0.3f : 1.0f);
        text.write(d2d_device_context, text_color, use_overlay->get_minimize_label(), down);
        use_overlay->find_element(L"KeyLeftGroup")->SetAttributeValue(L"fill-opacity", left_disabled ? 0.3f : 1.0f);
        text.set_alignment_right().write(d2d_device_context, text_color, use_overlay->get_snap_left(), left);
        use_overlay->find_element(L"KeyRightGroup")->SetAttributeValue(L"fill-opacity", right_disabled ? 0.3f : 1.0f);
        text.set_alignment_left().write(d2d_device_context, text_color, use_overlay->get_snap_right(), right);
    }
    else
    {
        auto time_since_start = std::chrono::high_resolution_clock::now() - shown_start_time;
        if (time_since_start.count() / 1000000 > milliseconds_press_time_for_global_windows_shortcuts - milliseconds_press_time_for_taskbar_icon_shortcuts)
        {
            global_windows_shortcuts_shown = true;
            global_windows_shortcuts_animation.reset();
        }
    }
}
