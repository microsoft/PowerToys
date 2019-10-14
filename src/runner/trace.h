#pragma once

class Trace {
public:
  static void RegisterProvider();
  static void UnregisterProvider();
  static void EventLaunch(std::wstring versionNumber);
};
