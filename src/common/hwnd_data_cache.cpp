#include "pch.h"
#include "hwnd_data_cache.h"

HWNDDataCache hwnd_cache;

WindowAndProcPath HWNDDataCache::get_window_and_path(HWND hwnd) {
  std::unique_lock lock(mutex);
  auto ptr = get_internal(hwnd);
  return ptr ? *ptr : WindowAndProcPath{};
}

HWND HWNDDataCache::get_window(HWND hwnd) {
  std::unique_lock lock(mutex);
  auto ptr = get_internal(hwnd);
  return ptr ? ptr->hwnd : nullptr;
}

WindowAndProcPath* HWNDDataCache::get_internal(HWND hwnd) {
  auto root = GetAncestor(hwnd, GA_ROOT);
  // Filter the fast and easy cases
  if (is_invalid_hwnd(root) ||
    is_invalid_class(root) ||
    is_invalid_style(root)) {
    return nullptr;
  }
  // Get the HWND process path from the cache
  DWORD pid = GetWindowThreadProcessId(root, nullptr);
  auto cache_ptr = get_from_cache(root, pid);
  if (cache_ptr == nullptr) {
    cache_ptr = put_in_cache(root, pid);
  }
  // If the app is a UWP app, check if it isnt banned
  if (is_uwp_app(root) && is_invalid_uwp_app(cache_ptr->process_path)) {
    // cache the HWND of the invalid app so we wont search for it again
    invalid_hwnds.push_back(root);
    return nullptr;
  }

  return cache_ptr;
}

WindowAndProcPath* HWNDDataCache::get_from_cache(HWND root, DWORD pid) {
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

WindowAndProcPath* HWNDDataCache::put_in_cache(HWND root, DWORD pid) {
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
bool HWNDDataCache::is_invalid_style(HWND hwnd) const {
  auto style = GetWindowLong(hwnd, GWL_STYLE);
  for (auto invalid : invalid_basic_styles) {
    if ((invalid & style) != 0) {
      return true;
    }
  }
  style = GetWindowLong(hwnd, GWL_EXSTYLE);
  for (auto invalid : invalid_ext_styles) {
    if ((invalid & style) != 0) {
      return true;
    }
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