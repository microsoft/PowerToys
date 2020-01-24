#include "pch.h"
#include "hwnd_data_cache.h"
#include "common.h"

HWNDDataCache hwnd_cache;

WindowInfo HWNDDataCache::get_window_info(HWND hwnd) {
  std::unique_lock lock(mutex);
  auto ptr = get_internal(hwnd);
  return ptr ? *ptr : WindowInfo{};
}

WindowInfo* HWNDDataCache::get_internal(HWND hwnd) {
  // Filter the fast and easy cases
  auto style = GetWindowLong(hwnd, GWL_STYLE);
  if (!IsWindowVisible(hwnd) ||
      is_invalid_hwnd(hwnd) ||
      is_invalid_class(hwnd) ||
      (style & WS_CHILD) == WS_CHILD) {
    return nullptr;
  }
  // Get the HWND process path from the cache
  DWORD pid = GetWindowThreadProcessId(hwnd, nullptr);
  auto cache_ptr = get_from_cache(hwnd, pid);
  if (cache_ptr == nullptr) {
    cache_ptr = put_in_cache(hwnd, pid);
  }
  // If the app is a UWP app, check if it isnt banned
  if (is_uwp_app(hwnd) && is_invalid_uwp_app(cache_ptr->process_path)) {
    // cache the HWND of the invalid app so we wont search for it again
    invalid_hwnds.push_back(hwnd);
    return nullptr;
  }
  cache_ptr->is_valid = true;
  cache_ptr->has_owner = (GetAncestor(hwnd, GA_ROOT) != hwnd) || GetWindow(hwnd, GW_OWNER) != nullptr;
  auto ex_style = GetWindowLong(hwnd, GWL_EXSTYLE);
  cache_ptr->standard = !((style & WS_DISABLED) ||
                          (ex_style & WS_EX_TOOLWINDOW) ||
                          (ex_style & WS_EX_NOACTIVATE));
  cache_ptr->resizable = (style & WS_THICKFRAME) || (style & WS_MAXIMIZEBOX);
  return cache_ptr;
}

WindowInfo* HWNDDataCache::get_from_cache(HWND root, DWORD pid) {
  auto next = next_timestamp();
  auto it = std::find_if(begin(cache), end(cache), [&](const auto& entry) {
    return root == entry.data.hwnd && pid == entry.pid;
  });
  if (it != end(cache)) {
    it->atime = next;
    return &(it->data);
  }
  else {
    return nullptr;
  }
}

WindowInfo* HWNDDataCache::put_in_cache(HWND root, DWORD pid) {
  auto next = next_timestamp();
  auto it = std::min_element(begin(cache), end(cache), [](const auto& lhs, const auto& rhs) {
    return lhs.atime < rhs.atime;
  });
  it->atime = next;
  it->pid = pid;
  it->data.hwnd = root;
  it->data.process_path = get_process_path(root);
  return &(it->data);
}

bool HWNDDataCache::is_invalid_hwnd(HWND hwnd) const {
  return std::find(begin(invalid_hwnds), end(invalid_hwnds), hwnd) != end(invalid_hwnds);
}
bool HWNDDataCache::is_invalid_class(HWND hwnd) const {
  std::array<char, 256> class_name;
  GetClassNameA(hwnd, class_name.data(), static_cast<int>(class_name.size()));
  for (auto invalid : invalid_classes) {
    if (strcmp(invalid, class_name.data()) == 0)
      return true;
  }
  return false;
}

bool HWNDDataCache::is_uwp_app(HWND hwnd) const {
  std::array<char, 256> class_name;
  GetClassNameA(hwnd, class_name.data(), static_cast<int>(class_name.size()));
  return strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0;
}
bool HWNDDataCache::is_invalid_uwp_app(const std::wstring& process_path) const {
  for (const auto& invalid : invalid_uwp_apps) {
    // check if process_path ends in "invalid"
    if (process_path.length() >= invalid.length() &&
      process_path.compare(process_path.length() - invalid.length(), invalid.length(), invalid) == 0) {
      return true;
    }
  }
  return false;
}

unsigned HWNDDataCache::next_timestamp() {
  auto next = ++current_timestamp;
  if (next == 0) {
    // Handle overflow by invalidating the cache
    for (auto& entry : cache) {
      entry.data.hwnd = nullptr;
    }
  }
  return next;
}
