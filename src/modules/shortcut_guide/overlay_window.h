#pragma once
#include "common/d2d_svg.h"
#include "common/d2d_window.h"
#include "common/d2d_text.h"
#include "common/monitors.h"
#include "common/animation.h"
#include "common/windows_colors.h"
#include "common/tasklist_positions.h"

struct ScaleResult {
  double scale;
  RECT rect;
};

class D2DOverlaySVG : public D2DSVG {
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

struct AnimateKeys {
  Animation animation;
  D2D1_COLOR_F original;
  winrt::com_ptr<ID2D1SvgElement> button;
  int vk_code;
};

class D2DOverlayWindow : public D2DWindow {
public:
  D2DOverlayWindow();
  void show(HWND active_window);
  void animate(int vk_code);
  ~D2DOverlayWindow();
  void apply_overlay_opacity(float opacity);

private:
  void animate(int vk_code, int offset);
  bool show_thumbnail(const RECT& rect, double alpha);
  void hide_thumbnail();
  virtual void init() override;
  virtual void resize() override;
  virtual void render(ID2D1DeviceContext5* d2d_dc) override;
  virtual void on_show() override;
  virtual void on_hide() override;
  float get_overlay_opacity();

  bool running = true;
  std::vector<AnimateKeys> key_animations;
  std::vector<int> key_pressed;
  std::vector<MonitorInfo> monitors;
  ScreenSize total_screen;
  int monitor_dx = 0, monitor_dy = 0;
  D2DText text;
  WindowsColors colors;
  Animation animation;
  RECT window_rect = {};
  Tasklist tasklist;
  std::vector<TasklistButton> tasklist_buttons;
  std::thread tasklist_thread;
  bool tasklist_update = false;
  std::mutex tasklist_cv_mutex;
  std::condition_variable tasklist_cv;

  HTHUMBNAIL thumbnail;
  HWND active_window = nullptr;
  D2DOverlaySVG landscape, portrait;
  D2DOverlaySVG* use_overlay = nullptr;
  D2DSVG no_active;
  std::vector<D2DSVG> arrows;
  std::chrono::steady_clock::time_point shown_start_time;
  float overlay_opacity = 0.9f;
};
