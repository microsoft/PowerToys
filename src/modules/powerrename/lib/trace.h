#pragma once

class Trace {
public:
  static void RegisterProvider() noexcept;
  static void UnregisterProvider() noexcept;
  static void Invoked() noexcept;
  static void InvokedRet(_In_ HRESULT hr) noexcept;
  static void EnablePowerRename(_In_ bool enabled) noexcept;
  static void UIShownRet(_In_ HRESULT hr) noexcept;
  static void RenameOperation(
      _In_ UINT totalItemCount,
      _In_ UINT selectedItemCount,
      _In_ UINT renameItemCount,
      _In_ DWORD flags,
      _In_ PCWSTR extensionList) noexcept;
  static void SettingsChanged() noexcept;
};
