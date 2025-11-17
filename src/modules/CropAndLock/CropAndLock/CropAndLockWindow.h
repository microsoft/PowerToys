#pragma once

struct CropAndLockWindow
{
	virtual ~CropAndLockWindow() {}

	virtual HWND Handle() = 0;
	virtual void CropAndLock(HWND windowToCrop, RECT cropRect) = 0;
	virtual void OnClosed(std::function<void(HWND)> callback) = 0;
};
