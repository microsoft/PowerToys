#include "pch.h"
#include "start_visible.h"

bool is_start_visible() {
  static winrt::com_ptr<IAppVisibility> app_visibility;
  if (!app_visibility) {
    winrt::check_hresult(CoCreateInstance(CLSID_AppVisibility,
                                          nullptr,
                                          CLSCTX_INPROC_SERVER,
                                          __uuidof(app_visibility),
                                          app_visibility.put_void()));
  }
  BOOL visible;
  auto result = app_visibility->IsLauncherVisible(&visible);
  return SUCCEEDED(result) && visible;
}
