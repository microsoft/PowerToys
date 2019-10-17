#include "pch.h"
#include "overlay_window.h"
#include "common/monitors.h"
#include "common/tasklist_positions.h"
#include "common/start_visible.h"
#include "keyboard_state.h"
#include "shortcut_guide.h"
#include "trace.h"

D2DOverlaySVG& D2DOverlaySVG::load(const std::wstring& filename, ID2D1DeviceContext5* d2d_dc) {
  D2DSVG::load(filename, d2d_dc);
  window_group = nullptr;
  thumbnail_top_left = {};
  thumbnail_bottom_right = {};
  thumbnail_scaled_rect = {};
  return *this;
}

D2DOverlaySVG& D2DOverlaySVG::resize(int x, int y, int width, int height, float fill, float max_scale) {
  D2DSVG::resize(x, y, width, height, fill, max_scale);
  if (thumbnail_bottom_right.x != 0 && thumbnail_bottom_right.y != 0) {
    auto scaled_top_left = transform.TransformPoint(thumbnail_top_left);
    auto scanled_bottom_right = transform.TransformPoint(thumbnail_bottom_right);
    thumbnail_scaled_rect.left = (int)scaled_top_left.x;
    thumbnail_scaled_rect.top = (int)scaled_top_left.y;
    thumbnail_scaled_rect.right = (int)scanled_bottom_right.x;
    thumbnail_scaled_rect.bottom = (int)scanled_bottom_right.y;
  }
  return *this;
}

