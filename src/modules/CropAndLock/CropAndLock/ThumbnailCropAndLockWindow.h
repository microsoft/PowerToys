#pragma once
#include <robmikh.common/DesktopWindow.h>
#include "CropAndLockWindow.h"

struct ThumbnailCropAndLockWindow : robmikh::common::desktop::DesktopWindow<ThumbnailCropAndLockWindow>, CropAndLockWindow
{
	static const std::wstring ClassName;
	ThumbnailCropAndLockWindow(std::wstring const& titleString, int width, int height);
	~ThumbnailCropAndLockWindow() override;
	LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);

	HWND Handle() override { return m_window; }
	void CropAndLock(HWND windowToCrop, RECT cropRect) override;
	void OnClosed(std::function<void(HWND)> callback) override { m_closedCallback = callback; }

private:
	static void RegisterWindowClass();

	void Hide();
	void DisconnectTarget();

private:
	HWND m_currentTarget = nullptr;
	POINT m_previousPosition = {};

	unique_hthumbnail m_thumbnail;
	RECT m_destRect = {};
	RECT m_sourceRect = {};

	bool m_destroyed = false;
	std::function<void(HWND)> m_closedCallback;
};