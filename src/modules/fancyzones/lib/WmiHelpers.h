#pragma once

#include <string_view>

struct WmiMonitorID
{
  std::wstring _instance_name;
  std::wstring _friendly_name;
  std::wstring _serial_number_id;

  inline std::wstring hardware_id() const
  {
    return _friendly_name + L"_" + _serial_number_id;
  }
};

WmiMonitorID parse_monitorID_from_dtd(std::wstring_view xml);