D2DOverlaySVG& D2DOverlaySVG::find_thumbnail(const std::wstring& id) {
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

D2DOverlaySVG& D2DOverlaySVG::find_window_group(const std::wstring& id) {
  window_group = nullptr;
  winrt::check_hresult(svg->FindElementById(id.c_str(), window_group.put()));
  return *this;
}

ScaleResult D2DOverlaySVG::get_thumbnail_rect_and_scale(int x_offset, int y_offset, int window_cx, int window_cy, float fill) {
  if (thumbnail_bottom_right.x == 0 && thumbnail_bottom_right.y == 0) {
    return {};
  }
  int thumbnail_scaled_rect_width = thumbnail_scaled_rect.right - thumbnail_scaled_rect.left;
  int thumbnail_scaled_rect_heigh = thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top;
  if (thumbnail_scaled_rect_heigh == 0 || thumbnail_scaled_rect_width == 0 ||
    window_cx == 0 || window_cy == 0) {
    return {};
  }
  float scale_h = fill * thumbnail_scaled_rect_width / window_cx;
  float scale_v = fill * thumbnail_scaled_rect_heigh / window_cy;
  float use_scale = min(scale_h, scale_v);
  RECT thumb_rect;
  thumb_rect.left = thumbnail_scaled_rect.left + (int)(thumbnail_scaled_rect_width - use_scale * window_cx) / 2 + x_offset;
  thumb_rect.right = thumbnail_scaled_rect.right - (int)(thumbnail_scaled_rect_width - use_scale * window_cx) / 2 + x_offset;
  thumb_rect.top = thumbnail_scaled_rect.top + (int)(thumbnail_scaled_rect_heigh - use_scale * window_cy) / 2 + y_offset;
  thumb_rect.bottom = thumbnail_scaled_rect.bottom - (int)(thumbnail_scaled_rect_heigh - use_scale * window_cy) / 2 + y_offset;
  ScaleResult result;
  result.scale = use_scale;
  result.rect = thumb_rect;
  return result;
}

winrt::com_ptr<ID2D1SvgElement> D2DOverlaySVG::find_element(const std::wstring& id) {
  winrt::com_ptr< ID2D1SvgElement> element;
  winrt::check_hresult(svg->FindElementById(id.c_str(), element.put()));
  return element;
}

D2DOverlaySVG& D2DOverlaySVG::toggle_window_group(bool active) {
  if (window_group) {
    window_group->SetAttributeValue(L"fill-opacity", active ? 1.0f : 0.3f);
  }
  return *this;
}

D2D1_RECT_F D2DOverlaySVG::get_maximize_label() const {
  D2D1_RECT_F result;
  auto height = (float)(thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top);
  auto width = (float)(thumbnail_scaled_rect.right - thumbnail_scaled_rect.left);
  if (width >= height) {
    result.top = thumbnail_scaled_rect.bottom + height * 0.210f;
    result.bottom = thumbnail_scaled_rect.bottom + height * 0.310f;
    result.left = thumbnail_scaled_rect.left + width * 0.009f;
    result.right = thumbnail_scaled_rect.right + width * 0.009f;
  } else {
    result.top = thumbnail_scaled_rect.top + height * 0.323f;
    result.bottom = thumbnail_scaled_rect.top + height * 0.398f;
    result.left = (float)thumbnail_scaled_rect.right;
    result.right = thumbnail_scaled_rect.right + width * 1.45f;
  }
  return result;
}
D2D1_RECT_F D2DOverlaySVG::get_minimize_label() const {
  D2D1_RECT_F result;
  auto height = (float)(thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top);
  auto width = (float)(thumbnail_scaled_rect.right - thumbnail_scaled_rect.left);
  if (width >= height) {
    result.top = thumbnail_scaled_rect.bottom + height * 0.8f;
    result.bottom = thumbnail_scaled_rect.bottom + height * 0.9f;
    result.left = thumbnail_scaled_rect.left + width * 0.009f;
    result.right = thumbnail_scaled_rect.right + width * 0.009f;
  } else {
    result.top = thumbnail_scaled_rect.top + height * 0.725f;
    result.bottom = thumbnail_scaled_rect.top + height * 0.800f;
    result.left = (float)thumbnail_scaled_rect.right;
    result.right = thumbnail_scaled_rect.right + width * 1.45f;
  }
  return result;
}
D2D1_RECT_F D2DOverlaySVG::get_snap_left() const {
  D2D1_RECT_F result;
  auto height = (float)(thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top);
  auto width = (float)(thumbnail_scaled_rect.right - thumbnail_scaled_rect.left);
  if (width >= height) {
    result.top = thumbnail_scaled_rect.bottom + height * 0.5f;
    result.bottom = thumbnail_scaled_rect.bottom + height * 0.6f;
    result.left = thumbnail_scaled_rect.left + width * 0.009f;
    result.right = thumbnail_scaled_rect.left + width * 0.339f;
  } else {
    result.top = thumbnail_scaled_rect.top + height * 0.523f;
    result.bottom = thumbnail_scaled_rect.top + height * 0.598f;
    result.left = (float)thumbnail_scaled_rect.right;
    result.right = thumbnail_scaled_rect.right + width * 0.450f;
  }
  return result;
}
D2D1_RECT_F D2DOverlaySVG::get_snap_right() const {
  D2D1_RECT_F result;
  auto height = (float)(thumbnail_scaled_rect.bottom - thumbnail_scaled_rect.top);
  auto width = (float)(thumbnail_scaled_rect.right - thumbnail_scaled_rect.left);
  if (width >= height) {
    result.top = thumbnail_scaled_rect.bottom + height * 0.5f;
    result.bottom = thumbnail_scaled_rect.bottom + height * 0.6f;
    result.left = thumbnail_scaled_rect.left + width * 0.679f;
    result.right = thumbnail_scaled_rect.right + width * 1.009f;
  } else {
    result.top = thumbnail_scaled_rect.top + height * 0.523f;
    result.bottom = thumbnail_scaled_rect.top + height * 0.598f;
    result.left = (float)thumbnail_scaled_rect.right + width;
    result.right = thumbnail_scaled_rect.right + width * 1.45f;
  }
  return result;
}



D2DOverlayWindow::D2DOverlayWindow() : total_screen({}), anim_time(0.3) {
  animation = Animation(anim_time);
  tasklist_thread = std::thread([&] {
    while (running) {
      // Removing <std::mutex> causes C3538 on std::unique_lock lock(mutex); in show(..)
      std::unique_lock<std::mutex> lock(tasklist_cv_mutex);
      tasklist_cv.wait(lock, [&] { return !running || tasklist_update; });
      if (!running)
        return;
      lock.unlock();
      while (running && tasklist_update) {
        std::vector<TasklistButton> buttons;
        if (tasklist.update_buttons(buttons)) {
          std::unique_lock lock(mutex);
          tasklist_buttons.swap(buttons);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(500));
      }
    }
  });
}

void D2DOverlayWindow::show(HWND active_window) {
  std::unique_lock lock(mutex);
  tasklist_buttons.clear();
  this->active_window = active_window;
  auto old_bck = colors.start_color_menu;
  auto colors_updated = colors.update();
  auto new_light_mode = (theme_setting == Light) || (theme_setting == System && colors.light_mode);
  if (initialized && (colors_updated || light_mode != new_light_mode)) {
    // update background colors
    landscape.recolor(old_bck, colors.start_color_menu);
    portrait.recolor(old_bck, colors.start_color_menu);
    for (auto& arrow : arrows) {
      arrow.recolor(old_bck, colors.start_color_menu);
    }
    light_mode = new_light_mode;
    if (light_mode) {
      landscape.recolor(0xDDDDDD, 0x222222);
      portrait.recolor(0xDDDDDD, 0x222222);
      for (auto& arrow : arrows) {
        arrow.recolor(0xDDDDDD, 0x222222);
      }
    } else {
      landscape.recolor(0x222222, 0xDDDDDD);
      portrait.recolor(0x222222, 0xDDDDDD);
      for (auto& arrow : arrows) {
        arrow.recolor(0x222222, 0xDDDDDD);
      }
    }
  }
  monitors = MonitorInfo::GetMonitors(true);
  // calculate the rect covering all the screens
  total_screen = ScreenSize(monitors[0].rect);
  for (auto& monitor : monitors) {
    total_screen.rect.left = min(total_screen.rect.left, monitor.rect.left);
    total_screen.rect.top = min(total_screen.rect.top, monitor.rect.top);
    total_screen.rect.right = max(total_screen.rect.right, monitor.rect.right);
    total_screen.rect.bottom = max(total_screen.rect.bottom, monitor.rect.bottom);
  }
  // make sure top-right corner of all the monitor rects is (0,0)
  monitor_dx = -total_screen.left();
  monitor_dy = -total_screen.top();
  total_screen.rect.left += monitor_dx;
  total_screen.rect.right += monitor_dx;
  total_screen.rect.top += monitor_dy;
  total_screen.rect.bottom += monitor_dy;
  tasklist.update();
  if (active_window) {
    // Ignore errors, if this fails we will just not show the thumbnail
    DwmRegisterThumbnail(hwnd, active_window, &thumbnail);
  }
  animation.reset();
  auto primary_screen = MonitorInfo::GetPrimaryMonitor();
  shown_start_time = std::chrono::steady_clock::now();
  lock.unlock();
  D2DWindow::show(primary_screen.left(), primary_screen.top(), primary_screen.width(), primary_screen.height());
  key_pressed.clear();
  // Check if taskbar is auto-hidden. If so, don't display the number arrows
  APPBARDATA param = {};
  param.cbSize = sizeof(APPBARDATA);
  if ((UINT)SHAppBarMessage(ABM_GETSTATE, &param) != ABS_AUTOHIDE) {
    tasklist_cv_mutex.lock();
    tasklist_update = true;
    tasklist_cv_mutex.unlock();
    tasklist_cv.notify_one();
  }
  Trace::EventShow();
}

void D2DOverlayWindow::animate(int vk_code) {
  animate(vk_code, 0);
}
void D2DOverlayWindow::animate(int vk_code, int offset) {
  if (!initialized || !use_overlay) {
    return;
  }
  bool done = false;
  for (auto& animation : key_animations) {
    if (animation.vk_code == vk_code) {
      animation.animation.reset(0.1, 0, 1);
      done = true;
    }
  }
  if (done) {
    return;
  }
  AnimateKeys animation;
  std::wstring id;
  animation.vk_code = vk_code;
  winrt::com_ptr<ID2D1SvgElement> button_letter, parrent;
  if (vk_code >= 0x41 && vk_code <= 0x5A) {
    id.push_back('A' + (vk_code - 0x41));
  } else {
    switch (vk_code) {
    case VK_SNAPSHOT:
    case VK_PRINT:
      id = L"PrnScr";
      break;
    case VK_CONTROL:
    case VK_LCONTROL:
    case VK_RCONTROL:
      id = L"Ctrl";
      break;
    case VK_UP:
      id = L"KeyUp";
      break;
    case VK_LEFT:
      id = L"KeyLeft";
      break;
    case VK_DOWN:
      id = L"KeyDown";
      break;
    case VK_RIGHT:
      id = L"KeyRight";
      break;
    case VK_OEM_PLUS:
    case VK_ADD:
      id = L"KeyPlus";
      break;
    case VK_OEM_MINUS:
    case VK_SUBTRACT:
      id = L"KeyMinus";
      break;
    case VK_TAB:
      id = L"Tab";
      break;
    case VK_RETURN:
      id = L"Enter";
      break;
    default:
      return;
    }
  }

  if (offset > 0) {
    id += L"_" + std::to_wstring(offset);
  }
  button_letter = use_overlay->find_element(id);
  if (!button_letter) {
    return;
  }
  button_letter->GetParent(parrent.put());
  if (!parrent) {
    return;
  }
  parrent->GetPreviousChild(button_letter.get(), animation.button.put());
  if (!animation.button || !animation.button->IsAttributeSpecified(L"fill")) {
    animation.button = nullptr;
    parrent->GetNextChild(button_letter.get(), animation.button.put());
  }
  if (!animation.button || !animation.button->IsAttributeSpecified(L"fill")) {
    return;
  }
  winrt::com_ptr<ID2D1SvgPaint> paint;
  animation.button->GetAttributeValue(L"fill", paint.put());
  paint->GetColor(&animation.original);
  animate(vk_code, offset + 1);
  std::unique_lock lock(mutex);
  animation.animation.reset(0.1, 0, 1);
  key_animations.push_back(animation);
  key_pressed.push_back(vk_code);
}

void D2DOverlayWindow::on_show() { 
  // show override does everything
}

void D2DOverlayWindow::on_hide() {
  tasklist_cv_mutex.lock();
  tasklist_update = false;
  tasklist_cv_mutex.unlock();
  tasklist_cv.notify_one();
  if (thumbnail) {
    DwmUnregisterThumbnail(thumbnail);
  }
  std::chrono::steady_clock::time_point shown_end_time = std::chrono::steady_clock::now();
  Trace::EventHide(std::chrono::duration_cast<std::chrono::milliseconds>(shown_end_time - shown_start_time).count(), key_pressed);
  key_pressed.clear();
}

D2DOverlayWindow::~D2DOverlayWindow() {
  tasklist_cv_mutex.lock();
  running = false;
  tasklist_cv_mutex.unlock();
  tasklist_cv.notify_one();
  tasklist_thread.join();
}

void D2DOverlayWindow::apply_overlay_opacity(float opacity) {
  if (opacity <= 0.0f) {
    opacity = 0.0f;
  }
  if (opacity >= 1.0f) {
    opacity = 1.0f;
  }
  overlay_opacity = opacity;
}

void D2DOverlayWindow::set_theme(const std::wstring& theme) {
  if (theme == L"light") {
    theme_setting = Light;
  } else if (theme == L"dark") {
    theme_setting = Dark;
  } else {
    theme_setting = System;
  }
}

float D2DOverlayWindow::get_overlay_opacity() {
  return overlay_opacity;
}

void D2DOverlayWindow::init() {
  colors.update();
  landscape.load(L"svgs\\overlay.svg", d2d_dc.get())
           .find_thumbnail(L"path-1")
           .find_window_group(L"Group-1")
           .recolor(0x000000, colors.start_color_menu);
  portrait.load(L"svgs\\overlay_portrait.svg", d2d_dc.get())
          .find_thumbnail(L"path-1")
          .find_window_group(L"Group-1")
          .recolor(0x000000, colors.start_color_menu);
  no_active.load(L"svgs\\no_active_window.svg", d2d_dc.get());
  arrows.resize(10);
  for (unsigned i = 0; i < arrows.size(); ++i) {
    arrows[i].load(L"svgs\\" + std::to_wstring((i + 1) % 10) + L".svg", d2d_dc.get())
             .recolor(0x000000, colors.start_color_menu);
  }
  light_mode = (theme_setting == Light) || (theme_setting == System && colors.light_mode);
  if (!light_mode) {
    landscape.recolor(0x222222, 0xDDDDDD);
    portrait.recolor(0x222222, 0xDDDDDD);
    for (auto& arrow : arrows) {
      arrow.recolor(0x222222, 0xDDDDDD);
    }
  }
}

void D2DOverlayWindow::resize() {
  window_rect = *get_window_pos(hwnd);
  float no_active_scale, font;
  if (window_width >= window_height) { // portriat is broke right now
    use_overlay = &landscape;
    no_active_scale = 0.3f;
    font = 15.0f;
  } else {
    use_overlay = &portrait;
    no_active_scale = 0.5f;
    font = 16.0f;
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

void render_arrow(D2DSVG& arrow, TasklistButton& button, RECT window, float max_scale, ID2D1DeviceContext5* d2d_dc) {
  int dx = 0, dy = 0;
  // Calculate taskbar orientation
  arrow.toggle_element(L"left", false);
  arrow.toggle_element(L"right", false);
  arrow.toggle_element(L"top", false);
  arrow.toggle_element(L"bottom", false);
  if (button.x <= window.left) { // taskbar on left
    dx = 1;
    arrow.toggle_element(L"left", true);
  }
  if (button.x >= window.right) { // taskbar on right
    dx = -1;
    arrow.toggle_element(L"right", true);
  }
  if (button.y <= window.top) { // taskbar on top
    dy = 1;
    arrow.toggle_element(L"top", true);
  }
  if (button.y >= window.bottom) { // taskbar on bottom
    dy = -1;
    arrow.toggle_element(L"bottom", true);
  }
  double arrow_ratio = (double)arrow.height() / arrow.width();
  if (dy != 0) {
    // assume button is 25% wider than taller, +10% to make room for each of the arrows that are hidden
    auto render_arrow_width = (int)(button.height * 1.25f * 1.2f);
    auto render_arrow_height = (int)(render_arrow_width * arrow_ratio);
    arrow.resize(button.x + (button.width - render_arrow_width) / 2,
                 dy == -1 ? button.y - render_arrow_height : 0,
                 render_arrow_width, render_arrow_height, 0.95f, max_scale)
         .render(d2d_dc);
  }
  else {
    // same as above - make room for the hidden arrow
    auto render_arrow_height = (int)(button.height * 1.2f);
    auto render_arrow_width = (int)(render_arrow_height / arrow_ratio);
    arrow.resize(dx == -1 ? button.x - render_arrow_width : 0,
                 button.y + (button.height - render_arrow_height) / 2,
                 render_arrow_width, render_arrow_height, 0.95f, max_scale)
         .render(d2d_dc);
  }
}

bool D2DOverlayWindow::show_thumbnail(const RECT& rect, double alpha) {
  if (!thumbnail) {
    return false;
  }
  DWM_THUMBNAIL_PROPERTIES thumb_properties;
  thumb_properties.dwFlags = DWM_TNP_SOURCECLIENTAREAONLY | DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY;
  thumb_properties.fSourceClientAreaOnly = FALSE;
  thumb_properties.fVisible = TRUE;
  thumb_properties.opacity = (BYTE)(255*alpha);
  thumb_properties.rcDestination = rect;
  if (DwmUpdateThumbnailProperties(thumbnail, &thumb_properties) != S_OK) {
    return false;
  }
  return true;
}

void D2DOverlayWindow::hide_thumbnail() {
  DWM_THUMBNAIL_PROPERTIES thumb_properties;
  thumb_properties.dwFlags = DWM_TNP_VISIBLE;
  thumb_properties.fVisible = FALSE;
  DwmUpdateThumbnailProperties(thumbnail, &thumb_properties);
}

void D2DOverlayWindow::render(ID2D1DeviceContext5* d2d_dc) {
  if (!winkey_held() || is_start_visible()) {
	  auto current_anim_value = animation.value(Animation::AnimFunctions::LINEAR);
	  if (!hiding) { // when user is done viewing the overlay
		  animation.reset(anim_time * current_anim_value);
		  hiding = true;
	  }
	  else if (current_anim_value == 1.0) { // animation to hide overlay has finished
		  hide();
		  instance->was_hidden();
		  animation.reset(anim_time);
		  hiding = false;
		  return;
	  }
  }
  d2d_dc->Clear();
  int x_offset = 0, y_offset = 0, dimention = 0;
  int alpha;
  double pos_anim_value;
  auto current_anim_value = (float)animation.value(Animation::AnimFunctions::LINEAR);
  if (hiding) {
	  alpha = 255 * (1.0 - current_anim_value);
	  pos_anim_value = animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
  } else {
	  alpha = 255 * current_anim_value;
	  pos_anim_value = 1 - animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
  }
  SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);

  if (!tasklist_buttons.empty()) {
    if (tasklist_buttons[0].x <= window_rect.left) { // taskbar on left
      x_offset = (int)(-pos_anim_value * use_overlay->width() * use_overlay->get_scale());
    }
    if (tasklist_buttons[0].x >= window_rect.right) { // taskbar on right
      x_offset = (int)(pos_anim_value * use_overlay->width() * use_overlay->get_scale());
    }
    if (tasklist_buttons[0].y <= window_rect.top) { // taskbar on top
      y_offset = (int)(-pos_anim_value * use_overlay->height() * use_overlay->get_scale());
    }
    if (tasklist_buttons[0].y >= window_rect.bottom) { // taskbar on bottom
      y_offset = (int)(pos_anim_value * use_overlay->height() * use_overlay->get_scale());
    }
  } else {
    x_offset = 0;
    y_offset = (int)(pos_anim_value * use_overlay->height() * use_overlay->get_scale());
  }
  // Draw background
  winrt::com_ptr<ID2D1SolidColorBrush> brush;
  float brush_opacity = get_overlay_opacity();
  D2D1_COLOR_F brushColor = light_mode ? D2D1::ColorF(1.0f, 1.0f, 1.0f, brush_opacity) : D2D1::ColorF(0, 0, 0, brush_opacity);
  winrt::check_hresult(d2d_dc->CreateSolidColorBrush(brushColor, brush.put()));
  D2D1_RECT_F background_rect = {};
  background_rect.bottom = (float)window_height;
  background_rect.right = (float)window_width;
  d2d_dc->SetTransform(D2D1::Matrix3x2F::Identity());
  d2d_dc->FillRectangle(background_rect, brush.get());
 
  // Thumbnail logic:
  auto window_state = get_window_state(active_window);
  auto thumb_window = get_window_pos(active_window);
  bool minature_shown = active_window != nullptr && thumbnail != nullptr && thumb_window && window_state != MINIMIZED;
  RECT client_rect;
  if (thumb_window && GetClientRect(active_window, &client_rect)) {
    int dx = ((thumb_window->right - thumb_window->left) - (client_rect.right - client_rect.left)) / 2;
    int dy = ((thumb_window->bottom - thumb_window->top) - (client_rect.bottom - client_rect.top)) / 2;
    thumb_window->left += dx;
    thumb_window->right -= dx;
    thumb_window->top += dy;
    thumb_window->bottom -= dy;
  }
  if (minature_shown && thumb_window->right - thumb_window->left <= 0 || thumb_window->bottom - thumb_window->top <= 0) {
    minature_shown = false;
  }
  bool render_monitors = true;
  auto total_monitor_with_screen = total_screen;
  if (thumb_window) {
    total_monitor_with_screen.rect.left = min(total_monitor_with_screen.rect.left, thumb_window->left + monitor_dx);
    total_monitor_with_screen.rect.top = min(total_monitor_with_screen.rect.top, thumb_window->top + monitor_dy);
    total_monitor_with_screen.rect.right = max(total_monitor_with_screen.rect.right, thumb_window->right + monitor_dx);
    total_monitor_with_screen.rect.bottom = max(total_monitor_with_screen.rect.bottom, thumb_window->bottom + monitor_dy);
  }
  // Only allow the new rect beeing slight bigger.
  if (total_monitor_with_screen.width() - total_screen.width() > (thumb_window->right - thumb_window->left) / 2 ||
      total_monitor_with_screen.height() - total_screen.height() > (thumb_window->bottom - thumb_window->top) / 2) {
    render_monitors = false;
  }
  if (window_state == MINIMIZED) {
    total_monitor_with_screen = total_screen;
  }
  auto rect_and_scale = use_overlay->get_thumbnail_rect_and_scale(0, 0, total_monitor_with_screen.width(), total_monitor_with_screen.height(), 1);
  if (minature_shown) {
    RECT thumbnail_pos;
    if (render_monitors) {
      thumbnail_pos.left = (int)((thumb_window->left + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
      thumbnail_pos.top = (int)((thumb_window->top + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
      thumbnail_pos.right = (int)((thumb_window->right + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
      thumbnail_pos.bottom = (int)((thumb_window->bottom + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
    } else {
      thumbnail_pos = use_overlay->get_thumbnail_rect_and_scale(0, 0, thumb_window->right - thumb_window->left, thumb_window->bottom - thumb_window->top, 1).rect;
    }
    // If the animation is done show the thumbnail
    //   we cannot animate the thumbnail, the animation lags behind
    minature_shown = show_thumbnail(thumbnail_pos, 1.0f);
  } else {
    hide_thumbnail();
  }
  if (window_state == MINIMIZED) {
    render_monitors = true;
  }
  // render the monitors
  if (render_monitors) {
    brushColor = D2D1::ColorF(colors.desktop_fill_color, 1.0f);
    brush = nullptr;
    winrt::check_hresult(d2d_dc->CreateSolidColorBrush(brushColor, brush.put()));
    for (auto& monitor : monitors) {
      D2D1_RECT_F monitor_rect;
      monitor_rect.left = (float)((monitor.rect.left + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
      monitor_rect.top = (float)((monitor.rect.top + monitor_dy) * rect_and_scale.scale + rect_and_scale.rect.top);
      monitor_rect.right = (float)((monitor.rect.right + monitor_dx) * rect_and_scale.scale + rect_and_scale.rect.left);
      monitor_rect.bottom = (float)((monitor.rect.bottom + monitor_dy)  * rect_and_scale.scale + rect_and_scale.rect.top);
      d2d_dc->SetTransform(D2D1::Matrix3x2F::Identity());
      d2d_dc->FillRectangle(monitor_rect, brush.get());
    }
  }
  // Finalize the overlay - dimm the buttons if no thumbnail is present and show "No active window"
  use_overlay->toggle_window_group(minature_shown || window_state == MINIMIZED);
  if (!minature_shown && window_state != MINIMIZED) {
    no_active.render(d2d_dc);
    window_state = UNKNONW;
  }

  // Set the animation - move the draw window according to animation step
  auto popin = D2D1::Matrix3x2F::Translation((float)x_offset, (float)y_offset);
  d2d_dc->SetTransform(popin);

  // Animate keys
  for (unsigned id = 0; id < key_animations.size();) {
    auto& animation = key_animations[id];
    D2D1_COLOR_F color;
    auto value = (float)animation.animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
    color.a = 1.0f;
    color.r = animation.original.r + (1.0f - animation.original.r) * value;
    color.g = animation.original.g + (1.0f - animation.original.g) * value;
    color.b = animation.original.b + (1.0f - animation.original.b) * value;
    animation.button->SetAttributeValue(L"fill", color);
    if (animation.animation.done()) {
      if (value == 1) {
        animation.animation.reset(0.05, 1, 0);
        animation.animation.value(Animation::AnimFunctions::EASE_OUT_EXPO);
      } else {
        key_animations.erase(key_animations.begin() + id);
        continue;
      }
    }
    ++id;
  }
  // Finally: render the overlay...
  use_overlay->render(d2d_dc);
  // ... window arrows texts ...
  std::wstring left, right, up, down;
  bool left_disabled = false;
  bool right_disabled = false;
  bool up_disabled = false;
  bool down_disabled = false;
  switch (window_state) {
  case MINIMIZED:
    left = L"No action";
    left_disabled = true;
    right = L"No action";
    right_disabled = true;
    up = L"Restore";
    down = L"No action";
    down_disabled = true;
    break;
  case MAXIMIZED:
    left = L"Snap left";
    right = L"Snap right";
    up = L"No action";
    up_disabled = true;
    down = L"Restore";
    break;
  case SNAPED_TOP_LEFT:
    left = L"Snap upper right";
    right = L"Snap upper right";
    up = L"Maximize";
    down = L"Snap left";
    break;
  case SNAPED_LEFT:
    left = L"Snap right";
    right = L"Restore";
    up = L"Snap upper left";
    down = L"Snap lower left";
    break;
  case SNAPED_BOTTOM_LEFT:
    left = L"Snap lower right";
    right = L"Snap lower right";
    up = L"Snap left";
    down = L"Minimize";
    break;
  case SNAPED_TOP_RIGHT:
    left = L"Snap upper left";
    right = L"Snap upper left";
    up = L"Maximize";
    down = L"Snap right";
    break;
  case SNAPED_RIGHT:
    left = L"Restore";
    right = L"Snap left";
    up = L"Snap upper right";
    down = L"Snap lower right";
    break;
  case SNAPED_BOTTOM_RIGHT:
    left = L"Snap lower left";
    right = L"Snap lower left";
    up = L"Snap right";
    down = L"Minimize";
    break;
  case RESTORED:
    left = L"Snap left";
    right = L"Snap right";
    up = L"Maximize";
    down = L"Minimize";
    break;
  default:
    left = L"No action";
    left_disabled = true;
    right = L"No action";
    right_disabled = true;
    up = L"No action";
    up_disabled = true;
    down = L"No action";
    down_disabled = true;
  }
  auto text_color = D2D1::ColorF(light_mode ? 0x222222 : 0xDDDDDD, minature_shown || window_state == MINIMIZED ? 1.0f : 0.3f);
  use_overlay->find_element(L"KeyUpGroup")->SetAttributeValue(L"fill-opacity", up_disabled ? 0.3f : 1.0f);
  text.set_aligment_center().write(d2d_dc, text_color, use_overlay->get_maximize_label(), up);
  use_overlay->find_element(L"KeyDownGroup")->SetAttributeValue(L"fill-opacity", down_disabled ? 0.3f : 1.0f);
  text.write(d2d_dc, text_color, use_overlay->get_minimize_label(), down);
  use_overlay->find_element(L"KeyLeftGroup")->SetAttributeValue(L"fill-opacity", left_disabled ? 0.3f : 1.0f);
  text.set_aligment_right().write(d2d_dc, text_color, use_overlay->get_snap_left(), left);
  use_overlay->find_element(L"KeyRightGroup")->SetAttributeValue(L"fill-opacity", right_disabled ? 0.3f : 1.0f);
  text.set_aligment_left().write(d2d_dc, text_color, use_overlay->get_snap_right(), right);
  // ... and the arrows with numbers
  for (auto&& button : tasklist_buttons) {
    if ((size_t)(button.keynum) - 1 >= arrows.size()) {
      continue;
    }
    render_arrow(arrows[(size_t)(button.keynum) - 1], button, window_rect, use_overlay->get_scale(), d2d_dc);
  }
}
