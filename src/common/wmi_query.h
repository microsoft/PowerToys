#pragma once
#include "common.h"
#include "utils/traits.h"

#include <functional>
#include <string_view>

class wmi_connection
{
  struct impl;
  pimpl_t<impl> _impl;
  
  wmi_connection();
public:
  static wmi_connection initialize();

  void select_all(const wchar_t * statement, std::function<void(std::wstring_view)> callback);
};
