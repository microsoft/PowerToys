#pragma once
#include <robmikh.common/DesktopWindow.h>
#include "CropAndLockWindow.h"
#include "ChildWindow.h"

struct ReparentCropAndLockWindow : robmikh::common::desktop::DesktopWindow<ReparentCropAndLockWindow>, CropAndLockWindow
{
	static const std::wstring ClassName;
	ReparentCropAndLockWindow(std::wstring const& titleString, int width, int height);
	~ReparentCropAndLockWindow() override;
	LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);

	HWND Handle() override { return m_window; }
	void CropAndLock(HWND windowToCrop, RECT cropRect) override;
	void OnClosed(std::function<void(HWND)> callback) override { m_closedCallback = callback; }

private:
	static void RegisterWindowClass();

	void Hide();
	void DisconnectTarget();
	void SaveOriginalState();
	void RestoreOriginalState();

private:
	HWND m_currentTarget = nullptr;
	std::unique_ptr<ChildWindow> m_childWindow;
	bool m_destroyed = false;
	std::function<void(HWND)> m_closedCallback;

	LONG_PTR originalExStyle = 0;
	LONG_PTR originalStyle = 0;
	WINDOWPLACEMENT originalPlacement = { sizeof(WINDOWPLACEMENT) };
	RECT originalRect = {};
};