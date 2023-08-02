#pragma once
#include <robmikh.common/DesktopWindow.h>

enum class CropAndLockType
{
	Reparent,
	Thumbnail,
};

struct SettingsWindow : robmikh::common::desktop::DesktopWindow<SettingsWindow>
{
	static const uint32_t TrayIconMessage = WM_USER + 1;
	static const std::wstring ClassName;
	SettingsWindow(std::wstring const& title, int width, int height);
	LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);

	CropAndLockType GetCropAndLockType() { return m_currentCropAndLockType; }

private:
	struct WindowTypeEntry
	{
		CropAndLockType Type;
		std::wstring DisplayName;
	};

	static void RegisterWindowClass();

	void Hide();
	void CreateControls(HINSTANCE instance);
	void OnSettingsMenuItemClicked();

private:
	std::unique_ptr<robmikh::common::desktop::PopupMenu> m_trayIconMenu;
	std::vector<WindowTypeEntry> m_windowTypes;
	CropAndLockType m_currentCropAndLockType = CropAndLockType::Reparent;
	HWND m_windowTypeComboBox = nullptr;
	std::unique_ptr<robmikh::common::desktop::controls::StackPanel> m_controls;
	wil::shared_hfont m_font;
};